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
        bool mHeaderSent;
        int mStatusCode = 200;
        string mStatusMessage = "OK";
        long mContentLength = -1;

        public bool HeaderSent { get => mHeaderSent; set { CheckSent(); mHeaderSent = value; } }
        public int StatusCode { get => mStatusCode; set { CheckSent(); mStatusCode = value; } }
        public string StatusMessage { get => mStatusMessage; set { CheckSent(); mStatusMessage = value; } }
        public long ContentLength { get => mContentLength; set { CheckSent(); mContentLength = value; } }

        void CheckSent()
        {
            if (mHeaderSent)
                throw new Exception("Response header cannot be modified after it was already sent");
        }

    }
}
