using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Text;
using static Gosub.Viewtop.NativeMethods;

namespace Gosub.Viewtop
{
    /// <summary>
    /// Windows terminal services
    /// </summary>
    public class Wts
    {
        public static int GetActiveConsoleSessionId()
        {
            return WTSGetActiveConsoleSessionId();
        }

        public static string GetSessionUserName(int sessionId)
        {
            return GetSessionString(sessionId, WTS_INFO_CLASS.UserName);
        }

        public static string GetSessionDomainName(int sessionId)
        {
            return GetSessionString(sessionId, WTS_INFO_CLASS.DomainName);
        }

        static string GetSessionString(int sessionId, WTS_INFO_CLASS info)
        {
            if (!WTSQuerySessionInformation(IntPtr.Zero, sessionId, info, out IntPtr bytes, out uint byteCount))
                return "";
            var infoString = Marshal.PtrToStringAnsi(bytes);
            WTSFreeMemory(bytes);
            return infoString;
        }

        /// <summary>
        /// Return the current desktop name
        /// Does not throw, returns a string containing $error if it doesn't work
        /// </summary>
        public static string GetDesktopName()
        {
            var hDesk = OpenInputDesktop(0, false, 0);
            if (hDesk == IntPtr.Zero)
                return "$error: GetDesktopName OpenInputDesktop, code=" + Marshal.GetLastWin32Error();

            const int UOI_NAME = 2;
            StringBuilder sb = new StringBuilder(1024);
            string desktop;
            if (GetUserObjectInformation(hDesk, UOI_NAME, sb, sb.Capacity, out int lengthNeeded))
                desktop = sb.ToString();
            else
                desktop = "$error: GetDesktopName GetUserObjectInformation, code=" + Marshal.GetLastWin32Error();

            CloseDesktop(hDesk);
            return desktop;
        }

    }
}

