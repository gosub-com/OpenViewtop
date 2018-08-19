using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Gosub.Http
{

    static public class Log
    {
        static QueueList<string> mLog = new QueueList<string>();

        /// <summary>
        /// Default: Write to console when debugger is attached
        /// </summary>
        static public bool WriteToConsole { get; set; } = Debugger.IsAttached;
        static public int MaxEntries { get; set; } = 1000;

        static Log()
        {
            Write("*** STARTING LOG ***");
        }

        static public void Write(string message, [CallerLineNumber]int lineNumber=0, [CallerFilePath]string fileName="", [CallerMemberName]string memberName="")
        {
            if (lineNumber > 0)
                message = "\"" + message + "\", " + Path.GetFileName(fileName) + " " + lineNumber + " " + memberName + "()";
            Add(message);
        }

        static public void Write(string message, Exception exception, [CallerLineNumber]int lineNumber = -1, [CallerFilePath]string fileName = "", [CallerMemberName]string memberName = "")
        {
            if (lineNumber > 0)
                message = "\"" + message + "\", " + Path.GetFileName(fileName) + " " + lineNumber + " " + memberName + "()";
            Add(message + ", \"" + exception.Message + "\", " + exception.GetType().Name +  ", STACK " + exception.StackTrace);
        }

        static void Add(string message)
        {
            message = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff") + " - " + message;
            lock (mLog)
            {
                mLog.Enqueue(message);
                if (mLog.Count > MaxEntries)
                    mLog.Dequeue();
            }
            if (WriteToConsole)
                Debug.WriteLine(message);
        }

        static public string GetAsString(int maxLines)
        {
            lock (mLog)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = mLog.Count-1;  i >= 0;  i--)
                {
                    sb.Append(mLog[i]);
                    if (i != 0)
                        sb.Append("\r\n");
                }
                return sb.ToString();
            }
        }

    }
}
