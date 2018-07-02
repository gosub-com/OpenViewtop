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

namespace Gosub.OpenViewtopServer
{
    public partial class ViewtopService : ServiceBase
    {
        const int HTTP_PORT = 8157;

        bool mRunning;
        Timer mSessionChangedTimer;
        HttpServer mHttpSeviceServer;

        WtsProcess mServerProcess;

        string mServerPipeName = "";
        NamedPipeServerStream mServerPipe;

        const string DEFAULT_DESKTOP = "Default";
        string mDesktop = DEFAULT_DESKTOP;

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
                mSessionChangedTimer = new Timer((obj) => { ServiceRunningTimer(); }, null, 0, 120);
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
                    Log.Write("FILE NOT FOUND: " + context.Request.Target);
                    return context.SendResponseAsync("File not found: " + context.Request.Target, 404);
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
                if (sessionId <= 0
                    || mServerProcess != null
                         && sessionId == mServerProcess.SessionId
                         && Wts.GetSessionDomainName(sessionId) == mServerProcess.DomainName
                         && Wts.GetSessionUserName(sessionId) == mServerProcess.UserName)
                {                    
                    return; // No change to session (or invalid session ID)
                }


                Log.Write("Session changed, id=" + sessionId + ", user=" + Wts.GetSessionUserName(sessionId));

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
                Task.Run(() => { WaitForPipeCommand(mServerPipe); });
            }
            catch (Exception ex)
            {
                Log.Write("Error opening pipe", ex);
                try { mServerPipe.Dispose(); } catch { }
                mServerPipeName = "(error)";
                mServerPipe = null;
            }

            // It seems like there should be a way to query the active desktop
            // from the service.  Since there doesn't seem to be one, we'll
            // let the application tell us which desktop is the active one
            var desktop = mDesktop;
            mDesktop = DEFAULT_DESKTOP;
            if (desktop.ToLower().Contains("$error"))
            {
                // Use the default desktop if there was an error
                Log.Write("Error retriving desktop name from client: " + desktop);
                desktop = DEFAULT_DESKTOP;
            }

            Log.Write("***Starting process on session " + sessionId + ", pipe=" + mServerPipeName + ", desktop=" + desktop);
            mServerProcess = WtsProcess.StartAsUser(sessionId, true,
                System.Windows.Forms.Application.ExecutablePath,
                Program.PARAM_SERVER + " " + Program.PARAM_CONTROL_PIPE + " " + mServerPipeName + " " 
                + Program.PARAM_DESKTOP + " \"" + desktop + "\"", 
                desktop);
        }


        /// <summary>
        /// Called in a background thread to process info from server
        /// </summary>
        async void WaitForPipeCommand(NamedPipeServerStream pipe)
        {
            try
            {
                Log.Write("Wait for pipe connect");
                await Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null);
                Log.Write("Got pipe connect");
                var sr = new StreamReader(pipe);
                string desktop = await sr.ReadLineAsync();

                Log.Write("Desktop change, desktop=" + desktop);
                if (desktop.Trim() != "")
                    lock (mServiceLock)
                        mDesktop = desktop;                
                // NOTE: For the time being we only ever get one thing from the server, and
                //       that is the name of the active desktop
            }
            catch (ObjectDisposedException)
            {
                // The program exited
                Log.Write("Pipe closed by server");
            }
            catch (Exception ex)
            {
                Log.Write("Error reading from pipe", ex);
            }
            // Exit without closing the pipe.  It will be closed by KillServerProcess.
            //CheckSessionChanged();
        }

        private void KillServerProcess(bool wait)
        {
            var process = mServerProcess;
            var pipeName = mServerPipeName;
            var pipe = mServerPipe;
            mServerProcess = null;
            mServerPipeName = "";
            mServerPipe = null;

            if (process != null)
                Log.Write("Kill server, pipe=" + pipeName + ", alive=" + process.IsRunning);

            // Quick exit if process is not running
            if (process == null || !process.IsRunning)
            {
                if (process != null)
                    process.Dispose();
                if (pipe != null)
                    pipe.Dispose();
                return;
            }

            // Force kill the process if the pipe is not connected
            if (pipe == null || !pipe.IsConnected)
            {
                // NOTE: Pipe was connected by WaitForPipeCommand
                Log.Write("FORCE KILL SERVER because pipe is not connected, pipe=" + pipeName + ", alive=" + process.IsRunning);
                process.Terminate(0);
                process.Dispose();
                if (pipe != null)
                    pipe.Dispose();
                return;
            }

            // We have a connected pipe and running server.
            // Send close command in background thread
            Task.Run(async () => 
            {
                try
                {
                    var sw = new StreamWriter(pipe);
                    await sw.WriteLineAsync("close");
                    await sw.FlushAsync();
                }
                catch (Exception ex)
                {
                    Log.Write("Error sending close command", ex);
                }
            });

            // Wait for it to exit gracefully, but kill it if still running after one second
            var task = Task.Run(async () => 
            {
                try
                {
                    // Give it up to one second to kill itslef
                    var now = DateTime.Now;
                    while ((DateTime.Now - now).TotalMilliseconds < 1000 && process.IsRunning)
                        await Task.Delay(10);

                    if (process.IsRunning)
                        Log.Write("FORCE KILL SERVER after sending cose command, pipe=" + pipeName + ", alive=" + process.IsRunning);
                    process.Terminate(0);
                    process.Dispose();
                    pipe.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Write("Cant kill process", ex);
                }
            });

            if (wait)
                task.Wait();
        }
    }
}
