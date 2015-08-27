using System;
using System.Diagnostics;
using System.ServiceProcess;

using System.IO;
using System.Threading;
using System.Management;

using CNU.ProcessMGMTClass;
using CNU.UpdateMGMTClass;
using System.Collections.Generic;

namespace CNU.Service
{
    public partial class CNUService : ServiceBase
    {
        //Ready
        #region "서비스의 기본: Init, Start & Stop명령."
        static ManualResetEvent _ServiceShutdownRequestEvent = new ManualResetEvent(false);
        static String[] EventLogSetting = { "CNUServLog", "CNUServLog" };
        static Boolean bEventLog = false, bReadyToStart = false;
        static String SrvPath = String.Empty;
        Thread[] _thread;

        public CNUService()
        {
            InitializeComponent();
            InitializeMyEventLog();
        }

        protected override void OnStart(string[] args)
        {
            this.RequestAdditionalTime(6000);

            if(bEventLog)
                this.eventLog.WriteEntry("In OnStart()", EventLogEntryType.SuccessAudit);

            this.TryMyDebug();
            // _thread;
            {
                this._thread = new Thread[2];
                this._thread[0] = new Thread(this.IsReadyToStart_ThreadFunc);
                this._thread[0].IsBackground = true;
                this._thread[0].Start();
            }
            Thread MainService = new Thread(this.MainService_ThreadFunc);
            MainService.Start();

            if(bEventLog)
                this.eventLog.WriteEntry("Out OnStart()", EventLogEntryType.Information);
        }
        protected override void OnStop()
        {
            if (bEventLog)
                this.eventLog.WriteEntry("In OnStop()", EventLogEntryType.SuccessAudit);

            _ServiceShutdownRequestEvent.Set();
            if (!this._thread[0].Join(500))
                this._thread[0].Abort();

            if (bEventLog)
                this.eventLog.WriteEntry("Out OnStop()", EventLogEntryType.Information);
        }

        private void InitializeMyEventLog()
        {
            try
            {
                if (!System.Diagnostics.EventLog.SourceExists(EventLogSetting[0]))
                    System.Diagnostics.EventLog.CreateEventSource(EventLogSetting[0], EventLogSetting[1]);

                this.eventLog.Source = EventLogSetting[0];
                this.eventLog.Log = EventLogSetting[1];
                bEventLog = true;
            }
            catch (Exception ex)
            {
                bEventLog = false;
                System.Console.WriteLine(ex.ToString());  // huh?
            }
        }

        protected String GetPath()
        {
            if (SrvPath == String.Empty)
                try
                {
                    foreach (ManagementObject mo in (new ManagementClass("Win32_Service")).GetInstances())
                        if (mo.GetPropertyValue("Name").ToString() == this.ServiceName)
                        {
                            SrvPath = Path.GetDirectoryName(mo.GetPropertyValue("PathName").ToString().Trim('"'));
                            break;
                        }
                    if (SrvPath != String.Empty)
                    {
                        if (bEventLog)
                            this.eventLog.WriteEntry("GetPath() : " + SrvPath, EventLogEntryType.SuccessAudit);
                    }
                    else
                        throw new Exception("No Service.");
                }
                catch (Exception ex)
                {
                    if (bEventLog)
                        this.eventLog.WriteEntry("Failed to get $SrvPath \n" + ex.ToString(), EventLogEntryType.Error);
                }
            return SrvPath;
        }

        [Conditional("DEBUG")]
        private void TryMyDebug()
        {
            try
            {
                if (bEventLog)
                    this.eventLog.WriteEntry("In TryDebug()", EventLogEntryType.Warning);
                System.Diagnostics.Debugger.Launch();
                if (bEventLog)
                    this.eventLog.WriteEntry("Out TryDebug()", EventLogEntryType.Warning);
            }
            catch (Exception ex)
            {
                if (bEventLog)
                    this.eventLog.WriteEntry(ex.ToString());
            }
            return;
        }
        #endregion

        //Set
        #region "서비스가 동작할 수 있을정도로 부팅이 되었는지 확인하는 함수들."
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);
            if (bEventLog)
                this.eventLog.WriteEntry("At OnSessionChange() : Got <" + changeDescription.Reason.ToString() + ">", EventLogEntryType.Information);
            if (changeDescription.Reason == SessionChangeReason.SessionLogon)
                bReadyToStart = true;
        }
        private void IsReadyToStart_ThreadFunc()
        {
            bool found = false;
            if (bEventLog)
                this.eventLog.WriteEntry("We missed the message about successfully logged-on.", EventLogEntryType.Information);
            while (!found && !bReadyToStart)
            {
                Thread.Sleep(1000);
                foreach (MyProcess s in MyProcess.GetProcessList())
                {
                    if (s.ProcessName.Equals("explorer"))
                    {
                        found = true;
                        bReadyToStart = true;
                        break;
                    }
                }
                if (!found)
                    if (bEventLog)
                        this.eventLog.WriteEntry("Failed to find 'explorer.exe', Maybe trying to upgrade?", EventLogEntryType.FailureAudit);
            }
            if (bEventLog)
                this.eventLog.WriteEntry("Now, Ready to Go!", EventLogEntryType.SuccessAudit);
            return;
        }
        #endregion

        //Go
        #region "서비스가 할 동작들."
        private void MainService_ThreadFunc()
        {
            if (bEventLog) this.eventLog.WriteEntry("Ready !!!!!  @ MainService_ThreadFunc()");
            while (!bReadyToStart)
                Thread.Sleep(2000);
            if (bEventLog) this.eventLog.WriteEntry("Set !!!!!  @ MainService_ThreadFunc()");

            Boolean OnceOnly = false;

            while (!_ServiceShutdownRequestEvent.WaitOne(0))
            {
                if (bEventLog) this.eventLog.WriteEntry("Go !!!!!   @ MainService_ThreadFunc()");
                Thread.Sleep(60 * 1000);
                try
                {
                    #region "1. 로컬 패키지 리스트를 받아옵니다."
                    {
                        List<Package> localPackage = Package.ReadXML(System.IO.Path.Combine(this.GetPath(), "Packages.xml"));
                        // "2. 패키지 리스트에서 실행해야할 파일을 뽑아냅니다."
                        List<MyProcess> currentRunProcess = MyProcess.GetProcessList();

                        foreach (Package i in localPackage)
                        {
                            MyProcess foundRunning;
                            if (
                                (
                                    foundRunning = currentRunProcess.Find(
                                        delegate(MyProcess runner)
                                        {
                                            if ((runner.ProcessName).Equals(System.IO.Path.GetFileNameWithoutExtension(i.Name)))
                                                return true;
                                            return false;
                                        }
                                    )
                                ) == null
                            )
                            {
                                if (i.toRun)
                                {
                                    // "3. 조건에 해당하면 돌립시다."
                                    if ((i.RunOnce == false) || ((i.RunOnce == true) && (OnceOnly == false)))
                                    {
                                        uint pid = uint.MaxValue;
                                        String url = System.IO.Path.Combine(this.GetPath(), i.Name);
                                        if (i.RunAs != null && i.RunAs.Equals("Administrator"))
                                        {
                                            pid = RunAs.by.AdminSliently(this.GetPath() + "\\" + i.Name);
                                            //Thread s = new Thread(Administrator_ThreadFunc);
                                            //s.Start((object)url);
                                        }
                                        else
                                        {
                                            pid = RunAs.by.User(url);
                                        }
                                        if (bEventLog)
                                            this.eventLog.WriteEntry(i.Name + " : Running at #" + pid.ToString() + "\n" + url, EventLogEntryType.SuccessAudit);
                                    }
                                }
                            }
                            else
                            {
                                if (bEventLog)
                                    this.eventLog.WriteEntry(i.Name + " : Already Running at #" + foundRunning.Id.ToString(), EventLogEntryType.Information);
                            }
                        }
                    }
                    OnceOnly = true;
                    #endregion
                }
                catch (Exception ex)
                {
                    if (bEventLog)  this.eventLog.WriteEntry("MainService_ThreadFunc() : ERROR!\n" + ex.ToString(), EventLogEntryType.Error);
                }
            }
        }
        
        private void Administrator_ThreadFunc(object path)
        {
            CNU.RunAs.by.AdminSliently((string)path);
        }
        
        #endregion
    }
}
