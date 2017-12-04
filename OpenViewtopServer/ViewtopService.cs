using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using Gosub.Viewtop;
using Gosub.Http;

namespace OpenViewtopServer
{
    public partial class ViewtopService : ServiceBase
    {
        const int HTTP_PORT = 8153;
        const int HTTPS_PORT = 8154;

        HttpServer mHttpServer;
        ViewtopServer mOvtServer = new ViewtopServer();
        Beacon mBeacon = new Beacon();

        public ViewtopService()
        {
        }

        protected override void OnStart(string[] args)
        {
            if (mHttpServer != null)
                mHttpServer.Stop();

            // Get machine name
            string machineName = "";
            try { machineName = Dns.GetHostName(); }
            catch { }
            if (machineName.Trim() == "")
                machineName = "localhost";

            mOvtServer = new ViewtopServer();
            mOvtServer.LocalComputerInfo.ComputerName = "V5, " + WindowsIdentity.GetCurrent().Name + "," + Environment.UserName;
            mOvtServer.LocalComputerInfo.Name = "CommonData path: " + Application.CommonAppDataPath;

            try
            {
                // Setup HTTP server
                mHttpServer = new HttpServer();
                mHttpServer.HttpHandler += (context) => { return mOvtServer.ProcessWebRemoteViewerRequest(context); };
                mHttpServer.Start(new TcpListener(IPAddress.Any, HTTP_PORT));
                mOvtServer.LocalComputerInfo.HttpPort = HTTP_PORT.ToString();

                // Setup HTTPS connection
                mHttpServer.Start(new TcpListener(IPAddress.Any, HTTPS_PORT), Util.GetCertificate());
                mOvtServer.LocalComputerInfo.HttpsPort = HTTPS_PORT.ToString();
            }
            catch (Exception ex)
            {
                try { mHttpServer.Stop(); } catch { }
                Debug.WriteLine("Error starting Open Viewtop service: " + ex.Message);
                return;
            }
        }

        protected override void OnStop()
        {
            if (mHttpServer != null)
                mHttpServer.Stop();
            mHttpServer = null;
            mOvtServer = new ViewtopServer();
        }

    }
}
