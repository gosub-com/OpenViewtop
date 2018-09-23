using System;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Gosub.Http
{
    /// <summary>
    /// RFC 6455: Implement websocket protocol
    /// </summary>
    public class WebSocket
    {
        const string CRLF = "\r\n";
        const int READ_BUFFER_SIZE = 2048;

        HttpContext mContext;
        HttpReader mReader;
        HttpWriter mWriter;
        WebSocketState mState;

        byte[] mReadHeaderBuffer = new byte[16];
        byte[] mReadBuffer;
        int    mReadFrameLength;
        int    mReadFrameOffset;
        bool   mReadFinal;
        byte[] mReadMask = new byte[4];
        int    mReadMaskIndex;
        WebSocketMessageType mReadMessageType = WebSocketMessageType.Close;
        byte[] mWriteHeaderBuffer = new byte[16];

        public bool PingReceived { get; set; }
        public bool PongReceived { get; set; }
        public WebSocketState State => mState;
        public HttpRequest Request => mContext.Request;

        /// <summary>
        /// Maximum message size allowed to be received (default 64Kb)
        /// </summary>
        public int MaxReceiveSize { get; set; } = 65536;

        enum OpCode
        {
            Continue = 0,
            Text = 1,
            Binary = 2,
            Close = 8,
            Ping = 9,
            Pong = 10
        }

        /// <summary>
        /// Only called by HttpContext
        /// </summary>
        internal WebSocket(HttpContext context, string protocol)
        {
            mState = WebSocketState.Open;
            mContext = context;

            var request = context.Request;
            var requestProtocol = request.Headers["sec-websocket-protocol"]; // TBD: Split "," separated list
            if (protocol != requestProtocol)
                throw new HttpException(400, "Invalid websocket protocol.  Client requested '" + requestProtocol + "', server accepted '" + protocol + "'");

            // RFC 6455, 4.2.1 and 4.2.2
            string key = request.Headers["sec-websocket-key"];
            if (key == "")
                throw new HttpException(400, "Websocket key not sent by client");
            string keyHash;
            using (var sha = SHA1.Create())
                keyHash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

            var response = mContext.Response;
            response.StatusCode = 101;
            response.StatusMessage = "Switching Protocols";
            response.Connection = "Upgrade";
            response.Headers["upgrade"] = "websocket";
            response.Headers["Sec-WebSocket-Accept"] = keyHash;
            response.Headers["Sec-WebSocket-Protocol"] = protocol;
            request.ContentLength = long.MaxValue; // Stream never ends
            mWriter = mContext.GetWriter(long.MaxValue);
            mReader = mContext.GetReader();
        }

        /// <summary>
        /// Clear the stream, then read the entire message.
        /// Exception thrown if message is longer than MaxReceiveSize (default 64Kb).
        /// When Close is returned, the stream holds the close status description.
        /// </summary>
        public async Task<WebSocketMessageType> ReceiveAsync(MemoryStream stream, CancellationToken cancellationToken)
        {
            if (mReadBuffer == null)
                mReadBuffer = new byte[READ_BUFFER_SIZE];
            stream.SetLength(0);
            stream.Position = 0;
            ArraySegment<byte> bufferSeg = new ArraySegment<byte>(mReadBuffer);
            WebSocketReceiveResult result;
            int messageLength = 0;
            do
            {
                result = await ReceiveAsync(bufferSeg, cancellationToken);
                messageLength += result.Count;

                // Check for close message
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    stream.SetLength(0);
                    byte[] message = Encoding.UTF8.GetBytes(result.CloseStatusDescription);
                    stream.Write(message, 0, message.Length);
                    return WebSocketMessageType.Close;
                }

                if (messageLength > MaxReceiveSize)
                {
                    // Eat rest of the message
                    while (!result.EndOfMessage && !cancellationToken.IsCancellationRequested)
                        result = await ReceiveAsync(bufferSeg, cancellationToken);
                    throw new HttpException(400, "Websocket: Message too long");
                }
                stream.Write(mReadBuffer, 0, result.Count);
                cancellationToken.ThrowIfCancellationRequested();
            } while (!result.EndOfMessage);

            stream.Position = 0;
            return result.MessageType;
        }

        /// <summary>
        /// Low level function to receive part of a message.
        /// The buffer must be at least 128 bytes long so it can hold the close reason
        /// Use EndOfMessage to know when you have the whole message.
        /// NOTE: I recommend using ReceiveAcync(stream) which handles the low level details
        /// </summary>
        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            // Read frame header if necessary
            Debug.Assert(buffer.Count >= 128);
            Debug.Assert(mReadFrameOffset <= mReadFrameLength);
            while (mReadFrameOffset == mReadFrameLength)
            {
                await ReadHeaderAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Read frame
            var count = Math.Min(buffer.Count, mReadFrameLength - mReadFrameOffset);
            if (count > 0)
                count = await mReader.ReadAllAsync(new ArraySegment<byte>(buffer.Array, buffer.Offset, count));
            cancellationToken.ThrowIfCancellationRequested();
            mReadFrameOffset += count;

            // Demask
            for (int i = 0; i < count; i++)
                buffer.Array[buffer.Offset + i] ^= mReadMask[mReadMaskIndex++ & 3];

            // Check for close message
            if (mReadMessageType == WebSocketMessageType.Close)
            {
                if (mReadFrameOffset != mReadFrameLength || !mReadFinal)
                    throw new HttpException(400, "Websocket: Close message cannot be fragmented, must be < 126 bytes, and must be final message");
                if (count < 2)
                    throw new HttpException(400, "Websocket: Close message must contain status");

                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    (WebSocketCloseStatus)(buffer.Array[buffer.Offset] * 256 + buffer.Array[buffer.Offset + 1]),
                    Encoding.UTF8.GetString(buffer.Array, buffer.Offset + 2, count - 2));
            }
            if (mReadMessageType != WebSocketMessageType.Text && mReadMessageType != WebSocketMessageType.Binary)
                throw new HttpException(500, "Websocket message type must be Text or Binary");  // This should never happen

            return new WebSocketReceiveResult(count, mReadMessageType, mReadFrameOffset == mReadFrameLength && mReadFinal);
        }

        /// <summary>
        /// Read frame header, put the info in the mRead variables
        /// </summary>
        async Task ReadHeaderAsync(CancellationToken cancellationToken)
        {
            // Read control bytes
            await mReader.ReadAllAsync(new ArraySegment<byte>(mReadHeaderBuffer, 0, 2));
            byte b = mReadHeaderBuffer[0];
            mReadFinal = (b & 0x80) != 0;
            mReadFrameLength = mReadHeaderBuffer[1] & 0x7F;
            mReadFrameOffset = 0;
            if ((b & 0x70) != 0)
                throw new HttpException(400, "Websocket: Invalid control byte");
            if ((mReadHeaderBuffer[1] & 0x80) == 0)
                throw new HttpException(400, "Websocket: Invalid packet, need masking");

            // Packet length
            int extra = 4;
            if (mReadFrameLength == 126)
                extra += 2;
            else if (mReadFrameLength == 127)
                extra += 8;

            // Read remaining packet header
            await mReader.ReadAllAsync(new ArraySegment<byte>(mReadHeaderBuffer, 0, extra));

            // Read packet length
            int index = 0;
            if (mReadFrameLength == 126)
            {
                index = 2;
                mReadFrameLength = (mReadHeaderBuffer[0] << 8) + mReadHeaderBuffer[1];
            }
            else if (mReadFrameLength == 127)
            {
                index = 8;
                mReadFrameLength = (mReadHeaderBuffer[4] << 24) + (mReadHeaderBuffer[5] << 16) + (mReadHeaderBuffer[6] << 8) + mReadHeaderBuffer[7];
                if (mReadFrameLength < 0 || mReadHeaderBuffer[0] != 0 || mReadHeaderBuffer[1] != 0 || mReadHeaderBuffer[2] != 0 || mReadHeaderBuffer[3] != 0)
                    throw new HttpException(400, "Websocket: Frame too long");
            }
            // Masking
            Array.Copy(mReadHeaderBuffer, index, mReadMask, 0, 4);
            mReadMaskIndex = 0;

            // Parse opcode
            OpCode opCode = (OpCode)(b & 0x0F);
            switch (opCode)
            {
                case OpCode.Continue:
                    if (mReadMessageType == WebSocketMessageType.Close)
                        throw new HttpException(400, "Websocket: Client sent data after connection was closed, or the first packet was not Text or Binary");
                    break;
                case OpCode.Ping:
                    PingReceived = true;
                    break;
                case OpCode.Pong:
                    PongReceived = true;
                    break;
                case OpCode.Close:
                    mReadMessageType = WebSocketMessageType.Close;
                    mState = mState == WebSocketState.CloseSent ? WebSocketState.Closed : WebSocketState.CloseReceived;
                    break;
                case OpCode.Text:
                    mReadMessageType = WebSocketMessageType.Text;
                    break;
                case OpCode.Binary:
                    mReadMessageType = WebSocketMessageType.Binary;
                    break;
                default:
                    throw new HttpException(400, "Websocket: Invalid opcode: " + opCode);
            }
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            if (mState == WebSocketState.Closed || mState == WebSocketState.CloseSent)
                throw new HttpException(500, "Websocket: Sent 'close' message after connection was already closed.  Connection state=" + mState);
            mState = mState == WebSocketState.CloseReceived ? WebSocketState.Closed : WebSocketState.CloseSent;

            // Prepend two bytes for status
            var message = Encoding.UTF8.GetBytes("XX" + statusDescription);
            message[0] = (byte)((int)closeStatus >> 8);
            message[1] = (byte)(int)closeStatus;
            await SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Close, true, cancellationToken);
            await mWriter.FlushAsync();
        }

        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            OpCode opCode;
            if (messageType == WebSocketMessageType.Close)
                opCode = OpCode.Close;
            else if (messageType == WebSocketMessageType.Binary)
                opCode = OpCode.Binary;
            else if (messageType == WebSocketMessageType.Text)
                opCode = OpCode.Text;
            else
                throw new HttpException(500, "SendAsync: Invalid message type");

            int index = 0;
            mWriteHeaderBuffer[0] = (byte)((endOfMessage ? 0x80 : 0) | ((int)opCode & 0x0F));
            if (buffer.Count < 126)
            {
                mWriteHeaderBuffer[1] = (byte)(buffer.Count);
                index = 2;
            }
            else if (buffer.Count < 65536)
            {
                mWriteHeaderBuffer[1] = 126;
                mWriteHeaderBuffer[2] = (byte)(buffer.Count >> 8);
                mWriteHeaderBuffer[3] = (byte)buffer.Count;
                index = 4;
            }
            else
            {
                mWriteHeaderBuffer[1] = 127;
                mWriteHeaderBuffer[2] = 0;
                mWriteHeaderBuffer[3] = 0;
                mWriteHeaderBuffer[4] = 0;
                mWriteHeaderBuffer[5] = 0;
                mWriteHeaderBuffer[6] = (byte)(buffer.Count >> 24);
                mWriteHeaderBuffer[7] = (byte)(buffer.Count >> 16);
                mWriteHeaderBuffer[8] = (byte)(buffer.Count >> 8);
                mWriteHeaderBuffer[9] = (byte)buffer.Count;
                index = 10;
            }
            await mWriter.WriteAsync(mWriteHeaderBuffer, 0, index);
            await mWriter.WriteAsync(buffer.Array, buffer.Offset, buffer.Count);
        }
    }
}
