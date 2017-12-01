using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gosub.Http
{
    public enum WebSocketState
    {
        None = 0,
        Connecting = 1,
        Open = 2,
        CloseSent = 3,
        CloseReceived = 4,
        Closed = 5
    }
}
