using System;
using System.Windows.Forms;
using System.Threading;
using System.ServiceProcess;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using Gosub.Http;
using Gosub.Viewtop;
using System.Runtime.InteropServices;

namespace Gosub.OpenViewtopServer
{
    public static class Program
    {
        public const string PARAM_SERVICE = "-service";
        public const string PARAM_SERVER = "-server";
        public const string PARAM_CONTROL_PIPE = "-controlpipe";
        public const string PARAM_DESKTOP = "-desktop";
        public const string PARAM_START_BROWSER = "-startbrowser";

        const string IS_RUNNING_MUTEX_NAME = "OpenViewtopMutex_IsRunning";
        const string SHOW_ON_TOP_MUTEX_NAME = "OpenViewtopMutex_ShowOnTop";

        // The other server could still be closing, so wait a short time
        // for it to exit.  But if it doesn't close fairly quickly, exit
        // and let the service restart us.
        const int WAIT_FOR_OTHER_SERVER_TIMEOUT_MS = 1000;

        public static bool ParamIsService = false;
        public static bool ParamIsServer = false;
        public static string ParamControlPipe = "";
        public static string ParamDesktop = "";

        private enum ProcessDPIAwareness
        {
            ProcessDPIUnaware = 0,
            ProcessSystemDPIAware = 1,
            ProcessPerMonitorDPIAware = 2
        }

        [DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(ProcessDPIAwareness value);


        // This was an attempt to switch desktops at program start.  Doing it 
        // that way would simplify the architecture because we wouldn't need
        // to send the current desktop back to the service to re-launch the
        // server on the new desktop.  It didn't work because the server
        // didn't get access to the desktop when launched to "WinSta0\Default"
        // and then switch itself to "WinSta0\Winlogon"
        static void MainDesktopSwitch(string[] args)
        {
            var status = Wts.SetThreadToCurrentDesktop();
            var thread = new Thread((object t) =>
            {
                Log.Write("SET THREAD TO DESKTOP: " + status);
                Main(args);
            });
            if (!thread.TrySetApartmentState(ApartmentState.STA))
                Log.Write("Error setting apartment state");
            thread.Start(thread);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string []args)
        {
            // Ensure that screen bound returns the correct size for each minitor.
            try { SetProcessDpiAwareness(ProcessDPIAwareness.ProcessPerMonitorDPIAware); }
            catch { }

            for (int i = 0; i < args.Length;)
            {
                var param = args[i++];
                if (param == PARAM_SERVICE)
                    ParamIsService = true;
                else if (param == PARAM_SERVER)
                    ParamIsServer = true;
                else if (param == PARAM_CONTROL_PIPE && i < args.Length)
                    ParamControlPipe = args[i++];
                else if (param == PARAM_DESKTOP && i < args.Length)
                    ParamDesktop = args[i++];
                else if (param == PARAM_START_BROWSER && i < args.Length)
                {
                    var fileName = args[i++];
                    try
                    {
                        Process.Start(fileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error starting browser: " + ex.Message, App.Name);
                    }
                    return;
                }
            }

            string progParams = "";
            foreach (var param in args)
                progParams += " " + (param.Contains(" ") ? "\"" + param + "\"" : param);
            Log.Write("PARAMETERS: " + progParams);

            // Check to see if the server is running on the correct desktop.
            // If not, send back the correct desktop and make a quick exit.
            if (ParamControlPipe != "" && ParamDesktop != "")
            {
                var myDesktop = Wts.GetDesktopName();
                Log.Write("Desktop: " + myDesktop);
                if (ParamDesktop.ToLower() != myDesktop.ToLower())
                {
                    // The server was started on the wrong desktop.
                    // Send correct desktop back to service and then exit.               
                    try
                    {
                        var np = new NamedPipeClientStream(".", ParamControlPipe, PipeDirection.InOut);
                        np.Connect();
                        var control = new StreamWriter(np);
                        control.WriteLine(myDesktop);
                        control.Flush();
                        np.Close();
                        return;
                    }
                    catch (Exception ex)
                    {
                        // Just let it run
                        Log.Write("Error sending desktop info", ex);
                    }
                }
            }

            // Run as a windows service
            if (ParamIsService)
            {
                // Server service
                var viewtopService = new ViewtopService();
                ServiceBase.Run(viewtopService);
                return;
            }


            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new FormServer();
            form.ControlPipe = ParamControlPipe;

            // Just run under debugger
            if (Debugger.IsAttached)
            {
                Application.Run(form);
                return;
            }

            // The user clicked the application.  Send a message to show the server
            var serverRunningMutex = new Mutex(false, IS_RUNNING_MUTEX_NAME);
            if (!ParamIsServer)
            {
                if (serverRunningMutex.WaitOne(0, true))
                {
                    // Server is not running, show error message
                    serverRunningMutex.ReleaseMutex();
                    MessageBox.Show("The Open Viewtop server is not running or the "
                        + "service has been stopped.  Try again and if the problem persists, "
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
