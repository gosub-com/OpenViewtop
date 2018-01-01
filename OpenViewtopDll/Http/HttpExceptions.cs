using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gosub.Http
{
    /// <summary>
    /// Throw this exception if there is a problem.  Set keepConnectionOpen to true
    /// if the persistent TCP connection can process another request.
    /// Server error message text (i.e. 500's) is logged, but not sent
    /// back to the client (only "SERVER ERROR" is displayed)
    /// Client error message text (i.e. 400's) is sent back to the client
    /// </summary>
    public class HttpException : Exception
    {
        public int Code { get; }
        public bool KeepConnectionOpen { get; }

        public HttpException(int code, string message, bool keepConnectionOpen)
            : base(message)
        {
            Code = code;
            KeepConnectionOpen = keepConnectionOpen;
        }

        public HttpException(int code, string message)
            : this(code, message, false)
        {
        }
    }

}
