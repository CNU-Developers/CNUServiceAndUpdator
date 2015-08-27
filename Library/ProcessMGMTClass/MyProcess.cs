using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;

namespace CNU.ProcessMGMTClass
{
    public class MyProcess
    {
        private Process link;
        public int Id;
        public String ProcessName, UserName, FileName;

        public static List<MyProcess> GetProcessList()
        {
            List<MyProcess> result = new List<MyProcess>(); ;
            foreach (Process s in Process.GetProcesses()){
                MyProcess This = new MyProcess();
                This.link = s;
                try
                {
                    This.Id = s.Id;
                }
                catch { }
                try
                {
                    This.ProcessName = s.ProcessName;
                }
                catch { }
                try
                {
                    This.UserName = s.StartInfo.UserName;
                }
                catch { }
                try
                {
                    This.FileName = s.MainModule.FileName;
                }
                catch { }
                result.Add(This);
            }
            return result;
        }
        public static bool KillProcess(string processName)
        {
            foreach (Process s in Process.GetProcesses())
            {
                if (s.ProcessName.CompareTo(processName) == 0)
                {
                    try
                    {
                        s.Kill();
                    }
                    catch
                    {
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }
        public static bool KillProcess(int processId)
        {
            foreach (Process s in Process.GetProcesses())
            {
                if (s.Id.CompareTo(processId) == 0)
                {
                    try
                    {
                        s.Kill();
                    }
                    catch
                    {
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
