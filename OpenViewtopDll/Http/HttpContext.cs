using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Net;

namespace Gosub.Http
{
    public class HttpContext
    {
        public const int HTTP_HEADER_MAX_SIZE = 16000;

        TcpClient mTcpClient;
        HttpReader mReader;
        HttpWriter mWriter;
        HttpRequest mRequest;
        HttpResponse mResponse;
        WebSocket mWebSocket;
        public bool IsSecure { get; }

        public HttpRequest Request => mRequest;
        public HttpResponse Response => mResponse;
        public EndPoint RemoteEndPoint => mTcpClient.Client.RemoteEndPoint;
        public EndPoint LocalEndPoint => mTcpClient.Client.LocalEndPoint;

        /// <summary>
        /// Created only by  HttpServer
        /// </summary>
        internal HttpContext(TcpClient client, HttpReader reader, HttpWriter writer, bool isSecure)
        {
            mTcpClient = client;
            mReader = reader;
            mWriter = writer;
            IsSecure = isSecure;
        }

        /// <summary>
        /// Called only by HttpServer, once per request
        /// </summary>
        internal void ResetRequestInternal(HttpRequest request, HttpResponse response)
        {
            mRequest = request;
            mResponse = response;
        }


        /// <summary>
        /// Set the HTTP response header and return the input stream.  
        /// The response header may not be modified after calling this function.
        /// NOTE: If you have not set Response.ContentLength, this will set it to zero
        /// </summary>
        public HttpReader GetReader()
        {
            if (!mResponse.HeaderSent)
            {
                mResponse.ContentLength = Math.Max(0, mResponse.ContentLength);
                SetHttpHeader();
            }
            return mReader;
        }

        /// <summary>
        /// Set the HTTP response header and return the output stream.
        /// The response header may not be modified after calling this function.
        /// </summary>
        public HttpWriter GetWriter(long contentLength)
        {
            if (!mResponse.HeaderSent)
            {
                if (contentLength < 0)
                    throw new HttpException(500, "GetWriter: ContentLength must be >= zero");
                mResponse.ContentLength = contentLength;
                SetHttpHeader();
            }
            if (contentLength !=  mResponse.ContentLength)
                throw new HttpException(500, "GetWriter: ContentLength cannot be changed once it is set");
            return mWriter;
        }

        /// <summary>
        /// Set the HTTP response header and return the web socket.
        /// The response header may not be modified after calling this function.
        /// </summary>
        public WebSocket AcceptWebSocket(string protocol)
        {
            if (!mRequest.IsWebSocketRequest)
                throw new HttpException(500, "Websocket not allowed to accept a non-web socket request");
            if (mResponse.HeaderSent)
                throw new HttpException(500, "Websocket cannot accept connection after http header was already sent");
            if (mWebSocket != null)
                throw new HttpException(500, "Websocket connection was already accepted");

            mTcpClient.NoDelay = true;
            mWebSocket = new WebSocket(this, protocol);
            return mWebSocket;
        }

        /// <summary>
        /// Set the response header before the stream is read or written
        /// </summary>
        void SetHttpHeader()
        {
            if (mResponse.HeaderSent)
                throw new HttpException(500, "SendHttpHeader: Http header already sent");
            if (mResponse.ContentLength < 0)
                throw new HttpException(500, "SendHttpHeader: ConentLength must be set before sending header");

            // Connection: Close or keep-alive
            if (mResponse.Connection == "")
            {
                mResponse.Connection = "close";
                var connection = mRequest.Headers["connection"].ToLower();
                if (connection == "keep-alive" || mRequest.ProtocolVersionMinor >= 1 && connection != "close")
                    mResponse.Connection = "keep-alive";
            }

            // Good to go, reset streams
            var header = Encoding.UTF8.GetBytes(Response.Generate());
            mReader.PositionInternal = 0;
            mReader.LengthInternal = Math.Max(0, mRequest.ContentLength);
            mWriter.PositionInternal = -header.Length; // Write position measures content output length
            mWriter.LengthInternal = Math.Max(0, mResponse.ContentLength);

            // Freeze HTTP response and send header
            mResponse.HeaderSent = true;
            mWriter.SetPreWriteTaskInternal(mWriter.WriteAsync(header));
        }

        public async Task SendResponseAsync(byte []message)
        {
            mResponse.ContentLength = message.Length;
            await GetWriter(message.Length).WriteAsync(message, 0, message.Length);
        }

        public async Task SendResponseAsync(string message)
        {
            await SendResponseAsync(Encoding.UTF8.GetBytes(message));
        }

        public async Task SendResponseAsync(string message, int statusCode)
        {
            mResponse.StatusCode = statusCode;
            await SendResponseAsync(message);
        }

        async public Task SendFileAsync(string path)
        {
            if (!File.Exists(path))
            {
                Log.Write("FILE NOT FOUND: " + path);
                await SendResponseAsync("File not found: " + path, 404);
                return;
            }
            using (var stream = File.OpenRead(path))
                await GetWriter(stream.Length).WriteAsync(stream);
        }

        async public Task<byte[]> ReadContentAsync(int maxLength)
        {
            // Currently we require the sender to include 'Content-Length.
            // TBD: Fix this since it is not required by the HTTP protocol
            if (mRequest.ContentLength < 0)
                throw new HttpException(411, "HTTP header did not contain 'Content-Length' which is required");
            if (mRequest.ContentLength > maxLength)
                throw new HttpException(413, "Content length is too large");

            // Read specific number of bytes (TBD: Enforce timeout)
            var stream = GetReader();
            var buffer = new byte[mRequest.ContentLength];
            int index = 0;
            while (index < buffer.Length)
                index += await stream.ReadAsync(buffer, index, buffer.Length - index);
            return buffer;
        }

    }
}
