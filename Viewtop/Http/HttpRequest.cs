using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;

namespace Gosub.Http
{
    /// <summary>
    /// HTTP request header received from client
    /// </summary>
    public class HttpRequest
    {
        public DateTime ReceiveDate;

        public string HttpMethod;
        public int ProtocolVersionMajor;
        public int ProtocolVersionMinor;

        // Target resource
        public string Target;
        public string TargetFull;
        public string QueryFull;
        public string Fragment;
        public HttpQuery Query;

        // Headers
        public HttpQuery Headers;
        public string Host;
        public long ContentLength;
        public bool KeepAlive;
    }
}
