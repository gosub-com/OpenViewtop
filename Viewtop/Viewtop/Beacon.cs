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
        const int BROADCAST_TIME_MS = 1750;

        byte[] mHeader;
        string mHeaderStr;
        string mGuid = Guid.NewGuid().ToString();
        Info mInfo;
        Dictionary<string, Peer> mPeers = new Dictionary<string, Peer>();

        Socket mSocketBroadcast; // Receive broadcasts only (no transmit)
        Socket mSocketUnicast;   // Transmit broadcasts and receive direct responses

        DateTime mBroadcastTime;
        List<IPAddress> mBroadcastAddresses;

        JsonSerializer mJsonSerializer = new JsonSerializer();
        MemoryStream mWriteBuffer = new MemoryStream();
        byte[] mReadBuffer = new byte[0];

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
            var now = DateTime.Now;
            var purgeKeys = new List<string>();
            foreach (var peer in mPeers)
            {
                var timeMs = (now - peer.Value.TimeReceived).TotalMilliseconds;
                if (peer.Value.Info.State == State.Exit && timeMs >= purgeTimeExitMs || timeMs >= purgeTimeLostConnectionMs)
                    purgeKeys.Add(peer.Key);
            }
            foreach (var peerKey in purgeKeys)
                mPeers.Remove(peerKey);
        }


        /// <summary>
        /// Call on the GUI thread to process beacon packets and broadcast our presense
        /// </summary>
        public void Update()
        {
            if (mSocketUnicast == null || mSocketBroadcast == null)
                return;

            // Receive and respond to broadcast and unicast packets
            while (mSocketBroadcast.Available > 0)
                ProcessResponse(mSocketBroadcast);
            while (mSocketUnicast.Available > 0)
                ProcessResponse(mSocketUnicast);

            // Periodically broadcast our presense        
            if (DateTime.Now - mBroadcastTime > new TimeSpan(0, 0, 0, 0, BROADCAST_TIME_MS))
            {
                // Enumerate networks if they have changed
                if (mBroadcastAddresses == null)
                    mBroadcastAddresses = EnumerateNetworks();

                // We do not want a response to this broadcast.
                // This broadcast is only to notify intervening firewalls that we want to receive
                // receive data on this port.  The firewall may or may not cooperate, especially
                // since we are expecting the response from a different port (i.e. the unicast port)
                SendResponse(mSocketBroadcast, new IPEndPoint(IPAddress.Parse("192.168.1.255"), BROADCAST_PORT), State.Nop);

                // We want a response to this brodacast
                SendResponse(mSocketUnicast, new IPEndPoint(IPAddress.Parse("192.168.1.255"), BROADCAST_PORT), State.Broadcast);
                mBroadcastTime = DateTime.Now;
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
            Dictionary<long, bool> uniqueAddresses = new Dictionary<long, bool>();
            foreach (var nif in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nif.IsReceiveOnly || nif.OperationalStatus != OperationalStatus.Up)
                    continue;
                foreach (var unicast in nif.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address))
                        continue;
                    long mask = unicast.IPv4Mask.Address;
                    long address = ((unicast.Address.Address & mask) | ~mask) & 0xFFFFFFFFL;
                    uniqueAddresses[address] = true;
                }
            }
            // Create list of addresses
            var addresses = new List<IPAddress>();
            foreach (var address in uniqueAddresses)
                addresses.Add(new IPAddress(address.Key));
            return addresses;
        }

        void ProcessResponse(Socket socket)
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

            // Keep different entries for the same application even if the
            // packets come from different IP addresses, which is possible
            // when connected to multiple networks.
            // TBD: Maybe it would be better to have one entry per application
            //      with a list of IP addresses instead.
            string key = info.Guid + ", " + ep.Address.ToString();
            Peer peer;
            if (!mPeers.TryGetValue(key, out peer))
            {
                peer = new Peer();
                mPeers[key] = peer;

                // Collect info even if NOP
                peer.Info = info; 
                peer.EndPoint = ep;
            }

            // Do almost nothing with this broadcast if it's a nop
            peer.ThisBeacon = info.Guid == mGuid;
            peer.TimeReceived = DateTime.Now;
            if (info.State == State.Nop)
                return;

            peer.Info = info;
            peer.EndPoint = ep;

            // Send responses to establish a connection
            if (info.State == State.Broadcast && !peer.ConnectionEstablished)
                SendResponse(mSocketUnicast, ep, State.Ping);
            if (info.State == State.Ping)
                SendResponse(mSocketUnicast, ep, State.Pong);
            if (info.State == State.Pong)
                SendResponse(mSocketUnicast, ep, State.Gong);
            if (info.State == State.Pong || info.State == State.Gong)
                peer.ConnectionEstablished = true;
            if (info.State == State.Exit)
                peer.ConnectionEstablished = false;
        }

        void SendResponse(Socket socket, IPEndPoint ep, State state)
        {
            try
            {
                // Set beacon info
                mInfo.Guid = mGuid;
                mInfo.State = state;
                mInfo.MyPort = ((IPEndPoint)socket.LocalEndPoint).Port;
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
            // Type and Guid are always overwritten by the beacon
            public State State;
            public string Guid = "";
            public int MyPort;
            public int YourPort;
        }

        /// <summary>
        /// Non-serialized peer info.
        /// </summary>
        public class Peer
        {
            public Info Info;
            public IPEndPoint EndPoint;
            public DateTime TimeReceived;
            public bool ThisBeacon; // Set to true if this is a packet from this beacon
            public bool ConnectionEstablished;  // P2P bidirectional communications established

            /// <summary>
            /// If the port number we receive doesn't match the port number reported by the
            /// remote computer, then we are using NAT and probably won't be able to communicate
            /// </summary>
            public bool UsingNat
            {
                get { return EndPoint.Port != Info.MyPort; }
            }

            public Peer Clone()
            {
                return (Peer)MemberwiseClone();
            }
        }
    }
}
