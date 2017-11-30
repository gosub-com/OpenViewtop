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
        const int HTTP_HEADER_MAX_SIZE = 16000;
        const string CRLF = "\r\n";

        TcpClient mTcpClient;
        HttpReader mReader;
        HttpWriter mWriter;
        HttpRequest mRequest;
        HttpResponse mResponse;
        WebSocket mWebSocket;
        public bool IsSecure { get; }

        static Dictionary<string, bool> mMethods = new Dictionary<string, bool>()
        {
            { "GET", true },
            { "HEAD", true },
            { "POST", true },
            { "PUT", true },
            { "DELETE", true },
            { "OPTIONS", true },
            { "CONNECT", true },
            { "TRACE", true }
        };

        /// <summary>
        /// Created only by the HttpServer
        /// </summary>
        internal HttpContext(TcpClient client, HttpReader reader, HttpWriter writer, bool isSecure)
        {
            mTcpClient = client;
            mReader = reader;
            mWriter = writer;
            IsSecure = isSecure;
        }

        public HttpRequest Request => mRequest;
        public HttpResponse Response => mResponse;
        public EndPoint RemoteEndPoint => mTcpClient.Client.RemoteEndPoint;

        /// <summary>
        /// Set the HTTP response header and return the input stream.  
        /// The response header may not be modified after calling this function.
        /// NOTE: If you have not set Response.ContentLength, this will set it to zero
        /// </summary>
        public HttpReader GetReader()
        {
            if (mRequest.IsWebSocketRequest)
                throw new HttpException(500, "GetReader: Not allowed to get an http reader on a websocket connection");

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
            if (mRequest.IsWebSocketRequest)
                throw new HttpException(500, "GetWriter: Not allowed to get an http writer on a websocket connection");

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
                throw new HttpException(404, "Websocket connection was already accepted");

            mTcpClient.NoDelay = true;
            mWebSocket = new WebSocket(this, mReader, mWriter, protocol);
            return mWebSocket;
        }


        /// <summary>
        /// Called only by HttpServer
        /// Read and parse the HTTP header, return true if everything worked.
        /// Returns false at EOF.  Throw an exception on error.
        /// </summary>
        async internal Task<bool> ReadHttpHeaderAsyncInternal()
        {
            mRequest = new HttpRequest();
            mRequest.Headers = new HttpQuery();
            mRequest.ReceiveDate = DateTime.Now;

            mResponse = new HttpResponse();
            mReader.PositionInternal = 0;
            mReader.LengthInternal = HTTP_HEADER_MAX_SIZE;

            var buffer = await mReader.ReadHttpHeaderAsyncInternal();
            if (buffer.Count == 0)
                return false;

            var header = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length == 0)
                throw new HttpException(400, "Invalid request: Empty");

            var headerParts = header[0].Split(' ');
            if (headerParts.Length != 3)
                throw new HttpException(400, "Invalid request line: Needs 3 parts separated by space");

            // Parse method
            mRequest.HttpMethod = headerParts[0];
            if (!mMethods.ContainsKey(mRequest.HttpMethod))
                throw new HttpException(400, "Invalid request line: unknown method");

            // Parse URL Fragment
            var target = headerParts[1];
            mRequest.TargetFull = target;
            int fragmentIndex = target.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                mRequest.Fragment = target.Substring(fragmentIndex+1);
                target = target.Substring(0, fragmentIndex);
            }
            // Parse URL query string
            int queryIndex = target.IndexOf('?');
            if (queryIndex >= 0)
            {
                var query = target.Substring(queryIndex + 1);
                mRequest.QueryFull = query;
                target = target.Substring(0, queryIndex);
                mRequest.Query = ParseQueryString(query);
            }
            else
            {
                mRequest.Query = new HttpQuery();
            }
            mRequest.Target = target;

            // Parse protocol and version
            var protocolParts = headerParts[2].Split('/');
            if (protocolParts.Length != 2 || protocolParts[0].ToUpper() != "HTTP")
                throw new HttpException(400, "Invalid request line: Unrecognized protocol.  Only HTTP is supported");

            var versionParts = protocolParts[1].Split('.');
            if (versionParts.Length != 2
                    || !int.TryParse(versionParts[0], out mRequest.ProtocolVersionMajor)
                    || !int.TryParse(versionParts[1], out mRequest.ProtocolVersionMinor))
                throw new HttpException(400, "Invalid request line: Protocol version format is incorrect (require #.#)");

            if (mRequest.ProtocolVersionMajor != 1)
                throw new HttpException(400, "Expecting HTTP version 1.#");

            // Read header fields
            var headers = mRequest.Headers;
            for (int lineIndex = 1; lineIndex < header.Length; lineIndex++)
            {
                var fieldLine = header[lineIndex];

                int index;
                if ((index = fieldLine.IndexOf(':')) < 0)
                    throw new HttpException(400, "Invalid header field: Missing ':'");

                var key = fieldLine.Substring(0, index).Trim().ToLower();
                var value = fieldLine.Substring(index + 1).Trim();
                if (key == "" || value == "")
                    throw new HttpException(400, "Invalid header field: Missing key or value");

                headers[key] = value;
            }

            // Parse well known header fields
            mRequest.Headers = headers;
            mRequest.Host = headers.Get("host");
            mRequest.ContentLength = -1; // Default if not sent
            if (long.TryParse(headers.Get("content-length"), out long contentLength))
                mRequest.ContentLength = contentLength;

            // Websocket connection?  RFC 6455, 4.2.1
            if (headers.Get("connection").ToLower().Contains("upgrade")
                && headers.Get("upgrade").ToLower() == "websocket")
            {
                int.TryParse(headers.Get("sec-websocket-version"), out int webSocketVersion);
                if (webSocketVersion < 13)
                    throw new HttpException(400, "Web socket request version must be >= 13");
                mRequest.IsWebSocketRequest = true;
            }
            return true;
        }

        public static HttpQuery ParseQueryString(string query)
        {
            var queryStrings = new HttpQuery();
            foreach (var q in query.Split('&'))
            {
                int keyIndex = q.IndexOf('=');
                if (keyIndex >= 0)
                    queryStrings[q.Substring(0, keyIndex)] = q.Substring(keyIndex + 1);
                else if (q != "")
                    queryStrings[q] = "";
            }
            return queryStrings;
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
            if (mRequest.IsWebSocketRequest)
                throw new HttpException(500, "SendHttpHeader: Not allowed to send http header on websocket connection");

            // Keep alive
            var connection = mRequest.Headers.Get("connection").ToLower();
            bool keepAlive = connection == "keep-alive" || mRequest.ProtocolVersionMinor >= 1 && connection != "close";
            Response.KeepAlive = keepAlive;

            // Status message
            var statusMessage = mResponse.StatusMessage.Replace('\r', ' ').Replace('\n', ' ');
            if (mResponse.StatusCode != 200 && statusMessage == "OK")
                statusMessage = "?";

            // Generate HTTP header
            var header = Encoding.UTF8.GetBytes(
                "HTTP/1.1 " + mResponse.StatusCode + " " + statusMessage + CRLF
                + "Content-Length:" + mResponse.ContentLength + CRLF
                + "Connection:" + (keepAlive ? "keep-alive" : "close") + CRLF
                + (mResponse.ContentType == "" ? "" : "Content-Type:" + mResponse.ContentType + CRLF)
                + CRLF);

            // Good to go, reset streams
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
                throw new HttpException(404, "File not found", true);
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
