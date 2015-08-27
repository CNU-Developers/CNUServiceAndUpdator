using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using CNU.RunAs.Csharpgeneral;
using System.Runtime.InteropServices;

namespace CNU.RunAs.Minho
{
    public class UsingStolenExplorersToken
    {
        public static uint Launch(string appCmdLine)
        {
            bool fail = false;
            uint result = uint.MaxValue;

            //Either specify the processID explicitly 
            //Or try to get it from a process owned by the user. 
            //In this case assuming there is only one explorer.exe 

            Process[] ps = Process.GetProcessesByName("explorer");
            int processId = -1;//=processId 
            if (ps.Length > 0)
            {
                processId = ps[0].Id;
            }

            if (processId > 1)
            {
                IntPtr token = ProcessAsUser.GetPrimaryToken(processId);

                if (token != IntPtr.Zero)
                {

                    IntPtr envBlock = ProcessAsUser.GetEnvironmentBlock(token);
                    result = LaunchProcessAsUser(appCmdLine, token, envBlock);
                    if (result == uint.MaxValue)
                        fail = true;
                    if (envBlock != IntPtr.Zero)
                        ProcessAsUser.DestroyEnvironmentBlock(envBlock);

                    ProcessAsUser.CloseHandle(token);
                }

            }
            return fail ? uint.MaxValue : result;
        }

        public static uint LaunchProcessAsUser(string cmdLine, IntPtr token, IntPtr envBlock)
        {
            bool result = false;


            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            SECURITY_ATTRIBUTES saProcess = new SECURITY_ATTRIBUTES();
            SECURITY_ATTRIBUTES saThread = new SECURITY_ATTRIBUTES();
            saProcess.nLength = (uint)Marshal.SizeOf(saProcess);
            saThread.nLength = (uint)Marshal.SizeOf(saThread);

            STARTUPINFO si = new STARTUPINFO();
            si.cb = (uint)Marshal.SizeOf(si);

            si.lpDesktop = @"WinSta0\Default"; //Modify as needed 
            si.dwFlags = ProcessAsUser.STARTF_USESHOWWINDOW | ProcessAsUser.STARTF_FORCEONFEEDBACK;
            si.wShowWindow = ProcessAsUser.SW_SHOW;
            //Set other si properties as required. 

            result = ProcessAsUser.CreateProcessAsUser(
                token,
                null,
                cmdLine,
                ref saProcess,
                ref saThread,
                false,
                ProcessAsUser.CREATE_UNICODE_ENVIRONMENT,
                envBlock,
                null,
                ref si,
                out pi);


            if (result == false)
            {
                int error = Marshal.GetLastWin32Error();
                string message = String.Format("CreateProcessAsUser Error: {0}", error);
                Debug.WriteLine(message);

            }

            return result ? pi.dwProcessId : uint.MaxValue;
        }
    }
}
