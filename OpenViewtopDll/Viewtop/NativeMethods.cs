using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Gosub.Viewtop
{
    [SuppressUnmanagedCodeSecurity]
    class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetClipboardSequenceNumber();

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void keybd_event(int bVk, int bScan, int dwFlags, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CreatePipe(ref IntPtr hReadPipe, ref IntPtr hWritePipe, ref SECURITY_ATTRIBUTES security, int bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess ();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(IntPtr handle, int exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr handle, ref int exitCode);

        [DllImport("wtsapi32.dll")]
        public static extern IntPtr WTSOpenServer([MarshalAs(UnmanagedType.LPStr)] String pServerName);

        [DllImport("wtsapi32.dll")]
        public static extern IntPtr WTSOpenServer(IntPtr hServer);

        [DllImport("wtsapi32.dll")]
        public static extern void WTSCloseServer(IntPtr hServer);

        [DllImport("kernel32.dll")]
        public static extern int WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSEnumerateSessions(IntPtr hServer, int Reserved, int Version, ref IntPtr ppSessionInfo, ref int pCount);

        [DllImport("wtsapi32.dll")]
        public static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSQuerySessionInformation(
            IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out uint pBytesReturned);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSQueryUserToken(int sessionId, out IntPtr ppBuffer);

        [DllImport("advapi32.dll",
              EntryPoint = "CreateProcessAsUser", SetLastError = true,
              CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern bool
        CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine,
                            ref SECURITY_ATTRIBUTES lpProcessAttributes, ref SECURITY_ATTRIBUTES lpThreadAttributes,
                            bool bInheritHandle, int dwCreationFlags, IntPtr lpEnvrionment,
                            string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
                            ref PROCESS_INFORMATION lpProcessInformation);

        [DllImport("advapi32.dll", EntryPoint = "OpenProcessToken", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr processHandle, int desiredAccess, ref IntPtr tokenHandle);

        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx", SetLastError = true)]
        public static extern bool DuplicateTokenEx(IntPtr hExistingToken, int dwDesiredAccess,
                           ref SECURITY_ATTRIBUTES lpThreadAttributes,
                           int ImpersonationLevel, int dwTokenType,
                           ref IntPtr phNewToken);

        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx", SetLastError = true)]
        public static extern bool DuplicateTokenEx(IntPtr hExistingToken, int dwDesiredAccess,
                           IntPtr lpThreadAttributes,
                           int ImpersonationLevel, int dwTokenType,
                           ref IntPtr phNewToken);

        [DllImport("advapi32.dll", EntryPoint = "SetTokenInformation", SetLastError = true)]
        public static extern bool SetTokenInformation(IntPtr tokenHandle, TOKEN_INFORMATION_CLASS tokenInformationClass,
                           ref int lpThreadAttributes, int sizeofInt);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int StructSize;
            public int Flags;
            public IntPtr Cursof;
            public POINT Position;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SESSION_INFO
        {
            public Int32 SessionID;
            [MarshalAs(UnmanagedType.LPStr)]
            public String StationName;
            public int State;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public int StructSize;
            public string Reserved;
            public string Desktop;
            public string Title;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public int XCountChars;
            public int YCountChars;
            public int FillAttribute;
            public int Flags;
            public short ShowWindow;
            public short Reserved2;
            public IntPtr Reserved3;
            public IntPtr StdInput;
            public IntPtr StdOutput;
            public IntPtr StdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr Process;
            public IntPtr Thread;
            public int ProcessID;
            public int ThreadID;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int StructSize;
            public IntPtr SecurityDescriptor;
            public bool InheritHandle;
        }

        public enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        public enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }

        public enum WTS_INFO_CLASS
        {
            InitialProgram,
            ApplicationName,
            WorkingDirectory,
            OEMId,
            SessionId,
            UserName,
            WinStationName,
            DomainName,
            ConnectState,
            ClientBuildNumber,
            ClientName,
            ClientDirectory,
            ClientProductId,
            ClientHardwareId,
            ClientAddress,
            ClientDisplay,
            ClientProtocolType
        }

        public enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            TokenElevationType,
            TokenLinkedToken,
            TokenElevation,
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            TokenIsAppContainer,
            TokenCapabilities,
            TokenAppContainerSid,
            TokenAppContainerNumber,
            TokenUserClaimAttributes,
            TokenDeviceClaimAttributes,
            TokenRestrictedUserClaimAttributes,
            TokenRestrictedDeviceClaimAttributes,
            TokenDeviceGroups,
            TokenRestrictedDeviceGroups,
            TokenSecurityAttributes,
            TokenIsRestricted,
            MaxTokenInfoClass
        }

    }
}
