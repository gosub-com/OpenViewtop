﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;

namespace Gosub.Http
{
    /// <summary>
    /// This is an HTTP stream, which will have the request header when you receive it.
    /// Fill out the response before reading or writing to the stream.  The response
    /// header cannot be changed after reading or writing the stream.
    /// You do not have to close/dispose the stream, but you may if you want.
    /// 
    /// TBD: Set ContentLength to reuse the TCP connection?
    /// </summary>
    public class HttpStream : Stream
    {
        Stream mStream;
        HttpRequest mRequest;
        HttpResponse mResponse;

        bool mHeaderSent;
        long mBytesWritten;
        long mBytesRead;
        Encoding mUtf8NoBom = new UTF8Encoding(false); // Necessary because Encoding.UTF8 writes BOM

        Dictionary<string, bool> mMethods = new Dictionary<string, bool>()
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

        public HttpRequest Request => mRequest;
        public HttpResponse Response => mResponse;
        public long BytesWritten => mBytesWritten;
        public long BytesRead => mBytesRead;

        /// <summary>
        /// Created only by the HttpServer
        /// </summary>
        internal HttpStream(Stream stream)
        {
            mStream = stream;
        }

        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override void SetLength(long value) { throw new NotImplementedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }
        public override bool CanTimeout => true;
        public override int ReadTimeout { get => mStream.ReadTimeout; set => mStream.ReadTimeout = value; }
        public override int WriteTimeout { get => mStream.WriteTimeout; set => mStream.WriteTimeout = value; }

        public override void Flush()
        {
            mStream.Flush();
        }

        public override void Close()
        {
            // We do not close this stream, the web server will do that
            mStream.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            // We do not dispose this stream, the web server will do that
            mStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!mHeaderSent)
                SendHeader();
            var length = mStream.Read(buffer, offset, count);
            mBytesRead += length;
            return length;
        }

        public override int ReadByte()
        {
            if (!mHeaderSent)
                SendHeader();
            int b = mStream.ReadByte();
            if (b >= 0)
                mBytesRead++;
            return b;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!mHeaderSent)
                SendHeader();
            mBytesWritten += count;
            mStream.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            if (!mHeaderSent)
                SendHeader();
            mBytesWritten++;
            mStream.WriteByte(value);
        }

        /// <summary>
        /// Called only by HttpServer
        /// Read and parse the HTTP header, return true if everything worked.
        /// On failure, a response was alreeady sent back to the client.
        /// </summary>
        internal void ParseHeader()
        {
            mRequest = new HttpRequest();
            mResponse = new HttpResponse();
            mRequest.ReceiveDate = DateTime.Now;
            mHeaderSent = false;
            mBytesWritten = 0;
            mBytesRead = 0;

            // Parse header
            var headerParts = ReadLine().Split(' ');
            if (headerParts.Length != 3)
                throw new HttpException(400, "Invalid request line: Needs 3 parts separated by space", true);

            // Parse method
            mRequest.HttpMethod = headerParts[0];
            if (!mMethods.ContainsKey(mRequest.HttpMethod))
                throw new HttpException(400, "Invalid request line: unknown method", true);

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
            var queryStrings = new HttpRequest.QueryDict();
            int queryIndex = target.IndexOf('?');
            if (queryIndex >= 0)
            {
                var query = target.Substring(queryIndex + 1);
                mRequest.QueryFull = query;
                target = target.Substring(0, queryIndex);
                foreach (var q in query.Split('&'))
                {
                    int keyIndex = q.IndexOf('=');
                    if (keyIndex >= 0)
                        queryStrings[q.Substring(0, keyIndex)] = q.Substring(keyIndex + 1);
                    else if (q != "")
                        queryStrings[q] = "";
                }
            }
            mRequest.Query = queryStrings;
            mRequest.Target = target;

            // Parse protocol and version
            var protocolParts = headerParts[2].Split('/');
            if (protocolParts.Length != 2 || protocolParts[0].ToUpper() != "HTTP")
                throw new HttpException(400, "Invalid request line: Unrecognized protocol.  Only HTTP is supported", true);

            var versionParts = protocolParts[1].Split('.');
            if (versionParts.Length != 2
                    || !int.TryParse(versionParts[0], out mRequest.ProtocolVersionMajor)
                    || !int.TryParse(versionParts[1], out mRequest.ProtocolVersionMinor))
                throw new HttpException(400, "Invalid request line: Protocol version format is incorrect (require #.#)", true);

            if (mRequest.ProtocolVersionMajor != 1)
                throw new HttpException(400, "Expecting HTTP version 1.#", true);

            // Read header fields
            var headers = new HttpRequest.QueryDict();
            string fieldLine;
            while ((fieldLine = ReadLine()) != "")
            {
                int index;
                if ((index = fieldLine.IndexOf(':')) < 0)
                    throw new HttpException(400, "Invalid header field: Missing ':'", true);

                var key = fieldLine.Substring(0, index).Trim().ToLower();
                var value = fieldLine.Substring(index + 1).Trim();
                if (key == "" || value == "")
                    throw new HttpException(400, "Invalid header field: Missing key or value", true);

                headers[key] = value;
            }

            // Parse well known header fields
            mRequest.Headers = headers;
            mRequest.Host = headers.Get("host");
            mRequest.ContentLength = -1; // Default if not sent
            if (long.TryParse(headers.Get("content-length"), out long contentLength))
                mRequest.ContentLength = contentLength;
            bool keepAlive = mRequest.ProtocolVersionMinor >= 1 && mRequest.Headers.Get("connection").ToLower() != "close"
                || mRequest.Headers.Get("connection").ToLower() == "keep-alive";
            mRequest.KeepAlive = keepAlive;
            mResponse.KeepAlive = keepAlive;
        }

        /// <summary>
        /// Read until there is a <CR><LF> as defined in RFC 7230.
        /// </summary>
        string ReadLine()
        {
            // NOTE: Improve performance using block reads to a buffer when we get a chance
            StringBuilder sb = new StringBuilder();
            int ch = mStream.ReadByte();
            while (ch != '\r')
            {
                if (ch < 0 )
                    throw new HttpException(400, "ReadHeaderLine: Expecting <CR>, found <EOF>", true);
                if (ch == '\n')
                    throw new HttpException(400, "ReadHeaderLine: Expecting <CR>, found <LF>", true);
                sb.Append((char)ch);
                ch = mStream.ReadByte();
            }
            if ( (ch = mStream.ReadByte()) != '\n')
                throw new HttpException(400, "ReadHeaderLine: Expecting <LF>, but found value char value " + ch, true);

            return sb.ToString();
        }

        /// <summary>
        /// Send the response header when the stream is read or written
        /// </summary>
        void SendHeader()
        {
            if (mHeaderSent)
                return;
            if (mResponse.ContentLength < 0)
                throw new HttpException(500, "Response requires ContentLength", true);

            // Freeze header
            mHeaderSent = true;
            mResponse.HeaderSent = true;

            // Check status messahe
            var statusMessage = mResponse.StatusMessage.Replace('\r', ' ').Replace('\n', ' ');
            if (mResponse.StatusCode != 200 && statusMessage == "OK")
                statusMessage = "?";

            // Send message
            var sw = new StreamWriter(mStream, mUtf8NoBom, 1024, true);
            sw.WriteLine("HTTP/1.1 " + mResponse.StatusCode + " " + statusMessage);
            sw.WriteLine("Content-Length:" + mResponse.ContentLength);
            if (!Response.KeepAlive)
                sw.WriteLine("Connection:close");
            if (Response.KeepAlive && Request.ProtocolVersionMinor == 0)
                sw.WriteLine("Connection:keep-alive");
            sw.WriteLine("");
            sw.Dispose();
        }

        public void SendResponse(byte []message)
        {
            mResponse.ContentLength = message.Length;
            Write(message, 0, message.Length);
        }

        public void SendResponse(string message)
        {
            SendResponse(mUtf8NoBom.GetBytes(message));
        }

        public void SendResponse(string message, int statusCode)
        {
            mResponse.StatusCode = statusCode;
            SendResponse(message);
        }

        public void SendFile(string path)
        {
            var stream = File.OpenRead(path);
            mResponse.ContentLength = stream.Length;
            stream.CopyTo(this);
            stream.Close();
        }

        public byte[] ReadContent(int maxLength)
        {
            // Currently we require the sender to include 'Content-Length.
            // TBD: Fix this since it is not required by the HTTP protocol,
            //      we get away with it because major browsers always include it
            if (mRequest.ContentLength < 0)
                throw new HttpException(411, "Error: HTTP header did not contain 'Content-Length' which is currently required", true);
            if (mRequest.ContentLength > maxLength)
                throw new HttpException(413, "Error: Content length is too large", true);

            // Read specific number of bytes (TBD: Enforce timeout)
            var buffer = new byte[mRequest.ContentLength];
            int index = 0;
            while (index < buffer.Length)
                index += Read(buffer, index, buffer.Length - index);
            return buffer;
        }

    }
}
