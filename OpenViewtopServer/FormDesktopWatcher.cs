using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gosub.Viewtop;

namespace Gosub.OpenViewtopServer
{
    public partial class FormDesktopWatcher : Form
    {
        bool mClosing;
        string mDesktop = "";
        ProcessManager mProcessManager;

        public FormDesktopWatcher()
        {
            InitializeComponent();
        }

        private void FormDesktopWatcher_Load(object sender, EventArgs e)
        {
            // Do not flash the form when it is first displayed
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-10000, -10000);
            Size = new Size();
        }

        private void FormDesktopWatcher_Shown(object sender, EventArgs e)
        {
            Hide();
            mProcessManager = new ProcessManager(Program.ParamControlPipe, false);
            WatchDesktop();
            ProcessServerCommands();
        }

        async void WatchDesktop()
        {
            await mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CONNECTED);
            while (!mClosing)
            {
                var desktop = Wts.GetDesktopName();
                if (desktop != mDesktop)
                    await mProcessManager.SendCommandAsync(desktop);
                await Task.Delay(50);
            }
        }

        async void ProcessServerCommands()
        {
            while (!mClosing)
            {
                var command = await mProcessManager.ReadCommandAsync();
                if (command == ProcessManager.COMMAND_CLOSE)
                {
                    mClosing = true;
                    await mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CLOSING);
                }
            }
        }
        
    }
}
