using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gosub.Http
{
    /// <summary>
    /// HTTP response header that is sent to the client.
    /// This is automatically sent immediately before reading or
    /// writing the stream and is permenently frozen after that.
    /// </summary>
    public class HttpResponse
    {
        const string CRLF = "\r\n";

        bool mHeaderSent;
        int mStatusCode = 200;
        string mStatusMessage = "OK";
        long mContentLength = -1;
        string mConnection = "";
        string mContentType = "";
        HttpDict mHeaders;
        HttpDict mCookies;

        public bool HeaderSent { get => mHeaderSent; set { CheckSent(); mHeaderSent = value; } }
        public int StatusCode { get => mStatusCode; set { CheckSent(); mStatusCode = value; } }
        public string StatusMessage { get => mStatusMessage; set { CheckSent(); mStatusMessage = value; } }
        public long ContentLength { get => mContentLength; set { CheckSent(); mContentLength = value; } }
        public string Connection { get => mConnection; set { CheckSent(); mConnection = value; } }
        public string ContentType { get => mContentType;  set { CheckSent(); mContentType = value; } }
        public HttpDict Headers => mHeaders == null ? mHeaders = new HttpDict() : mHeaders;
        public HttpDict Cookies => mCookies == null ? mCookies = new HttpDict() : mCookies;


        void CheckSent()
        {
            if (mHeaderSent)
                throw new Exception("Response header cannot be modified after it was already sent");
        }

        public string Generate()
        {
            // Status message
            var statusMessage = StatusMessage.Replace('\r', ' ').Replace('\n', ' ');
            if (StatusCode != 200 && statusMessage == "OK")
                statusMessage = "?";

            StringBuilder header = new StringBuilder();
            header.Append(
                "HTTP/1.1 " + StatusCode + " " + statusMessage + CRLF
                + (ContentLength < 0 ? "" : "content-length:" + ContentLength + CRLF)
                + "connection:" + Connection + CRLF
                + (ContentType == "" ? "" : "content-type:" + ContentType + CRLF));

            if (mHeaders != null)
                foreach (var headerOption in mHeaders)
                    header.Append(headerOption.Key + ":" + headerOption.Value + CRLF);

            if (mCookies != null)
                foreach (var cookie in mCookies)
                    header.Append("set-cookie: " + cookie.Key + "=" + cookie.Value);

            header.Append(CRLF);
            return header.ToString();
        }

    }
}
