using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;

using System.ServiceProcess;
using System.Management;
using System.Diagnostics;

namespace CNU.Service
{
    [RunInstaller(true)]
    public partial class Installer : System.Configuration.Install.Installer
    {
        static String[] EventLogSetting = { "CNUUpdSrvInst", "CNUUpdSIns" };
        bool bEventLog = false;

        public Installer()
        {
            InitializeComponent();
            InitializeMyEventLog();
        }

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);

            try
            {
                ServiceController scMyService = new ServiceController(this.serviceInstaller.ServiceName);
                scMyService.Start();
            }
            catch
            {
                if (bEventLog)  eventLog.WriteEntry("Failed to Start new service(), After Installing.", System.Diagnostics.EventLogEntryType.Error);
            }
        }

        protected override void OnBeforeUninstall(IDictionary savedState)
        {
            try
            {
                ServiceController scMyService = new ServiceController(this.serviceInstaller.ServiceName);
                scMyService.Stop();
            }
            catch
            {
                if (bEventLog)  eventLog.WriteEntry("Tried to Stop service(), Before Uninstalling.", System.Diagnostics.EventLogEntryType.Warning);
            }

            base.OnBeforeUninstall(savedState);
        }
        private void serviceProcessInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
            if (bEventLog)  eventLog.WriteEntry("In&Out serviceProcessInstaller1_AfterInstall()", System.Diagnostics.EventLogEntryType.Information);
        }
        private void serviceInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
            if (bEventLog)  eventLog.WriteEntry("In serviceInstaller1_AfterInstall()", System.Diagnostics.EventLogEntryType.SuccessAudit);
            SetDesktopInteractOptions();
            SetFailureRecoveryOptions();
            SetSeServiceLogonRight();
            if (bEventLog)  eventLog.WriteEntry("Out serviceInstaller1_AfterInstall()", System.Diagnostics.EventLogEntryType.Information);
        }

        private void SetSeServiceLogonRight()
        {
            RunAs.by.RaiseLogonRight();
        }

        private void InitializeMyEventLog()
        {
            try
            {
                if (!System.Diagnostics.EventLog.SourceExists(EventLogSetting[0]))
                    System.Diagnostics.EventLog.CreateEventSource(EventLogSetting[0], EventLogSetting[1]);

                eventLog.Source = EventLogSetting[0];
                eventLog.Log = EventLogSetting[1];
                bEventLog = true;
            }
            catch
            {
                bEventLog = false;
            }
        }
        private void SetFailureRecoveryOptions()
        {
            int exitCode;
            using (var process = new Process())
            {
                var startInfo = process.StartInfo;
                startInfo.FileName = "sc";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                // tell Windows that the service should restart if it fails
                startInfo.Arguments = string.Format("failure \"{0}\" reset= 0 actions= restart/5000/restart/5000/restart/5000", serviceInstaller.ServiceName);

                process.Start();
                process.WaitForExit();

                exitCode = process.ExitCode;

                process.Close();
            }

            if (exitCode != 0)
                throw new InvalidOperationException();
        }
        private void SetDesktopInteractOptions()
        {
            ConnectionOptions coOptions = new ConnectionOptions();
            coOptions.Impersonation = ImpersonationLevel.Impersonate;
            {
                ManagementScope mgmtScope = new ManagementScope(@"root\CIMV2", coOptions);
                mgmtScope.Connect();
                {
                    ManagementObject wmiService = new ManagementObject("Win32_Service.Name='" + serviceInstaller.ServiceName + "'");
                    ManagementBaseObject InParam = wmiService.GetMethodParameters("Change");
                    InParam["DesktopInteract"] = true;
                    ManagementBaseObject OutParam = wmiService.InvokeMethod("Change", InParam, null);
                }
            }
        }
    }
}
