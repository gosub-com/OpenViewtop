using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gosub.Http
{
    public struct WebSocketReceiveResult
    {
        public WebSocketReceiveResult(int count, WebSocketMessageType messageType, bool endOfMessage)
        {
            Count = count;
            MessageType = messageType;
            EndOfMessage = endOfMessage;
            CloseStatus = WebSocketCloseStatus.Empty;
            CloseStatusDescription = "";
        }
        public WebSocketReceiveResult(int count, WebSocketMessageType messageType, bool endOfMessage, WebSocketCloseStatus? closeStatus, string closeStatusDescription)
        {
            Count = count;
            MessageType = messageType;
            EndOfMessage = endOfMessage;
            CloseStatus = closeStatus;
            CloseStatusDescription = closeStatusDescription;
        }
        public int Count { get; }
        public bool EndOfMessage { get; }
        public WebSocketMessageType MessageType { get; }
        public WebSocketCloseStatus? CloseStatus { get; }
        public string CloseStatusDescription { get; }
    }
}
