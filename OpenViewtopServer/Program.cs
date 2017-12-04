using System;
using System.Windows.Forms;
using System.Threading;
using System.ServiceProcess;

using Gosub.Viewtop;

namespace OpenViewtopServer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string []args)
        {
            bool isService = false;
            foreach (var param in args)
                if (param == "-service")
                    isService = true;

            if (isService)
            {
                // Run Service.
                var viewtopService = new ViewtopService();
                var services = new ServiceBase[] { viewtopService };
                ServiceBase.Run(services);
            }
            else
            {
                // Run GUI
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FormMain());
            }
        }
    }
}
