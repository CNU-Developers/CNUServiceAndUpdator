using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CNU.RunAs.Alejacma;
using System.Runtime.InteropServices;
using CNU.RunAs.Csharpgeneral;

namespace CNU.RunAs.Minho
{
    public class UsingCreateProcessAsUser
    {
        // http://calebdelnay.com/blog/2012/01/displaying-a-program-on-the-windows-secure-desktop .
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool SetTokenInformation(
            IntPtr TokenHandle,
            TOKEN_INFORMATION_CLASS TokenInformationClass,
            ref UInt32 TokenInformation,
            UInt32 TokenInformationLength);

        public enum TOKEN_INFORMATION_CLASS : int
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
            MaxTokenInfoClass
        };

        [DllImport("kernel32.dll")]
        public static extern uint WTSGetActiveConsoleSessionId();

        // Following http://blogs.msdn.com/b/winsdk/archive/2009/07/14/launching-an-interactive-process-from-windows-service-in-windows-vista-and-later.aspx .
        public static uint RunIt(String strCommand, String strDomain, String strName, String strPassword)
        {
            IntPtr hToken = IntPtr.Zero;
            uint pid = uint.MaxValue;
            try
            {
                Boolean result = Win32ProcessCall.LogonUser(strName, strDomain, strPassword, Win32ProcessCall.LogonType.LOGON32_LOGON_INTERACTIVE, Win32ProcessCall.LogonProvider.LOGON32_PROVIDER_DEFAULT, out hToken);
                if (!result) { throw new Exception("Logon error #" + Marshal.GetLastWin32Error()); }
                UInt32 dwSessionId = WTSGetActiveConsoleSessionId();
                IntPtr newToken = IntPtr.Zero;
                //http://stackoverflow.com/questions/3128017/possible-to-launch-a-process-in-a-users-session-from-a-service

                CNU.RunAs.Alejacma.Win32ProcessCall.STARTUPINFO startInfo = new CNU.RunAs.Alejacma.Win32ProcessCall.STARTUPINFO();
                startInfo.cb = Marshal.SizeOf(startInfo);
                IntPtr envBlock = ProcessAsUser.GetEnvironmentBlock(hToken);
                pid = ProcessAsUser.LaunchProcessAsUserPid(strCommand, hToken, envBlock);
                if (envBlock != IntPtr.Zero)
                    ProcessAsUser.DestroyEnvironmentBlock(envBlock);

            }
            finally
            {
                Win32ProcessCall.CloseHandle(hToken);
            }
            return pid;
        }
    }
}
