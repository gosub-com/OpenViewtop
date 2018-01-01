using System;
using System.Windows.Forms;
using System.Threading;
using System.ServiceProcess;

using Gosub.Viewtop;

namespace OpenViewtopServer
{
    static class Program
    {
        public const string PARAM_USER_SERVER = "-userserver";
        public const string PARAM_SERVICE = "-service";
        public const string PARAM_DEBUG_SERVICE = "-debugservice";
        public const string PARAM_CONTROL_PIPE = "-controlpipe";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string []args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isService = false;
            bool isDebugService = false;
            bool isUserServer = false;
            string controlPipe = "";
            for (int i = 0;  i < args.Length;)
            {
                var param = args[i++];
                if (param == PARAM_USER_SERVER)
                    isUserServer = true;
                else if (param == PARAM_SERVICE)
                    isService = true;
                else if (param == PARAM_DEBUG_SERVICE)
                    isDebugService = true;
                else if (param == PARAM_CONTROL_PIPE && i < args.Length && !args[i].StartsWith("-"))
                    controlPipe = args[i++];
            }

            if (isService)
            {
                // Server service
                var viewtopService = new ViewtopService();
                ServiceBase.Run(viewtopService);
                return;
            }


            Form form;
            if (isDebugService)
            {
                form = new FormMain();
            }
            else if (isUserServer)
            {
                if (controlPipe == "")
                    controlPipe = "X";  // TBD: Change
                form = new FormMain(controlPipe);
            }
            else
            {
                form = new FormMain();
            }
            Application.Run(form);
        }
    }
}
