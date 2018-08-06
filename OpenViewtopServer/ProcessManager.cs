using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using Gosub.Viewtop;
using Gosub.Http;


namespace Gosub.OpenViewtopServer
{
    /// <summary>
    /// Manage communications for a process launched into a user's session.
    /// 
    /// Communications are ascii commands separated by line feeds,
    /// and lines starting with "$" are control codes.  
    /// See COMMAND_* for protocol info.
    ///     
    /// To prevent race conditions, the pipe should be created before the
    /// process starts, then set Process should be called to monitor it.
    /// </summary>
    class ProcessManager
    {
        string mPipeName = "";

        PipeStream mPipe;
        NamedPipeServerStream mServerPipe;
        StreamWriter mPipeWriter;
        StreamReader mPipeReader;

        public const string COMMAND_CONNECTED = "$connected"; // Sent by remote process when connected
        public const string COMMAND_CLOSE = "$close"; // Request remote application to close
        public const string COMMAND_CLOSING = "$closing"; // Remote application says it's closing

        public string PipeName => mPipeName;
        public bool IsConnected => mServerPipe != null && mServerPipe.IsConnected;

        /// <summary>
        /// Open a pipe to the remote process
        /// </summary>
        public ProcessManager(string pipeName, bool server)
        {
            try
            {
                mPipeName = pipeName;
                if (server)
                {
                    mServerPipe = new NamedPipeServerStream(mPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    mPipe = mServerPipe;
                }
                else
                {
                    var clientPipe = new NamedPipeClientStream(".", mPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    clientPipe.Connect();
                    mPipe = clientPipe;
                }
                mPipeWriter = new StreamWriter(mPipe);
                mPipeReader = new StreamReader(mPipe);
            }
            catch (Exception ex)
            {
                Log.Write("Error initializing pipe: ", ex);
            }
        }

        public void Close()
        {
            if (mPipe == null)
                return;
            try { mPipe.Close(); } catch { }
            mPipe = null;
        }

        /// <summary>
        /// Called by server to wait for connection from client
        /// </summary>
        public async Task WaitForConnection()
        {
            await Task.Factory.FromAsync(mServerPipe.BeginWaitForConnection, mServerPipe.EndWaitForConnection, null);
        }

        /// <summary>
        /// Send a command to the remote application.
        /// Use ASCII, do not include control characters.
        /// Exceptions are marshalled back to the command receiver.
        /// </summary>
        public async Task SendCommandAsync(string command)
        {
            await mPipeWriter.WriteLineAsync(command);
            await mPipeWriter.FlushAsync();
        }

        /// <summary>
        /// Read a command from the remote server.  Close application when COMMAND_CLOSE is received.
        /// </summary>
        public Task<string> ReadCommandAsync()
        {
            return mPipeReader.ReadLineAsync();
        }

    }
}
