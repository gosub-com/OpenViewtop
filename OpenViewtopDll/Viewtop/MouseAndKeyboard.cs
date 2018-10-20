using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using Gosub.Http;
using static Gosub.Viewtop.NativeMethods;

namespace Gosub.Viewtop
{
    /// <summary>
    /// NOTE: We should delay mouse events and play them back a little delayed,
    ///       but with relative timing preserved.  This would improve the chance
    ///       that multiple clicks are grouped properly to a double click event.
    ///       
    /// Need to ignore remote mouse events when the local user is dragging the mouse.
    /// </summary>
    public class MouseAndKeyboard
    {
        /// <summary>
        /// This must be set to a control so SendKeys can be invoked on the GUI thread.
        /// </summary>
        public static Control GuiThreadControl;

        const int MOUSEEVENTF_WHEEL = 0x800;
        const int SHIFT_CODE = 16;
        const int CTRL_CODE = 17;
        const int ALT_CODE = 18;

        bool mMouseOnScreen;
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

            var p = new Point((int)(scale * x), (int)(scale * y));
            mMouseOnScreen = p.X >= 0 && p.X < Screen.PrimaryScreen.Bounds.Width
                                && p.Y >= 0 && p.Y < Screen.PrimaryScreen.Bounds.Height;

            if (mMouseOnScreen)
                Cursor.Position = p;
        }

        void MouseButton(Action action, Button button)
        {
            if (!mMouseOnScreen)
                return;

            try
            {
                int flags = (int)button * (int)action;
                if (flags != 0)
                    mouse_event( flags, 0, 0, 0, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Write("Error sending mouse event: " + ex.Message);
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
                case 1: button = Button.Left; break;
                case 2: button = Button.Middle; break;
                case 3: button = Button.Right; break;
                default: return;
            }
            MouseButton(action, button);
        }

        public void MouseWheel(int delta)
        {
            if (!mMouseOnScreen)
                return;

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
                Log.Write("Error sending mouse event: " + ex.Message);
            }
        }


        // http://www.javascripter.net/faq/keycodes.htm
        static Dictionary<int, string> sKeyCodes = new Dictionary<int, string>()
        {
            { 8, "{BS}" },
            { 9, "{TAB}" },
            { 13, "{ENTER}" },
            { 19, "{BREAK}" },
            { 20, "{CAPSLOCK}" },
            { 27, "{ESC}" },
            { ' ', " " },
            { '!', "{PGUP}" },
            { '\"', "{PGDN}" },
            { '#', "{END}" },
            { '$', "{HOME}" },
            { '%', "{LEFT}" },
            { '&', "{UP}" },
            { '\'', "{RIGHT}" },
            { '(', "{DOWN}" },
            { ',', "{PRTSC}"},
            { '-', "{INSERT}" },
            { '.', "{DELETE}" },
            { ';', ";" }, // Firefox
            { '=', "=" }, // Firefox
            { 'p', "{F1}" },
            { 'q', "{F2}" },
            { 'r', "{F3}" },
            { 's', "{F4}" },
            { 't', "{F5}" },
            { 'u', "{F6}" },
            { 'v', "{F7}" },
            { 'w', "{F8}" },
            { 'x', "{F9}" },
            { 'y', "{F10}" },
            { 'z', "{F11}" },
            { '{', "{F12}" },
            { 144, "{NUMLOCK}" },
            { 145, "{SCROLLLOCK}" },
            { 173, "-" }, // Firefox
            { 186, ";" },
            { 187, "=" },
            { 189, "-" },
            { 188, "," },
            { 190, "." },
            { 191, "/" },
            { 192, "`" },
            { 219, "{[}" },
            { 220, "\\" },
            { 221, "{]}" },
            { 222, "'" }            
        };

        public void KeyPress(Action action, int code, bool shift, bool ctrl, bool alt)
        {
            // Since SendKeys doesn't have a way to send SHIFT/CTRL/ALT
            // use the windows SDK directly.  We need this for drag/drop.
            try
            {
                int downFlag = action == Action.Down ? 0 : 2;
                keybd_event(code, 0, downFlag, IntPtr.Zero);
                return; 
            }
            catch (Exception ex)
            {
                Log.Write("Error sending key via keybd_event: " + ex.Message);
            }

            if (action != Action.Down)
                return;

            // Encode SHIFT/CTRL/ALT
            StringBuilder sb = new StringBuilder();
            if (shift)
                sb.Append('+');
            if (ctrl)
                sb.Append('^');
            if (alt)
                sb.Append('%');

            // Convert to SendKeys code
            if (sKeyCodes.ContainsKey(code))
                sb.Append(sKeyCodes[code]);
            else if (char.IsLetterOrDigit((char)code))
                sb.Append(char.ToLower((char)code));
            else
            {
                // Unrecognized control
                Log.Write("Unrecognized code: " + code);
                return;
            }

            // NOTE: SendKeys needs to be called from the GUI thread
            GuiThreadControl?.Invoke((MethodInvoker)delegate 
            {
                try
                {
                    SendKeys.Send(sb.ToString());
                }
                catch (Exception ex)
                {
                    Log.Write("Error sending key viar SendKeys: " + ex.Message);
                }
            });
        }
    }
}
