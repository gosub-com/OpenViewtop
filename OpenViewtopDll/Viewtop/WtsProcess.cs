using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using static Gosub.Viewtop.NativeMethods;
using Gosub.Http;

namespace Gosub.Viewtop
{
    public class WtsProcess : IDisposable
    {
        IntPtr mProcess;
        IntPtr mThread;
        int mSessionId;
        string mUserName;
        string mDomainName;

        public int SessionId => mSessionId;
        public string DomainName => mDomainName;
        public string UserName => mUserName;

        private WtsProcess()
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

        public unsafe static WtsProcess StartAsUser(
            int sessionId,
            bool runAsSystem,
            string filename,
            string args = "",
            string desktop = "Default")
        {
            const int MAXIMUM_ALLOWED = 0x2000000;
            const int TOKEN_DUPLICATE = 2;

            var winStation = "winsta0\\" + desktop;

            IntPtr tokenHandle = IntPtr.Zero;
            if (runAsSystem)
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_DUPLICATE, ref tokenHandle))
                    throw new Win32Exception("OpenProcessToken: " + Marshal.GetLastWin32Error());
            }
            else
            {
                // NOTE: We could convert this to admin token using GetTokenInformation with TokenLinkedToken
                if (!WTSQueryUserToken(sessionId, ref tokenHandle))
                    throw new Win32Exception("WTSQueryUserToken: " + Marshal.GetLastWin32Error());
            }

            // Duplicate the handle so it can be modified
            var dupedToken = IntPtr.Zero;
            if (!DuplicateTokenEx(
                    tokenHandle,
                    MAXIMUM_ALLOWED,
                    IntPtr.Zero,
                    (int)SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    (int)TOKEN_TYPE.TokenPrimary,
                    ref dupedToken))
            {
                CloseHandle(tokenHandle);
                throw new Win32Exception("DuplicateTokenEx: " + Marshal.GetLastWin32Error());
            }
            CloseHandle(tokenHandle);

            IntPtr environment = IntPtr.Zero;
            if (runAsSystem)
            {
                // Run the process in the correct session
                if (!SetTokenInformation(dupedToken, TOKEN_INFORMATION_CLASS.TokenSessionId, ref sessionId, sizeof(int)))
                {
                    CloseHandle(dupedToken);
                    throw new Win32Exception("SetTokenInformation TokenSessionId: " + Marshal.GetLastWin32Error());
                }

                // Allow access to login screen, task manager, and other protected screens
                int uiAccess = 1;
                if (!SetTokenInformation(dupedToken, TOKEN_INFORMATION_CLASS.TokenUIAccess, ref uiAccess, sizeof(int)))
                {
                    CloseHandle(dupedToken);
                    throw new Win32Exception("SetTokenInformation TokenUIAccess: " + Marshal.GetLastWin32Error());
                }
            }
            else
            {
                // Get the environment block when running as a user
                if (!CreateEnvironmentBlock(ref environment, dupedToken, false))
                    Log.Write("CreateEnvironmentBlock: " + Marshal.GetLastWin32Error());
            }

            var pi = new PROCESS_INFORMATION();
            var si = new STARTUPINFO();
            si.StructSize = Marshal.SizeOf(si);
            si.Desktop = winStation;

            const int NORMAL_PRIORITY_CLASS = 0x00000020;
            const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
            var userName = Wts.GetSessionUserName(sessionId);
            var domainName = Wts.GetSessionDomainName(sessionId);
            if (!CreateProcessAsUser(dupedToken, IntPtr.Zero,
                    string.Format("\"{0}\" {1}", filename.Replace("\"", "\"\""), args),
                    IntPtr.Zero, IntPtr.Zero,
                    false, NORMAL_PRIORITY_CLASS | CREATE_UNICODE_ENVIRONMENT,
                    environment, IntPtr.Zero, ref si, ref pi))
            {
                CloseHandle(dupedToken);
                DestroyEnvironmentBlock(environment);
                Log.Write("CreateProcessAsUser: " + Marshal.GetLastWin32Error());
                throw new Win32Exception("CreateProcessAsUser: " + Marshal.GetLastWin32Error());
            }

            CloseHandle(dupedToken);
            DestroyEnvironmentBlock(environment);

            var process = new WtsProcess
            {
                mProcess = pi.Process,
                mThread = pi.Thread,
                mSessionId = sessionId,
                mDomainName = domainName,
                mUserName = userName
            };
            return process;
        }
    }
}
