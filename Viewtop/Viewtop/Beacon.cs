using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Gosub.Viewtop
{
    /// <summary>
    /// Broadcast a beacon, and communicate with remote computers
    /// </summary>
    class Beacon
    {
        const int BROADCAST_PORT = 24706;
        const int BROADCAST_TIME_MS = 1400;
        const int BROADCAST_DETECT_MS = 1600;
        public delegate void PeerChangedDelegate(Peer peer);

        byte[] mHeader;
        string mHeaderStr;
        string mSourceId = Guid.NewGuid().ToString();
        Info mInfo;
        Dictionary<string, Peer> mPeers = new Dictionary<string, Peer>();

        Socket mSocketBroadcast; // Receive broadcasts only (no transmit)
        Socket mSocketUnicast;   // Transmit broadcasts and receive direct responses

        DateTime mBroadcastTime;
        List<IPAddress> mBroadcastAddresses;

        JsonSerializer mJsonSerializer = new JsonSerializer();
        MemoryStream mWriteBuffer = new MemoryStream();
        byte[] mReadBuffer = new byte[0];

        public event PeerChangedDelegate PeerAdded;
        public event PeerChangedDelegate PeerRemoved;
        public event PeerChangedDelegate PeerConnectionEstablishedChanged;

        /// <summary>
        /// Start the beacon with the given beacon info.
        /// Inherit from Info and add extra application information.
        /// </summary>
        public void Start(string packetHeader, Info info)
        {
            Stop();

            mInfo = info;
            mHeaderStr = packetHeader;
            mHeader = Encoding.UTF8.GetBytes(packetHeader);

            mSocketBroadcast = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            mSocketBroadcast.ExclusiveAddressUse = false;
            mSocketBroadcast.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            mSocketBroadcast.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));

            mSocketUnicast = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            mSocketUnicast.Bind(new IPEndPoint(IPAddress.Any, 0));

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            mBroadcastAddresses = EnumerateNetworks();
        }

        public void Stop()
        {
            // Send app exit notification to all peers
            if (mSocketUnicast != null)
                foreach (var peer in mPeers)
                    try { SendResponse(mSocketUnicast, peer.Value.EndPoint, State.Exit); }
                    catch {  }

            // Close network
            NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
            if (mSocketBroadcast != null)
                mSocketBroadcast.Dispose();
            if (mSocketUnicast != null)
                mSocketUnicast.Dispose();
            mSocketBroadcast = null;
            mSocketUnicast = null;

            PeerAdded = null;
            PeerRemoved = null;
            PeerConnectionEstablishedChanged = null;
        }

        public Info BroadcastInfo
        {
            get { return mInfo; }
            set { mInfo = value; }
        }

        /// <summary>
        /// Get a list of all broadcast networks
        /// </summary>
        public IPAddress []GetBroadcastAddresses()
        {
            if (mBroadcastAddresses == null)
                mBroadcastAddresses = EnumerateNetworks();
            return mBroadcastAddresses.ToArray();
        }

        /// <summary>
        /// Return a list of active local IP addresses.
        /// Using Dns.GetHostEntry can return multple IP addresses which
        /// are not necessarily active.  This returns only active local
        /// IP addresses since we are actively pinging ourself.
        /// </summary>
        /// <returns></returns>
        public IPAddress[]GetLocalAddresses()
        {
            // Find unique addresses
            var uniqueAddresses = new Dictionary<IPAddress, bool>();
            foreach (var peer in mPeers)
                if (peer.Value.ThisBeacon)
                    uniqueAddresses[peer.Value.EndPoint.Address] = true;
            return uniqueAddresses.Keys.ToArray();
        }

        /// <summary>
        /// Return a list of peers
        /// </summary>
        public Peer []GetPeers()
        {
            var peers = new Peer[mPeers.Count];
            int i = 0;
            foreach (var peer in mPeers)
                peers[i++] = peer.Value.Clone();
            return peers;
        }

        // Purge peers we haven't heard from for a while
        public void PurgePeers(int purgeTimeExitMs, int purgeTimeLostConnectionMs)
        {
            // Find list of peers to purge
            var now = DateTime.Now;
            var purgeKeys = new List<string>();
            foreach (var peer in mPeers)
            {
                var timeMs = (now - peer.Value.TimeReceived).TotalMilliseconds;
                if (peer.Value.Info.State == State.Exit && timeMs >= purgeTimeExitMs || timeMs >= purgeTimeLostConnectionMs)
                    purgeKeys.Add(peer.Key);
            }
            // Purge peers
            foreach (var peerKey in purgeKeys)
            {
                Peer peer = mPeers[peerKey];
                mPeers.Remove(peerKey);
                PeerRemoved?.Invoke(peer);
            }
        }

        /// <summary>
        /// Call on the GUI thread about once every 100 milliseconds
        /// to process beacon packets and broadcast our presense
        /// </summary>
        public void Update()
        {
            if (mSocketUnicast == null || mSocketBroadcast == null)
                return;

            // Receive and respond to broadcast and unicast packets
            while (mSocketBroadcast.Available > 0)
                ProcessResponse(mSocketBroadcast, true);
            while (mSocketUnicast.Available > 0)
                ProcessResponse(mSocketUnicast, false);

            // Periodically broadcast our presense
            var now = DateTime.Now;
            if (now - mBroadcastTime > new TimeSpan(0, 0, 0, 0, BROADCAST_TIME_MS))
            {
                mBroadcastTime = now;

                // Enumerate networks if they have changed
                if (mBroadcastAddresses == null)
                    mBroadcastAddresses = EnumerateNetworks();

                // Send broadcasts to all networks
                foreach (var ip in mBroadcastAddresses)
                {
                    // We do not want a response to this broadcast.
                    // This broadcast is only to notify intervening firewalls that we want to receive
                    // receive data on this port.  The firewall may or may not cooperate.
                    SendResponse(mSocketBroadcast, new IPEndPoint(ip, BROADCAST_PORT), State.Nop);

                    // We want a response to this brodacast
                    SendResponse(mSocketUnicast, new IPEndPoint(ip, BROADCAST_PORT), State.Broadcast);
                }

                // Ping any peers that are not broadcasting
                foreach (var peer in mPeers)
                    if (now - peer.Value.TimeReceived > new TimeSpan(0, 0, 0, 0, BROADCAST_DETECT_MS))
                        SendResponse(mSocketUnicast, peer.Value.EndPoint, State.Ping);
            }
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            mBroadcastAddresses = null;
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            mBroadcastAddresses = null;
        }

        List<IPAddress> EnumerateNetworks()
        {
            // Find network broadcast addresses without duplicates
            var uniqueAddresses = new Dictionary<string, IPAddress>();
            foreach (var nif in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nif.IsReceiveOnly || nif.OperationalStatus != OperationalStatus.Up)
                    continue;
                foreach (var unicast in nif.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address))
                        continue;

                    // Create broadcast address
                    var addressBytes = unicast.Address.GetAddressBytes();
                    byte []maskBytes = null;
                    try
                    {
                        maskBytes = unicast.IPv4Mask.GetAddressBytes();
                    }
                    catch
                    {
                        // Mono doesn't support IPv4Mask, so let's use 255.255.255.0, and hope for the best
                        maskBytes = new byte[addressBytes.Length];
                        for (int i = 0; i < addressBytes.Length - 1; i++)
                            maskBytes[i] = 255;
                    }                   
                    // Create broadcast address
                    for (int i = 0; i < maskBytes.Length; i++)
                        addressBytes[i] = (byte)(addressBytes[i] & maskBytes[i] | ~maskBytes[i]);
                    var broadcastIp = new IPAddress(addressBytes);
                    uniqueAddresses[broadcastIp.ToString()] = broadcastIp;
                }
            }
            // Create list of addresses
            var addresses = new List<IPAddress>();
            foreach (var address in uniqueAddresses)
                addresses.Add(address.Value);
            return addresses;
        }

        void ProcessResponse(Socket socket, bool broadcastSocket)
        {
            if (mReadBuffer.Length < socket.Available)
                mReadBuffer = new byte[(int)(1.2f*socket.Available)];
            byte[] packet = mReadBuffer;

            EndPoint remoteEp = new IPEndPoint(0, 0);
            int length = 0;
            try
            {
                length = socket.ReceiveFrom(packet, ref remoteEp);
            }
            catch
            {
                // The remote computer can forcibly close the port, then we get the exception here
                return;
            }

            // Verify packet header is correct
            var ep = (IPEndPoint)remoteEp;
            if (ep.AddressFamily != AddressFamily.InterNetwork
                    || packet.Length < mHeader.Length 
                    || length < mHeader.Length)
                return;
            for (int i = 0; i < mHeader.Length; i++)
                if (mHeader[i] != packet[i])
                    return;

            // Decode the beacon packet
            Info info;
            try
            {
                var ms = new MemoryStream(packet, mHeader.Length, length - mHeader.Length);
                info = (Info)mJsonSerializer.Deserialize(new StreamReader(ms), mInfo.GetType());
                if (info == null)
                    throw new Exception("Invalid JSON received");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error decoding becon packet: " + ex.Message);
                return;
            }

            // Packet must contain a source ID to be valid
            if (info.SourceId == "")
                return;

            // Keep different entries for the same application even if the
            // packets come from different IP addresses, which is possible
            // when connected to multiple networks.
            // TBD: Maybe it would be better to have one entry per application
            //      with a list of IP addresses instead.
            string key = info.SourceId + ", " + ep.Address.ToString();
            Peer peer;
            if (!mPeers.TryGetValue(key, out peer))
            {
                // Collect first contact info, even if NOP
                peer = new Peer(key);
                mPeers[key] = peer;
                peer.Info = info; 
                peer.EndPoint = ep;
                peer.ThisBeacon = info.SourceId == mSourceId;
                peer.TimeReceived = DateTime.Now;
                PeerAdded?.Invoke(peer);
            }

            // Save peer info (not as much if it's a NOP)
            peer.ThisBeacon = info.SourceId == mSourceId;
            peer.TimeReceived = DateTime.Now;
            if (info.State == State.Nop)
                return;
            peer.Info = info;
            peer.EndPoint = ep;

            // Send response to establish a connection
            if (info.State == State.Broadcast && !peer.ConnectionEstablished)
                SendResponse(mSocketUnicast, ep, State.Ping);

            // Do not allow P2P on the broadcast port
            if (broadcastSocket)
                return;

            // Continue conversation on unicast socket
            if (info.State == State.Ping)
                SendResponse(mSocketUnicast, ep, State.Pong);

            if (info.State == State.Pong)
                SendResponse(mSocketUnicast, ep, State.Gong);

            if ((info.State == State.Pong || info.State == State.Gong) && !peer.ConnectionEstablished)
            {
                peer.ConnectionEstablished = true;
                PeerConnectionEstablishedChanged?.Invoke(peer);
            }
            if (info.State == State.Exit && peer.ConnectionEstablished)
            {
                peer.ConnectionEstablished = false;
                PeerConnectionEstablishedChanged?.Invoke(peer);
            }
        }

        void SendResponse(Socket socket, IPEndPoint ep, State state)
        {
            try
            {
                // Set beacon info
                mInfo.State = state;
                mInfo.SourceId = mSourceId;
                mInfo.SourcePort = ((IPEndPoint)socket.LocalEndPoint).Port;
                mInfo.YourPort = ep.Port;

                // Serialize to JSON
                mWriteBuffer.Position = 0;
                mWriteBuffer.SetLength(0);
                var sw = new StreamWriter(mWriteBuffer);
                sw.Write(mHeaderStr);
                mJsonSerializer.Serialize(sw, mInfo);
                sw.Flush();

                socket.SendTo(mWriteBuffer.GetBuffer(), 0, (int)mWriteBuffer.Length, SocketFlags.None, ep);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error sending broadcast: " + ex.Message);
            }
        }

        public enum State
        {
            Nop,        // Broadcast I am here, please do not respond (I'm just sayin')
            Broadcast,  // Broadcast I am here, please ping me if communications not established
            Ping,       // Please respond with Pong
            Pong,       // Please respond with Gong
            Gong,       // Bi-directional communications established (or keep alive)
            Exit
        }

        /// <summary>
        /// Serialized peer info.  Do not set these parameters.  
        /// Inherit from this class and set parameters there instead.
        /// </summary>
        public class Info
        {
            // These are overwritten by the beacon
            public State State;
            public string SourceId = "";
            public int SourcePort;
            public int YourPort;
        }

        /// <summary>
        /// Non-serialized peer info.
        /// </summary>
        public class Peer
        {
            string mKey;

            public string Key { get { return mKey; } }
            public Info Info;
            public IPEndPoint EndPoint;
            public DateTime TimeReceived;
            public bool ThisBeacon; // Set to true if this is a packet from this beacon
            public bool ConnectionEstablished;  // P2P bidirectional communications established

            public Peer(string key)
            {
                mKey = key;
            }

            /// <summary>
            /// If the port number we receive doesn't match the port number reported by the
            /// remote computer, then we are using NAT and probably won't be able to communicate
            /// </summary>
            public bool UsingNat
            {
                get { return EndPoint.Port != Info.SourcePort; }
            }

            public Peer Clone()
            {
                return (Peer)MemberwiseClone();
            }
        }
    }
}
