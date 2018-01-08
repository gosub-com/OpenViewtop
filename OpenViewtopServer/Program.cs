using System;
using System.Windows.Forms;
using System.Threading;
using System.ServiceProcess;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Gosub.Viewtop;

namespace OpenViewtopServer
{
    static class Program
    {
        public const string PARAM_SERVICE = "-service";
        public const string PARAM_SERVER = "-server";
        public const string PARAM_CONTROL_PIPE = "-controlpipe";

        const string IS_RUNNING_MUTEX_NAME = "OpenViewtopMutex_IsRunning";
        const string SHOW_ON_TOP_MUTEX_NAME = "OpenViewtopMutex_ShowOnTop";

        // The other server could still be closing, so wait a short time
        // for it to exit.  But if it doesn't close fairly quickly, exit
        // and let the service restart us.
        const int WAIT_FOR_OTHER_SERVER_TIMEOUT_MS = 1000;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string []args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isService = false;
            bool isServer = false;
            string controlPipe = "";
            for (int i = 0;  i < args.Length;)
            {
                var param = args[i++];
                if (param == PARAM_SERVICE)
                    isService = true;
                else if (param == PARAM_SERVER)
                    isServer = true;
                else if (param == PARAM_CONTROL_PIPE && i < args.Length && !args[i].StartsWith("-"))
                    controlPipe = args[i++];
            }

            // Run as a windows service
            if (isService)
            {
                // Server service
                var viewtopService = new ViewtopService();
                ServiceBase.Run(viewtopService);
                return;
            }

            var form = new FormServer();
            form.ControlPipe = controlPipe;

            // Just run under debugger
            if (Debugger.IsAttached)
            {
                Application.Run(form);
                return;
            }

            // The user clicked the application.  Send a message to show the server
            var serverRunningMutex = new Mutex(false, IS_RUNNING_MUTEX_NAME);
            if (!isServer)
            {
                if (serverRunningMutex.WaitOne(0, true))
                {
                    // Server is not running, show error message
                    serverRunningMutex.ReleaseMutex();
                    MessageBox.Show("The Open Viewtop server is not running or the "
                        + "service has been stopped.  Try again and if the problem persist, "
                        + "re-install the application.", App.Name);
                }
                else
                {
                    // Server is running, use mutex being polled in the other server to show it to top
                    // TBD: A better way to do this is to use RegisterWindowMessage and sent it
                    //      to the other application, but this doesn't seem to work since the
                    //      server was started at high privilege by the service.  
                    var showMutex = new Mutex(false, SHOW_ON_TOP_MUTEX_NAME);
                    showMutex.WaitOne(0, true);
                    Thread.Sleep(1000);
                    showMutex.ReleaseMutex();
                }
                return;
            }

            // Run as server, started from the service
            if (serverRunningMutex.WaitOne(WAIT_FOR_OTHER_SERVER_TIMEOUT_MS, true))
            {
                try
                {
                    form.ShowOnTopMutex = new Mutex(false, SHOW_ON_TOP_MUTEX_NAME);
                    Application.Run(form);
                }
                finally
                {
                    serverRunningMutex.ReleaseMutex();
                }
            }

        }
    }
}
