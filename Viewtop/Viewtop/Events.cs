using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;

namespace Gosub.Viewtop
{
    /// <summary>
    /// NOTE: We should delay mouse events and play them back a little delayed,
    ///       but with relative timing preserved.  This would improve the chance
    ///       that multiple clicks are grouped properly to a double click event.
    ///       
    /// Need to ignore remote mouse events when the local user is dragging the mouse.
    /// </summary>
    class Events
    {
        const int MOUSEEVENTF_WHEEL = 0x800;

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

        long mMouseMoveTime;
        int mMouseX;
        int mMouseY;

        public enum Button
        {
            Left = 2,
            Right = 8,
            Middle = 0x20,
        }
        public enum Action
        {
            Down = 1,
            Up = 2
        }

        public void SetMousePosition(long remoteTime, double scale, int x, int y, bool force)
        {
            if (!force)
            {
                if (remoteTime <= mMouseMoveTime)
                    return;
                if (x == mMouseX && y == mMouseY)
                    return;
            }
            mMouseMoveTime = remoteTime;
            mMouseX = x;
            mMouseY = y;
            Cursor.Position = new Point((int)(scale * x), (int)(scale * y));
        }

        public void MouseButton(Action action, Button button)
        {
            try
            {
                int flags = (int)button * (int)action;
                if (flags != 0)
                    mouse_event( flags, 0, 0, 0, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error sending mouse event: " + ex.Message);
            }
        }

        /// <summary>
        /// Generate mouse event from standard browser event
        /// </summary>
        public void MouseButton(Action action, int which)
        {
            Button button;
            switch (which)
            {
                case 1: button = Events.Button.Left; break;
                case 2: button = Events.Button.Middle; break;
                case 3: button = Events.Button.Right; break;
                default: return;
            }
            MouseButton(action, button);
        }

        public void MouseWheel(int delta)
        {
            if (delta >= 0)
                delta = 120;
            else
                delta = -120;

            try
            {
                mouse_event(MOUSEEVENTF_WHEEL, 0, 0, delta, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error sending mouse event: " + ex.Message);
            }
        }

        public void KeyPress(int code, int ch, bool shift, bool ctrl, bool alt)
        {
            // TBD: Still need to implement all the special keys (up, down, backspace, etc.)
            StringBuilder sb = new StringBuilder();
            if (shift)
                sb.Append('+');
            if (ctrl)
                sb.Append('^');
            if (alt)
                sb.Append('%');
            sb.Append((char)ch);

            // NOTE: SendKeys needs to be called from the GUI thread
            Application.OpenForms[0].Invoke((MethodInvoker)delegate { SendKeys.Send(sb.ToString()); });
        }
    }
}
