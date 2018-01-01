using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json;
using Gosub.Viewtop;
using Gosub.Http;

namespace OpenViewtopServer
{
    public partial class ViewtopService : ServiceBase
    {
        const int HTTP_PORT = 8157;
        HttpServer mHttpSeviceServer;

        int mActiveSessionId = -1;
        Wts.Process mActiveProcess;
        Timer mSessionChangedTimer;

        List<string> mLog = new List<string>();

        HttpServer mHttpServer;
        ViewtopServer mOvtServer;
        
        void AddLog(string log)
        {
            lock (mLog)
            {
                if (mLog.Count > 200)
                    mLog.RemoveAt(mLog.Count-1);
                mLog.Insert(0, DateTime.Now.ToString() + " - " + log);
            }
        }

        public ViewtopService()
        {
            AddLog("Starting Service...");
        }

        public bool IsRunning => mHttpSeviceServer != null;

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);
            AddLog("Session CHANGE: " + changeDescription.SessionId + ", " + Wts.GetActiveConsoleSessionId());
        }

        protected override void OnStart(string[] args)
        {
            OnStop();
            AddLog("OnStart");
            try
            {
                // Setup HTTP server
                mHttpSeviceServer = new HttpServer();
                mHttpSeviceServer.HttpHandler += (context) => 
                {
                    if (context.Request.Target == "/ovt/log")
                        return context.SendResponseAsync(string.Join("\r\n", mLog.ToArray()));
                    if (context.Request.Target == "/ovt/sessions")
                        return context.SendResponseAsync(JsonConvert.SerializeObject(Wts.GetSessions(), Formatting.Indented));
                    throw new HttpException(404, "File not found");
                };
                mHttpSeviceServer.Start(new TcpListener(IPAddress.Any, HTTP_PORT));
            }
            catch (Exception ex)
            {
                try { mHttpSeviceServer.Stop(); }
                catch { }
                Debug.WriteLine("Error starting Open Viewtop service: " + ex.Message);
            }
            mSessionChangedTimer = new Timer((obj) => { CheckSessionChanged(); }, null, 0, 453);

            mOvtServer = new ViewtopServer();
            try
            {
                // Setup HTTP server
                mHttpServer = new HttpServer();
                mHttpServer.HttpHandler += (context) => { return mOvtServer.ProcessOpenViewtopRequestAsync(context); };
                mHttpServer.Start(new TcpListener(IPAddress.Any, 8159));
                mOvtServer.LocalComputerInfo.HttpPort = "8159";
            }
            catch (Exception ex)
            {
                AddLog("Error starting OVT server: " + ex.Message);
                try { mHttpServer.Stop(); } catch { }
            }
        }

        protected override void OnStop()
        {
            if (mActiveProcess == null && mHttpSeviceServer == null && mSessionChangedTimer == null && mActiveSessionId < 0)
                return;

            AddLog("OnStop");
            try { if (mHttpSeviceServer != null) mHttpSeviceServer.Stop();  }
            catch { }
            try { if (mSessionChangedTimer != null) mSessionChangedTimer.Dispose(); }
            catch { }
            KillActiveSession();
            mHttpSeviceServer = null;
            mSessionChangedTimer = null;
        }

        void CheckSessionChanged()
        {
            try
            {
                if (mActiveProcess != null && !mActiveProcess.IsRunning)
                {
                    AddLog("Process has stopped.  Restarting.");
                    KillActiveSession();
                }

                var sessionId = Wts.GetActiveConsoleSessionId();
                if (sessionId <= 0 || sessionId == mActiveSessionId)
                    return;

                AddLog("Starting process on session " + sessionId);
                KillActiveSession();
                mActiveProcess = Wts.Process.CreateProcessAsUser(sessionId, System.Windows.Forms.Application.ExecutablePath,  "-userserver");
                mActiveSessionId = sessionId;
            }
            catch (Exception ex2)
            {
                // TBD: Use event log
                Debug.WriteLine("Error: " + ex2.Message);
                AddLog("Error 2: " + ex2.Message);
            }
        }

        private void KillActiveSession()
        {
            mActiveSessionId = -1;
            if (mActiveProcess == null)
                return;
            try { mActiveProcess.Terminate(0); }
            catch { }
            try { mActiveProcess.Dispose(); }
            catch { }
            mActiveProcess = null;
        }
    }
}
