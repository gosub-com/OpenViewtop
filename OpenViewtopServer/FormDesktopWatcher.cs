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
        const int POLL_FOR_DESKTOP_CHANGE_MS = 25;

        string mDesktop = "";
        ProcessManager mProcessManager;

        public FormDesktopWatcher()
        {
            InitializeComponent();
        }

        private async void FormDesktopWatcher_Load(object sender, EventArgs e)
        {
            // Watch desktop, then close application when requested
            await Task.Run(async () => { await ProcessServerCommands(); });
            try { Application.Exit(); } catch { }
        }

        private void FormDesktopWatcher_Shown(object sender, EventArgs e)
        {
            Hide();
        }

        async Task ProcessServerCommands()
        {
            try
            {
                mProcessManager = new ProcessManager(Program.ParamControlPipe, false);
                await mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CONNECTED);
                var commandTask = mProcessManager.ReadCommandAsync();

                // Poll for desktop change every POLL_FOR_DESKTOP_CHANGE_MS milliseconds,
                // and close application when COMMAND_CLOSE is received
                while (true)
                {
                    var desktop = Wts.GetDesktopName();
                    if (desktop != mDesktop)
                        await mProcessManager.SendCommandAsync(desktop);
                    mDesktop = desktop;

                    if (commandTask.IsCompleted)
                    {
                        var command = commandTask.Result;
                        commandTask = mProcessManager.ReadCommandAsync();
                        if (command.StartsWith(ProcessManager.COMMAND_CLOSE))
                        {
                            await mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CLOSING + "@reason=Closed by service");
                            break;
                        }
                    }
                    commandTask.Wait(POLL_FOR_DESKTOP_CHANGE_MS);
                }
            }
            catch (Exception ex)
            {
                try { mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CLOSING + "@reason=Exception: " + ex.Message + ", Stack: " + ex.StackTrace.Replace("\n", "").Replace("\r", "")).Wait(100); }
                catch { }
            }
        }

        private void FormDesktopWatcher_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // Try sending the message, even though the pipe could
                // be closed, transmitting, or not even open yet
                mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CLOSING + "@reason=" + e.CloseReason).Wait(100);
                mProcessManager.Close();
            }
            catch { }
        }
    }
}
