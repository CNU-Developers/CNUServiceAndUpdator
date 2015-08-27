using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Threading;
using CNU.UpdateMGMTClass;
using System.Management;
using CNU.ProcessMGMTClass;
using System.Net;
using System.ServiceProcess;
using System.Security;

namespace CNU.Updater2
{
    public partial class Updater2 : Window
    {
        #region EventLog
        static String[] EventLogSetting = { "CNUUpdLog", "CNUUpdLog" };
        static Boolean bEventLog = false;
        static EventLog eventLog = new EventLog();
        static void EventLog(String evt, EventLogEntryType tpy)
        {
            if (bEventLog)
                eventLog.WriteEntry(evt, tpy);
            //Console.WriteLine(evt);
            //MessageBox.Show(evt);
        }
        static void EventLog(String evt)
        {
            EventLog(evt, EventLogEntryType.Information);
        }
        #endregion

        public Updater2()
        {
            InitializeComponent();
            ConsoleUpdater();
            Application.Current.Shutdown();
        }

        static void GetError(object sender, FailingEventArgs e)
        {
            // Error Handling Event입니다.
            // TODO: 아직 Failure Recovering까지는 구현하지 못했습니다.
            EventLog("GotError! from " + ((Package)sender).Name + " :: " + e.FailureReason, EventLogEntryType.Error);
        }

        void ConsoleUpdater()
        {
            List<ManualResetEvent> doneUninstallEvents = new List<ManualResetEvent>();
            List<ManualResetEvent> doneInstallEvents = new List<ManualResetEvent>();
            List<Package> toBeUninstall = new List<Package>();
            List<Package> toBeInstall = new List<Package>();

            #region EventLog Init
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
            #endregion
            #region 0. Get Path
            String Path = String.Empty;
            try
            {
                foreach (ManagementObject mo in (new ManagementClass("Win32_Service")).GetInstances())
                    if (mo.GetPropertyValue("Name").ToString() == "CNUService")
                    {
                        Path = System.IO.Path.GetDirectoryName(mo.GetPropertyValue("PathName").ToString().Trim('"'));
                        break;
                    }
                if (Path == String.Empty)
                    Path = AppDomain.CurrentDomain.BaseDirectory;
            }
            catch (Exception ex)
            {
                EventLog("Failed to get $SrvPath \n" + ex.ToString(), EventLogEntryType.Error);
            }
            Package.UpdatePath = Path;
            #endregion
            #region 0. 다른 업데이터가 떠있으면 자살.
            List<MyProcess> currentRunning = MyProcess.GetProcessList();
            {
                List<MyProcess> foundUpdater = currentRunning.FindAll(
                    delegate(MyProcess runner)
                    {
                        if (("CNUUpdater").Equals(runner.ProcessName))
                            return true;
                        return false;
                    }
                );
                if (foundUpdater.Count > 1)
                {
                    EventLog("0. 업데이트가 2개 이상 켜져있습니다. 자살합니다ㅠ.", EventLogEntryType.Error);
                    return;
                }
            }
            #endregion

            #region "1. 컴퓨터에 깔린 패키지 리스트를 불러옵니다. (못불러오면 그냥Die!)"
            EventLog("1. 컴퓨터에 깔린 패키지 리스트를 불러옵니다.", EventLogEntryType.SuccessAudit);
            List<Package> localPackage;
            try
            {
                #region #UNITTEST
                /*
                localPackage = new List<Package>();
                Package t1 = new Package();
                t1.Name = "Uninstall";  // Uninstall
                t1.Version = "1.0.0";
                //t1.ProductCode = "{27FA8657-6D2C-4917-990E-CE46C94D31B6}";
                localPackage.Add(t1);
                Package t2 = new Package();
                t2.Name = "Uninstall+Install";  // Uninstall(->Install)
                t2.Version = "2.0.0";
                localPackage.Add(t2);
                Package t3 = new Package();
                t3.Name = "SameVersion";  // nope
                t3.Version = "3.0.0";
                localPackage.Add(t3);
                */
                #endregion
                String packages = System.IO.Path.Combine(Path, "Packages.xml");
                EventLog(packages);
                localPackage = Package.ReadXML(packages);
            }
            catch (Exception ex)
            {
                EventLog("1. 컴퓨터에 깔린 패키지 리스트가 없습니다. " + ex.ToString(), EventLogEntryType.Error);
                return;
            }
            #endregion

            #region "2. 서버에 있는 최신 패키지 리스트를 불러옵니다. (못불러오면 그냥Die!)"
            EventLog("2. 서버에 있는 최신 패키지 리스트를 불러옵니다. ", EventLogEntryType.SuccessAudit);
            List<Package> remotePackage;
            try
            {
                #region #UNITTEST
                /*
                remotePackage = new List<Package>();
                Package t1 = new Package();
                t1.Name = "Install";  // Install
                t1.Version = "1.0.0";
                remotePackage.Add(t1);
                Package t2 = new Package();
                t2.Name = "Uninstall+Install";  // Uninstall(->Install)
                t2.Version = "3.0.0";
                remotePackage.Add(t2);
                Package t3 = new Package();
                t3.Name = "SameVersion";
                t3.Version = "3.0.0";
                remotePackage.Add(t3);
                 */
                #endregion
                EventLog(Package.UpdateServer);
                WebClient newClient = new WebClient();
                newClient.DownloadFile(Package.UpdateServer + "Latest.xml", System.IO.Path.Combine(Path, "Latest.xml"));
                remotePackage = Package.ReadXML(System.IO.Path.Combine(Path, "Latest.xml"));
            }
            catch (Exception ex)
            {
                EventLog("2. 서버에 있는 최신 패키지 리스트가 없습니다. \n" + ex.ToString(), EventLogEntryType.Error);
                return;
            }
            #endregion

            #region "3. 컴터에 깔려있는데, 서버엔 없어!! => UNINSTALL목록에 넣습니다."
            EventLog("3. 이미 깔려있는 패키지들을 불러옵니다.");
            foreach (Package lcl_one in localPackage)
            {
                #region "패키지가 실행중인지 확인."
                MyProcess foundRunning;
                if (
                    (
                        foundRunning = currentRunning.Find(
                            delegate(MyProcess runner)
                            {
                                if ((runner.ProcessName).Equals(System.IO.Path.GetFileNameWithoutExtension(lcl_one.Name)))
                                    return true;
                                return false;
                            }
                        )
                    ) != null
                )
                {
                    lcl_one.CurrentPID = foundRunning.Id;
                    EventLog("3. 현재 <" + lcl_one.Name + "> 패키지가 사용중(#" + lcl_one.CurrentPID + ")에 있습니다.", EventLogEntryType.Warning);
                }
                #endregion
                #region "삭제할 패키지가 있는지 확인."
                if (remotePackage.Find(
                        delegate(Package rmt_one)
                        {
                            if ((rmt_one.Name).Equals(lcl_one.Name))
                                return true;
                            return false;
                        }
                    ) == null
                )
                {
                    toBeUninstall.Add(lcl_one);

                    ManualResetEvent evt = new ManualResetEvent(false);
                    doneUninstallEvents.Add(evt);
                    lcl_one.SetEventHandlers(evt, null);

                    lcl_one.Error += new FailingEventHandler(GetError);

                    EventLog("3. 최신 목록에는 <" + lcl_one.Name + "> 패키지가 없으므로, 삭제할 예정입니다.", EventLogEntryType.Information);
                }
                #endregion
            }
            #endregion

            #region "4. 서버에만 있을경우 => INSTALL목록에 넣습니다. (업데이트도 INSTALL로).)"
            EventLog("4. 서버의 최신 패키지 리스트들을 불러옵니다.");
            foreach (Package latest_rmt_one in remotePackage)
            {
                Package found_lcl_one;
                if ((found_lcl_one = localPackage.Find(
                        delegate(Package lcl_one)
                        {
                            if ((lcl_one.Name).Equals(latest_rmt_one.Name))
                                return true;
                            return false;
                        }
                    )) != null
                )
                {
                    #region "이미 패키지가 깔려있을경우, 업데이트 해야하는지 확인."
                    if (Package.IsNeedToUpdateByComparePackages(found_lcl_one, latest_rmt_one))
                    {
                        toBeInstall.Add(latest_rmt_one);

                        ManualResetEvent evt2 = new ManualResetEvent(false);
                        doneInstallEvents.Add(evt2);
                        latest_rmt_one.SetEventHandlers(null, evt2);

                        latest_rmt_one.Error += new FailingEventHandler(GetError);

                        EventLog("4. 최신의 <" + latest_rmt_one.Name + "> 패키지로 업데이트할 예정입니다.", EventLogEntryType.Information);
                    }
                    else
                    {
                        EventLog("4. 이미 최신의 <" + latest_rmt_one.Name + "> 패키지를 사용합니다.", EventLogEntryType.Information);
                    }
                    #endregion
                }
                else
                {
                    #region "패키지가 안깔려있으니 설치해야함."
                    toBeInstall.Add(latest_rmt_one);
                    ManualResetEvent evt = new ManualResetEvent(false);
                    doneInstallEvents.Add(evt);
                    latest_rmt_one.SetEventHandlers(null, evt);
                    latest_rmt_one.Error += new FailingEventHandler(GetError);
                    EventLog("4. 새로운 <" + latest_rmt_one.Name + "> 패키지를 설치할 예정입니다.", EventLogEntryType.Information);
                    #endregion
                }
            }
            #endregion

            #region "5. 업데이트 해야하면 => CNUService를 끄고 계속 진행합니다. (아니면 그냥Die!)"
            ServiceController cnuService = null;
            if (toBeInstall.Count > 0 || toBeUninstall.Count > 0)
            {
                try
                {
                    cnuService = new ServiceController("CNUService");
                    cnuService.Stop();
                }
                catch (Exception ex)
                {
                    EventLog("5. 업데이트 해야하면 => CNUService가없어?!!!!뭐야? \n" + ex.ToString(), EventLogEntryType.Error);
                }
            }
            else
            {
                EventLog("5. 업데이트 할게없어요ㅋㅋ, 잘자요.", EventLogEntryType.SuccessAudit);
                System.IO.File.Delete(System.IO.Path.Combine(Path, "Latest.xml"));
                return;
            }
            #endregion

            #region "6. 업데이트를 위한 THREAD를 생성합니다. (UNINSTALL->INSTALL순서로 진행됩니다.)"
            foreach (Package to_uninstall in toBeUninstall)
                ThreadPool.QueueUserWorkItem(to_uninstall.UpdateCallback, null);
            foreach (Package to_install in toBeInstall)
                ThreadPool.QueueUserWorkItem(to_install.UpdateCallback, null);
            EventLog("6. 업데이트를 위한 THREAD를 생성합니다. (UNINSTALL->INSTALL순서로 진행됩니다.)", EventLogEntryType.Information);
            #endregion

            #region "7. UNINSTALL들이 다 됬는지 기달립니다. (INSTALL이 진행중일수도 있습니다)"
            EventLog("7. " + toBeUninstall.Count + "건의 UNINSTALL이 모두 끝나기를 기다릴게요.");
            // WaitHandle.WaitAll(doneUninstallEvents.ToArray());  // 으악! 이게 안됨!ㅠㅠ
            foreach (WaitHandle one in doneUninstallEvents)
                WaitHandle.WaitAny(new WaitHandle[] { one });


            EventLog("7. UNINSTALL이 모두 끝났습니다.", EventLogEntryType.SuccessAudit);
            #endregion

            #region "8. INSTALL들이 다 됬는지 기달립니다."
            EventLog("8. " + toBeInstall.Count + "건의 INSTALL이 모두 끝나기를 기다릴게요.");
            try
            {
                //WaitHandle.WaitAll(doneInstallEvents.ToArray());
                foreach (WaitHandle one in doneInstallEvents)
                    WaitHandle.WaitAny(new WaitHandle[] { one });
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString());
            }
            EventLog("8. INSTALL이 모두 끝났습니다.", EventLogEntryType.SuccessAudit);
            #endregion

            #region "9. Post Updater의 업데이트가 있을경우, 먼저 적용합니다."
            try
            {
                EventLog("9. Post Updater의 업데이트가 있을경우, 먼저 적용합니다.");
                String postupdater = "CNUPostUpdater2.exe";
                foreach (Package IfPost in toBeInstall)
                {
                    if ((IfPost.Name).Equals(postupdater))
                    {
                        EventLog("9. Post Updater의 업데이트가 있습니다.");
                        String dest = System.IO.Path.Combine(Path, postupdater);
                        System.IO.File.Delete(dest);
                        System.IO.File.Move(System.IO.Path.Combine(Path, "Updates", postupdater), dest);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog("9. Post Updater의 업데이트 도중 문제가 생겼습니다. \n" + ex.ToString(), EventLogEntryType.Error);
            }
            #endregion

            #region "10. Post Update로 역활을 넘깁니다."
            EventLog("10. Post Update로 역활을 넘깁니다.", EventLogEntryType.Information);
            try
            {
                // http://stackoverflow.com/questions/3891623/c-sharp-granting-log-on-as-service-permission-to-a-windows-user
                RunAs.by.AdminSliently(System.IO.Path.Combine(Path, "CNUPostUpdater2.exe"));
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventLogEntryType.Error);
            }
            #endregion
        }
    }
}
