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
using System.Management;
using System.ServiceProcess;
using System.Threading;

namespace CNU.Updater2.Post
{
    public partial class PostUpdater2 : Window
    {
        #region EventLog
        static String[] EventLogSetting = { "CNUPUpLog", "CNUPUpLog" };
        static Boolean bEventLog = false;
        static EventLog eventLog = new EventLog();
        static void EventLog(String evt, EventLogEntryType tpy)
        {
            if (bEventLog)
                eventLog.WriteEntry(evt, tpy);
            //Console.WriteLine(evt);
        }
        static void EventLog(String evt)
        {
            EventLog(evt, EventLogEntryType.Information);
        }
        #endregion

        public PostUpdater2()
        {
            InitializeComponent();
        }

        void PostUpdaterDoing()
        {   
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
            #endregion

            #region "10. 역활을받고 11. 업데이트를 덮어씁시다!"
            try
            {
                EventLog("10. Post Update가 역활을 받았습니다.", EventLogEntryType.SuccessAudit);
                foreach (String filename in System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(Path, "Updates")))
                {
                    EventLog("11. 업데이트를 적용합니다. : " + filename, EventLogEntryType.Information);
                    try
                    {
                        String dest = System.IO.Path.Combine(
                                Path,
                                filename.Substring(System.IO.Path.Combine(Path, "Updates").Length + 1)
                            );
                        System.IO.File.Delete(dest);
                        EventLog("11. 삭제 Dest : " + dest);
                        System.IO.File.Move(filename, dest);
                        EventLog("11. 복사 " + filename + " -> " + dest);
                    }
                    catch (Exception ex)
                    {
                        EventLog("11. 업데이트를 적용하다 에러가 발생. " + filename + "\n" + ex.ToString(), EventLogEntryType.Error);
                    }
                }
                try
                {
                    System.IO.Directory.Delete(System.IO.Path.Combine(Path, "Updates"));
                }
                catch (Exception ex)
                {
                    EventLog("11. 폴더를 지우다 에러가 발생. \n" + ex.ToString(), EventLogEntryType.Error);
                }
            }
            catch (Exception ex)
            {
                EventLog("10. Post Update가 파일목록을 받는데 실패했습니다. \n" + ex.ToString(), EventLogEntryType.Error);
            }
            #endregion

            #region "12. 업데이트 된 패키지 리스트로 새로고침합니다."
            try
            {
                EventLog("12. 업데이트 된 패키지 리스트로 새로고침합니다.");
                System.IO.File.Delete(System.IO.Path.Combine(Path, "Packages.xml.old"));
                System.IO.File.Copy(System.IO.Path.Combine(Path, "Packages.xml"), System.IO.Path.Combine(Path, "Packages.xml.old"));
                System.IO.File.Delete(System.IO.Path.Combine(Path, "Packages.xml"));
                System.IO.File.Move(System.IO.Path.Combine(Path, "Latest.xml"), System.IO.Path.Combine(Path, "Packages.xml"));
            }
            catch (Exception ex)
            {
                EventLog("12. 패키지 리스트를 교환하다 에러가 발생.\n" + ex.ToString(), EventLogEntryType.Error);
            }
            #endregion

            #region "13. 업데이트가 끝났으면 CNUService를 다시 살려 패키지들이 다시 살아나게 합니다."
            try
            {
                ServiceController cnuService = new ServiceController("CNUService");
                cnuService.Start();
            }
            catch (Exception ex)
            {
                EventLog("13. 업데이트가 끝났는데 => CNUService가없어?!!!!뭐야? \n" + ex.ToString(), EventLogEntryType.Error);
            }
            #endregion

            Application.Current.Shutdown();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Thread.Sleep(1000);
            PostUpdaterDoing();
        }
    }
}
