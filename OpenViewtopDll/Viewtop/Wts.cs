using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Forms;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Security.AccessControl;
using static Gosub.Viewtop.NativeMethods;

namespace Gosub.Viewtop
{
    /// <summary>
    /// Windows terminal services
    /// </summary>
    public class Wts
    {
        public enum SessionState
        {
            // See WTS_CONNECTSTATE_CLASS
            Active,
            Connected,
            ConnectQuery,
            Shadow,
            Disconnected,
            Idle,
            Listen,
            Reset,
            Down,
            Init
        }

        public class Session
        {
            public int SessionID;
            public SessionState State;
            public string StationName;
            public string DomainName;
            public string UserName;
        }

        class SessionInfo
        {
            public List<Session> Sessions;
            public int ActiveSessionId;
        }

        public static int GetActiveConsoleSessionId()
        {
            return WTSGetActiveConsoleSessionId();
        }

        public static object GetSessions()
        {
            try
            {
                var si = new SessionInfo();
                si.Sessions = GetSessions("");
                si.ActiveSessionId = WTSGetActiveConsoleSessionId();
                return si;
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        static List<Session> GetSessions(string serverName)
        {
            IntPtr serverHandle = serverName == "" ? WTSOpenServer(IntPtr.Zero) : WTSOpenServer(serverName);
            try
            {
                IntPtr sessionInfoPtr = IntPtr.Zero;
                Int32 sessionCount = 0;
                if (!WTSEnumerateSessions(serverHandle, 0, 1, ref sessionInfoPtr, ref sessionCount))
                    throw new Win32Exception("WTSEnumerateSessions: " + Marshal.GetLastWin32Error());

                IntPtr currentSession = sessionInfoPtr;
                var users = new List<Session>();
                for (int i = 0; i < sessionCount; i++)
                {
                    SESSION_INFO si = (SESSION_INFO)Marshal.PtrToStructure(currentSession, typeof(SESSION_INFO));
                    currentSession += Marshal.SizeOf(typeof(SESSION_INFO));

                    WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.UserName, out IntPtr userPtr, out uint bytes);
                    WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.DomainName, out IntPtr domainPtr, out bytes);

                    var user = new Session
                    {
                        SessionID = si.SessionID,
                        State = (SessionState)si.State,
                        StationName = si.StationName,
                        DomainName = Marshal.PtrToStringAnsi(domainPtr),
                        UserName = Marshal.PtrToStringAnsi(userPtr)
                    };
                    users.Add(user);
                    WTSFreeMemory(userPtr);
                    WTSFreeMemory(domainPtr);
                }
                WTSFreeMemory(sessionInfoPtr);
                return users;
            }
            finally
            {
                WTSCloseServer(serverHandle);
            }
        }

        public class Process : IDisposable
        {
            IntPtr mProcess;
            IntPtr mThread;

            Process()
            {
            }

            public void Dispose()
            {
                if (mThread != IntPtr.Zero)
                    CloseHandle(mThread);
                if (mThread != IntPtr.Zero)
                    CloseHandle(mProcess);
                mThread = IntPtr.Zero;
                mProcess = IntPtr.Zero;
            }

            /// <summary>
            /// Use this only as a last resort since it's an un-graceful exit
            /// </summary>
            public void Terminate(int exitCode)
            {
                if (mProcess != IntPtr.Zero)
                    TerminateProcess(mProcess, exitCode);
            }

            public bool IsRunning
            {
                get
                {
                    if (mProcess == IntPtr.Zero)
                        return false;
                    int exitCode = 0;
                    if (!GetExitCodeProcess(mProcess, ref exitCode))
                        return false;
                    const int STILL_ACTIVE = 259;
                    return exitCode == STILL_ACTIVE;
                }
            }

            public unsafe static Process CreateProcessAsUser(int sessionId, string filename, string args)
            {
                const int GENERIC_ALL_ACCESS = 0x10000000;
                const int TOKEN_DUPLICATE = 2;

                IntPtr tokenHandle = IntPtr.Zero;
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_DUPLICATE, ref tokenHandle))
                    throw new Win32Exception("OpenProcessToken: " + Marshal.GetLastWin32Error());

                var hDupedToken = IntPtr.Zero;
                if (!DuplicateTokenEx(
                        tokenHandle,
                        GENERIC_ALL_ACCESS,
                        IntPtr.Zero,
                        (int)SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                        (int)TOKEN_TYPE.TokenPrimary,
                        ref hDupedToken))
                {
                    CloseHandle(tokenHandle);
                    throw new Win32Exception("DuplicateTokenEx: " + Marshal.GetLastWin32Error());
                }

                if (!SetTokenInformation(hDupedToken, TOKEN_INFORMATION_CLASS.TokenSessionId, ref sessionId, sizeof(int)))
                {
                    CloseHandle(tokenHandle);
                    CloseHandle(hDupedToken);
                    throw new Win32Exception("SetTokenInformation: " + Marshal.GetLastWin32Error());
                }

                // TBD: Enable this after the executable is signed
                //int uiAccess = 1;
                //if (!SetTokenInformation(hDupedToken, TOKEN_INFORMATION_CLASS.TokenUIAccess, ref uiAccess, sizeof(int)))
                //{
                //    CloseHandle(tokenHandle);
                //    CloseHandle(hDupedToken);
                //    throw new Win32Exception("SetTokenInformation: " + Marshal.GetLastWin32Error());
                //}

                var pi = new PROCESS_INFORMATION();
                var sa = new SECURITY_ATTRIBUTES();
                sa.StructSize = Marshal.SizeOf(sa);

                var si = new STARTUPINFO();
                si.StructSize = Marshal.SizeOf(si);
                si.Desktop = "winsta0\\default";

                var path = Path.GetFullPath(filename);
                var dir = Path.GetDirectoryName(path);

                if (!NativeMethods.CreateProcessAsUser(hDupedToken, path,
                                    string.Format("\"{0}\" {1}", filename.Replace("\"", "\"\""), args),
                                    ref sa, ref sa, false, 0, IntPtr.Zero, dir, ref si, ref pi))
                {
                    CloseHandle(tokenHandle);
                    CloseHandle(hDupedToken);
                    throw new Win32Exception("CreateProcessAsUser: " + Marshal.GetLastWin32Error());
                }

                CloseHandle(tokenHandle);
                CloseHandle(hDupedToken);

                var process = new Process
                {
                    mProcess = pi.Process,
                    mThread = pi.Thread,
                };
                return process;
            }
        }
    }
}

