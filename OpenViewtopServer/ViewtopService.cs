using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Gosub.Viewtop;
using Gosub.Http;

namespace OpenViewtopServer
{
    public partial class ViewtopService : ServiceBase
    {
        const int HTTP_PORT = 8157;

        bool mRunning;
        Timer mSessionChangedTimer;
        HttpServer mHttpSeviceServer;

        int mServerSessionId = -1;
        Wts.Process mServerProcess;
        string mServerPipeName = "";
        NamedPipeServerStream mServerPipe;
        IAsyncResult mServerPipeAsyncResult;

        object mServiceLock = new object();
        
        public ViewtopService()
        {
        }

        protected override void OnStart(string[] args)
        {
            lock (mServiceLock)
            {
                if (mRunning)
                    return;
                Log.Write("OnStart");
                mRunning = true;
                mSessionChangedTimer = new Timer((obj) => { ServiceRunningTimer(); }, null, 0, 453);
            }
        }

        protected override void OnStop()
        {
            lock (mServiceLock)
            {
                if (!mRunning)
                    return;
                mRunning = false;

                Log.Write("OnStop");
                try { if (mHttpSeviceServer != null) mHttpSeviceServer.Stop(); }
                catch { }
                try { if (mSessionChangedTimer != null) mSessionChangedTimer.Dispose(); }
                catch { }
                mSessionChangedTimer = null;
                mHttpSeviceServer = null;
                KillServerProcess(true);
            }
        }

        /// <summary>
        /// Called periodically to ensure everything is running properly
        /// </summary>
        void ServiceRunningTimer()
        {
            lock (mServiceLock)
            {
                if (!mRunning)
                    return;

                CheckHttpServerRunning();
                CheckSessionChanged();
            }
        }

        private void CheckHttpServerRunning()
        {
            if (mHttpSeviceServer != null)
                return;

            try
            {
                // Setup HTTP server
                mHttpSeviceServer = new HttpServer();
                mHttpSeviceServer.HttpHandler += (context) =>
                {
                    if (context.Request.Target == "/ovt/log")
                        return context.SendResponseAsync(Log.GetAsString(200));
                    if (context.Request.Target == "/ovt/sessions")
                        return context.SendResponseAsync(JsonConvert.SerializeObject(Wts.GetSessions(), Formatting.Indented));
                    throw new HttpException(404, "File not found");
                };
                mHttpSeviceServer.Start(new TcpListener(IPAddress.Any, HTTP_PORT));
            }
            catch (Exception ex)
            {
                Log.Write("Error starting Open Viewtop service", ex);
                try { mHttpSeviceServer.Stop(); }
                catch { }
                mHttpSeviceServer = null;
            }
        }

        private void CheckSessionChanged()
        {
            try
            {
                if (mServerProcess != null && !mServerProcess.IsRunning)
                {
                    Log.Write("Process has stopped.  Restarting.");
                    KillServerProcess(false);
                }

                var sessionId = Wts.GetActiveConsoleSessionId();
                if (sessionId <= 0 || sessionId == mServerSessionId)
                    return;

                // Kill process, then start a new one
                KillServerProcess(false);
                StartServerProcess(sessionId);
            }
            catch (Exception ex2)
            {
                // TBD: Use event log
                Log.Write("Error checking session changed", ex2);
            }
        }

        private void StartServerProcess(int sessionId)
        {
            try
            {
                mServerPipeName = "OpenViewtop_" + Util.GenerateRandomId();
                mServerPipe = new NamedPipeServerStream(mServerPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                mServerPipeAsyncResult = mServerPipe.BeginWaitForConnection(null, null);
            }
            catch (Exception ex)
            {
                Log.Write("Error opening pipe", ex);
                try { mServerPipe.Dispose(); } catch { }
                mServerPipeName = "(error)";
                mServerPipe = null;
                mServerPipeAsyncResult = null;
            }
            Log.Write("Starting process on session " + sessionId + ", pipe=" + mServerPipeName);
            mServerProcess = Wts.Process.CreateProcessAsUser(sessionId, System.Windows.Forms.Application.ExecutablePath,
                Program.PARAM_SERVER + " " + Program.PARAM_CONTROL_PIPE + " " + mServerPipeName);
            mServerSessionId = sessionId;
        }

        private void KillServerProcess(bool wait)
        {
            var process = mServerProcess;
            var pipeName = mServerPipeName;
            var pipe = mServerPipe;
            var pipeResult = mServerPipeAsyncResult;
            mServerSessionId = -1;
            mServerProcess = null;
            mServerPipeName = "";
            mServerPipe = null;
            mServerPipeAsyncResult = null;

            if (process != null)
                Log.Write("Kill server, pipe=" + pipeName + ", alive=" + process.IsRunning);

            if (process == null || !process.IsRunning)
            {
                if (process != null)
                    process.Dispose();
                if (pipe != null)
                    pipe.Dispose();
                return;
            }

            if (pipe == null || pipeResult == null || !pipeResult.IsCompleted)
            {
                Log.Write("Pipe not connected, can't gracefully kill process");
                if (process.IsRunning)
                    Log.Write("FORCE KILL SERVER, pipe=" + pipeName + ", alive=" + process.IsRunning);
                process.Terminate(0);
                process.Dispose();
                if (pipe != null)
                    pipe.Dispose();
            }

            // Send close command in background thread
            Task.Run(async () => 
            {
                try
                {
                    pipe.EndWaitForConnection(pipeResult);
                    var sw = new StreamWriter(pipe);
                    await sw.WriteLineAsync("close");
                    await sw.FlushAsync();
                }
                catch (Exception ex)
                {
                    Log.Write("Error sending close command", ex);
                }
            });

            // Wait for it to kill itself gracefully
            var task = Task.Run(async () => 
            {
                // Give it up to one second to kill itslef
                var now = DateTime.Now;
                while ((DateTime.Now - now).TotalMilliseconds < 1000 && process.IsRunning)
                    await Task.Delay(10);

                if (process.IsRunning)
                    Log.Write("FORCE KILL SERVER, pipe=" + pipeName + ", alive=" + process.IsRunning);
                process.Terminate(0);
                process.Dispose();
                pipe.Dispose();
            });

            if (wait)
                task.Wait();
        }
    }
}
