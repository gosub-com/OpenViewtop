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
    public partial class Service : ServiceBase
    {
        const int HTTP_PORT = 8157;
        const int PROCESS_TERMINATE_TIMEOUT_MS = 1000;

        bool mRunning;
        Timer mSessionChangedTimer;
        HttpServer mHttpSeviceServer;

        ProcessManager mGuiMan;
        WtsProcess mGuiProcess;
        ProcessManager mDesktopMan;
        WtsProcess mDesktopProcess;


        const string DEFAULT_DESKTOP = "Default";
        string mDesktop = DEFAULT_DESKTOP;
        string mGuiLaunchDesktop = "";
        int mSessionId;
        string mDomainName = "";
        string mUserName = "";


        object mServiceLock = new object();
        
        public Service()
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
                mSessionChangedTimer = new Timer((obj) => { CheckServiceRunning(); }, null, 0, 120);
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

                TerminateProcess(ref mGuiProcess, ref mGuiMan, true);
                TerminateProcess(ref mDesktopProcess, ref mDesktopMan, true);
            }
        }

        /// <summary>
        /// Called periodically to ensure everything is running properly
        /// </summary>
        void CheckServiceRunning()
        {
            lock (mServiceLock)
            {
                if (!mRunning)
                    return;

                CheckHttpServerRunning();
                CheckSessionOrDesktopChanged();
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

        private void CheckSessionOrDesktopChanged()
        {
            try
            {
                // Check for GUI process stopped
                if (mGuiProcess != null && !mGuiProcess.IsRunning)
                {
                    Log.Write("GUI process has stopped for unknown reason.");
                    TerminateProcess(ref mGuiProcess, ref mGuiMan, false);
                }
                // Check for DESKTOP process stopped
                if (mDesktopProcess != null && !mDesktopProcess.IsRunning)
                {
                    Log.Write("DESKTOP process has stopped for unknown reason.");
                    TerminateProcess(ref mDesktopProcess, ref mDesktopMan, false);
                }
                // Check for desktop changed
                if (mDesktop != mGuiLaunchDesktop && mGuiProcess != null)
                {
                    Log.Write("Stopping GUI process because desktop changed from '" + mGuiLaunchDesktop + "' to '" + mDesktop);
                    TerminateProcess(ref mGuiProcess, ref mGuiMan, false);
                }
                // Check for session changed
                var sessionId = Wts.GetActiveConsoleSessionId();
                var domainName = Wts.GetSessionDomainName(sessionId);
                var userName = Wts.GetSessionUserName(sessionId);

                if (sessionId != mSessionId || domainName != mDomainName || userName != mUserName)
                {
                    mSessionId = sessionId;
                    mDomainName = domainName;
                    mUserName = userName;
                    if (mGuiProcess != null || mDesktopProcess != null)
                        Log.Write("Stopping GUI and DESKTOP processes because id or user changed, id=" + sessionId + ", user=" + Wts.GetSessionUserName(sessionId));
                    TerminateProcess(ref mGuiProcess, ref mGuiMan, false);
                    TerminateProcess(ref mDesktopProcess, ref mDesktopMan, false);

                    if (sessionId < 0)
                        Log.Write("*** No active session, not starting GUI or DESKTOP processes ***");
                }

                // Ensure processes are running
                if (mGuiProcess == null && sessionId >= 0)
                    StartGuiProcess(sessionId);
                if (mDesktopProcess == null && sessionId >= 0)
                    StartDesktopWatcherProcess(sessionId);
            }
            catch (Exception ex)
            {
                // TBD: Use event log
                Log.Write("Error checking session changed", ex);
            }
        }

        void StartGuiProcess(int sessionId)
        {
            TerminateProcess(ref mGuiProcess, ref mGuiMan, false);


            var desktop = mDesktop;
            mGuiLaunchDesktop = desktop;
            var pipeName = "OpenViewtop_gui_" + Util.GenerateRandomId();

            // When on the default desktop, run as the user so the clipboard works properly.
            // For other desktops we need to run as system so the GUI has permission to copy the screen.
            // TBD: Seems like there should be a better way to do this
            WtsProcessType processType = desktop.ToLower() != DEFAULT_DESKTOP.ToLower() || sessionId == 0 
                                            ? WtsProcessType.System : WtsProcessType.UserFallbackToSystem;
            processType |= WtsProcessType.Admin | WtsProcessType.UIAccess;

            // Mange process communications
            mGuiMan = new ProcessManager(pipeName, true);
            Task.Run(() => { ProcessRemoteCommunications(mGuiMan, "GUI"); });

            Log.Write("*** Starting GUI: Session=" + sessionId + ", User=" + mUserName + ", Desktop=" + desktop + ", Pipe=" + pipeName);
            mGuiProcess = WtsProcess.StartInSession(
                sessionId, processType,
                System.Windows.Forms.Application.ExecutablePath,
                Program.PARAM_GUI + " " + Program.PARAM_CONTROL_PIPE + " " + pipeName, desktop);

        }

        void StartDesktopWatcherProcess(int sessionId)
        {
            TerminateProcess(ref mDesktopProcess, ref mDesktopMan, false);

            // Create new process
            mDesktop = DEFAULT_DESKTOP; // If anything goes wrong, just use the default desktop
            var pipeName = "OpenViewtop_desktop_" + Util.GenerateRandomId();

            // Manage communications
            mDesktopMan = new ProcessManager(pipeName, true);
            Task.Run(() => { ProcessRemoteCommunications(mDesktopMan, "DESKTOP"); });

            Log.Write("*** Starting DESKTOP: Session=" + sessionId + ", User=" + mUserName + ", Pipe=" + pipeName);
            mDesktopProcess = WtsProcess.StartInSession(
                sessionId, WtsProcessType.System,
                System.Windows.Forms.Application.ExecutablePath,
                Program.PARAM_WATCH_DESKTOP + " " + Program.PARAM_CONTROL_PIPE + " " + pipeName);
        }

        /// <summary>
        /// Monitor GUI and DESKTOP processes
        /// </summary>
        async void ProcessRemoteCommunications(ProcessManager manager, string processName)
        {
            try
            {
                await manager.WaitForConnection();
                var command = "";
                while (command != ProcessManager.COMMAND_CLOSING)
                {
                    command = await manager.ReadCommandAsync();
                    if (command == ProcessManager.COMMAND_CONNECTED)
                        Log.Write(processName + " process connected on pipe " + manager.PipeName);
                    else if (command == ProcessManager.COMMAND_CLOSING)
                        Log.Write(processName + " process closing on pipe " + manager.PipeName);
                    else if (!command.StartsWith("$"))
                    {
                        // For now, all non-control commands are just the desktop name
                        if (command != mDesktop)
                        {
                            Log.Write("Desktop changed to: " + command);
                            mDesktop = command;
                            CheckServiceRunning();  // Restart GUI on new desktop
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Log.Write(processName + " pipe closed by other thread or remote process: Pipe=" + manager.PipeName);
            }
            catch (Exception ex)
            {
                Log.Write(processName + " process exception: Pipe=" + manager.PipeName, ex);
            }
            CheckServiceRunning();
        }

        /// <summary>
        /// End the task gracefully if possible, forcefully if necessesary.
        /// Disposes both the pipe and the process when done.
        /// Wait up to timeoutMs milliseconds for the application to close itself.
        /// This is optionally a blocking call, but can also be a "fire and forget"
        /// function by setting block=false.  
        /// </summary>
        void TerminateProcess(ref WtsProcess refProcess, ref ProcessManager refManager, bool block)
        {
            var process = refProcess;
            var manager = refManager;
            refProcess = null;
            refManager = null;

            // Quick exit if process is not running
            if (process == null || !process.IsRunning)
            {
                if (process != null)
                    process.Dispose();
                if (manager != null)
                    manager.Close();
                return;
            }

            // Force kill the process if the pipe is not connected
            if (manager == null || !manager.IsConnected)
            {
                // NOTE: Pipe was connected by WaitForPipeCommand
                Log.Write("FORCE KILL process because pipe is not connected: Pipe=" + manager.PipeName);
                process.Terminate(0);
                process.Dispose();
                if (manager != null)
                    manager.Close();
                return;
            }

            // Send the process a request to close in background thread
            Log.Write("Closing process: Pipe=" + manager.PipeName);
            Task.Run(async () =>
            {
                try
                {
                    await manager.SendCommandAsync(ProcessManager.COMMAND_CLOSE);
                }
                catch (Exception ex)
                {
                    Log.Write("Exception sending close command", ex);
                }
            });

            // Wait for it to exit gracefully, but kill it if still running after the timeout
            var task = Task.Run(async () =>
            {
                try
                {
                    // Give it up to one second to kill itslef
                    var now = DateTime.Now;
                    while ((DateTime.Now - now).TotalMilliseconds < PROCESS_TERMINATE_TIMEOUT_MS && process.IsRunning)
                        await Task.Delay(10);

                    if (process.IsRunning)
                    {
                        Log.Write("FORCE KILL process after sending cose command: Pipe=" + manager.PipeName);
                        process.Terminate(0);
                    }
                    else
                    {
                        Log.Write("Process closed gracefully: Pipe=" + manager.PipeName);
                    }
                    process.Dispose();
                    manager.Close();
                }
                catch (Exception ex)
                {
                    Log.Write("Cant kill process: Pipe=" + manager.PipeName, ex);
                }
            });

            if (block)
                task.Wait();

        }


    }
}
