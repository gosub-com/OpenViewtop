using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Gosub.Http
{

    static public class Log
    {
        static Queue<string> mLog = new Queue<string>();

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
                var log = new string[Math.Min(maxLines, mLog.Count)];
                int i = log.Length;
                foreach (var entry in mLog)
                {
                    if (--i < 0)
                        break;
                    log[i] = entry;
                }
                return string.Join("\r\n", log);
            }
        }

    }
}
