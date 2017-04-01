using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;

namespace Gosub.Http
{
    public class HttpConext
    {
        const int HTTP_HEADER_MAX_SIZE = 16000;

        HttpRequest mRequest;
        HttpResponse mResponse;
        HttpReader mReader;
        HttpWriter mWriter;
        bool mHeaderSent;

        Encoding mUtf8NoBom = new UTF8Encoding(false); // Necessary because Encoding.UTF8 writes BOM

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
        internal HttpConext(HttpReader reader, HttpWriter writer)
        {
            mReader = reader;
            mWriter = writer;
        }

        public HttpRequest Request => mRequest;
        public HttpResponse Response => mResponse;

        /// <summary>
        /// Write the HTTP response header and return the input stream.  
        /// The response header may not be modified after calling this function.
        /// NOTE: If you have not set Response.ContentLength, this will set it to zero
        /// </summary>
        public HttpReader GetReader()
        {
            if (!mHeaderSent)
            {
                mResponse.ContentLength = Math.Max(0, mResponse.ContentLength);
                SendHeader();
            }
            return mReader;
        }

        /// <summary>
        /// Write the HTTP response header and return the output stream.
        /// The response may not be modified after calling this function.
        /// </summary>
        public HttpWriter GetWriter(long contentLength)
        {
            if (!mHeaderSent)
            {
                if (contentLength < 0)
                    throw new HttpException(500, "GetWriter: ContentLength must be >= zero", true);
                mResponse.ContentLength = contentLength;
                SendHeader();
            }
            if (contentLength !=  mResponse.ContentLength)
                throw new HttpException(500, "GetWriter: ContentLength cannot be changed once it is set");
            return mWriter;
        }

        /// <summary>
        /// Called only by HttpServer
        /// Read and parse the HTTP header, return true if everything worked.
        /// Returns false at EOF and throws an exception on error.
        /// </summary>
        internal bool ReadHeader()
        {
            mRequest = new HttpRequest();
            mResponse = new HttpResponse();
            mRequest.ReceiveDate = DateTime.Now;
            mHeaderSent = false;
            mReader.PositionInternal = 0;
            mReader.LengthInternal = HTTP_HEADER_MAX_SIZE;

            // Check for end of stream (TBD: Improve this)            
            int ch;
            try
            {
                ch = mReader.ReadByte();
                if (ch < 0)
                    return false;
            }
            catch (IOException)
            {
                return false;
            }

            var headerParts = ((char)ch + ReadLine()).Split(' ');
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
            var queryStrings = new HttpQuery();
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
            var headers = new HttpQuery();
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

            return true;
        }

        /// <summary>
        /// Read until there is a <CR><LF> as defined in RFC 7230.
        /// </summary>
        string ReadLine()
        {
            // NOTE: Improve performance using block reads to a buffer when we get a chance
            StringBuilder sb = new StringBuilder();
            int ch = mReader.ReadByte();
            while (ch != '\r')
            {
                if (ch < 0)
                    throw new HttpException(400, "ReadHeaderLine: Expecting <CR>, found <EOF>", true);
                if (ch == '\n')
                    throw new HttpException(400, "ReadHeaderLine: Expecting <CR>, found <LF>", true);
                sb.Append((char)ch);
                ch = mReader.ReadByte();
            }
            if ((ch = mReader.ReadByte()) != '\n')
                throw new HttpException(400, "ReadHeaderLine: Expecting <LF>, but found value char value " + ch, true);

            return sb.ToString();
        }


        /// <summary>
        /// Send the response header before the stream is read or written
        /// </summary>
        void SendHeader()
        {
            if (mHeaderSent)
                throw new HttpException(500, "SendHeader: Not allowed to send header twice", true);
            if (mResponse.ContentLength < 0)
                throw new HttpException(500, "SendHeader: ConentLength must be set before sending header", true);

            // Freeze response header and setup to write header
            mHeaderSent = true;
            mResponse.HeaderSent = true;
            mWriter.PositionInternal = 0;
            mWriter.LengthInternal = HTTP_HEADER_MAX_SIZE;

            // Check status message
            var statusMessage = mResponse.StatusMessage.Replace('\r', ' ').Replace('\n', ' ');
            if (mResponse.StatusCode != 200 && statusMessage == "OK")
                statusMessage = "?";

            // Send header
            var sw = new StreamWriter(mWriter, mUtf8NoBom, 1024, true);
            sw.WriteLine("HTTP/1.1 " + mResponse.StatusCode + " " + statusMessage);
            sw.WriteLine("Content-Length:" + mResponse.ContentLength);
            if (!Response.KeepAlive)
                sw.WriteLine("Connection:close");
            if (Response.KeepAlive && Request.ProtocolVersionMinor == 0)
                sw.WriteLine("Connection:keep-alive");
            sw.WriteLine("");
            sw.Dispose();

            // Good to go, reset streams
            mReader.PositionInternal = 0;
            mReader.LengthInternal = Math.Max(0, mRequest.ContentLength);
            mWriter.PositionInternal = 0;
            mWriter.LengthInternal = Math.Max(0, mResponse.ContentLength);
        }

        public void SendResponse(byte []message)
        {
            mResponse.ContentLength = message.Length;
            GetWriter(message.Length).Write(message, 0, message.Length);
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
            stream.CopyTo(GetWriter(stream.Length));
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
            var stream = GetReader();
            var buffer = new byte[mRequest.ContentLength];
            int index = 0;
            while (index < buffer.Length)
                index += stream.Read(buffer, index, buffer.Length - index);
            return buffer;
        }

    }
}
