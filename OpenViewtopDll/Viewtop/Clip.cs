using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using static Gosub.Viewtop.NativeMethods;

namespace Gosub.Viewtop
{
    /// <summary>
    /// Provide thread safe access to the clipboard.
    /// </summary>
    public class Clip
    {
        /// <summary>
        /// This must be set to a control so clipboard functions can be invoked on the GUI thread.
        /// </summary>
        public static Control GuiThreadControl;

        int mClipSequence;
        bool mEverChanged;

        public Clip()
        {
            mClipSequence = GetClipSequence();
        }

        public bool EverChanged
        {
            get { return mEverChanged || Changed; }
        }

        public bool Changed
        {
            get
            {
                if (mClipSequence != GetClipSequence())
                {
                    mEverChanged = true;
                    return true;
                }
                return false;
            }
            set
            {
                mClipSequence = GetClipSequence() - (value ? 1 : 0);
            }
        }

        int GetClipSequence()
        {
            try
            {
                return GetClipboardSequenceNumber();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error getting clipboard sequence: " + ex.Message);
                return 0;
            }
        }

        public bool ContainsText()
        {
            var containsText = false;
            GuiThreadControl?.Invoke((MethodInvoker)(() => { containsText = Clipboard.ContainsText(); }));
            return containsText;
        }

        public bool ContainsFiles()
        {
            var containsFiles = false;
            GuiThreadControl?.Invoke((MethodInvoker)(() => { containsFiles = Clipboard.ContainsFileDropList(); }));
            return containsFiles;
        }

        public string GetText()
        {
            var text = "";
            GuiThreadControl?.Invoke((MethodInvoker)(() =>
            {
                if (Clipboard.ContainsText())
                    text = Clipboard.GetText();
            }));
            return text;
        }

        public string []GetFiles()
        {
            var files = new string[0];
            GuiThreadControl?.Invoke((MethodInvoker)(() =>
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var sc = Clipboard.GetFileDropList();
                    files = new string[sc.Count];
                    sc.CopyTo(files, 0);
                }
            }));
            return files;
        }

    }
}
