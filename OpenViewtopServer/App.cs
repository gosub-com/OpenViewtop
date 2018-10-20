using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Gosub.Viewtop
{
    class App
    {
        public const string Name = "Open Viewtop";
        public static string Version
        {
            get
            {
                var ver = Assembly.GetEntryAssembly().GetName().Version;
                return "" + ver.Major + "." + ver.Minor + "." + ver.Build;
            }
        }
    }
}
