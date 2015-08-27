using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Xml;
using System.Threading;
using System.ComponentModel;

using CNU.RunAs;
using CNU.ProcessMGMTClass;
using System.Net;

namespace CNU.UpdateMGMTClass
{
    public delegate void FailingEventHandler(object sender, FailingEventArgs e);

    public class Package
    {
        public String Name, Version;
        //public String ProductCode;  // 안쓰는듯?
        public bool toRun = false;
        public String RunAs;
        //public String RunWhen;  // 안쓰는듯?
        public bool RunOnce = false;
        public int CurrentPID;
        protected ManualResetEvent _doneUninstallEvent, _doneInstallEvent;

        public event FailingEventHandler Error;
        public void GotError(string reason)
        {
            if (this.Error != null)
            {
                FailingEventArgs pArgs = new FailingEventArgs(reason);
                this.Error(this, pArgs);
            }
        }

        //public List<String> Dependents;  // 어차피 우리는 모두 최신으로 유지하려고 할꺼니까 무시합시다!
        //public Package()
        //{
        //    Dependents = new List<String>();
        //}

        public void SetEventHandlers(ManualResetEvent uninstallEvt, ManualResetEvent installEvt)
        {
            if(uninstallEvt != null)
                this._doneUninstallEvent = uninstallEvt;
            if(installEvt != null)
                this._doneInstallEvent = installEvt;
        }
        public void UpdateCallback(Object threadContext)
        {
            if (this._doneUninstallEvent != null)
                #region "UNINSTALL"
            {
                // 1. Kill Process
                try
                {
                    if (this.CurrentPID != 0)
                        MyProcess.KillProcess(this.CurrentPID);  // Already Found.
                    MyProcess.KillProcess(this.Name);  // With Extension
                    MyProcess.KillProcess(System.IO.Path.GetFileNameWithoutExtension(this.Name));  // Without Extension
                }
                catch (Exception ex)
                {
                    GotError(ex.ToString());
                }

                Thread.Sleep(2000);

                //if (ProductCode == null)
                {
                    // 2. Delete that file.
                    try
                    {
                        System.IO.File.Delete(System.IO.Path.Combine(UpdatePath, this.Name));
                    }
                    catch (Exception ex)
                    {
                        GotError(ex.ToString());
                    }
                }
                #region "#TODO: Product Code"
                /*
                else
                {
                    // 2. Uninstall current package.
                    // msiexec /x {1239081203801283012312-1230812038-1230} /passive
                    //CNU.RunAs.by.User("msiexec /x " + ProductCode);
                }
                */
                #endregion
                _doneUninstallEvent.Set();
            }
            #endregion

            if (this._doneInstallEvent != null)
                #region "INSTALL"
            {
                //if (ProductCode == null)
                {
                    // 1. Download Update -> Done.
                    // e) If Download Fail, delete that file.
                    try
                    {
                        String remotePath = Package.UpdateServer + this.Name + "/" + this.Version + "/" + this.Name;
                        String localPath = System.IO.Path.Combine(UpdatePath, "Updates", this.Name);
                        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(UpdatePath, "Updates"));
                        WebClient newClient = new WebClient();
                        newClient.DownloadFile(remotePath, localPath);
                    }
                    catch (Exception ex)
                    {
                        GotError(ex.ToString());
                    }
                }
                #region "#TODO: Product Code"
                /*
                else
                {
                    // 1. Download Update
                    // 2. Kill Process
                    // 3. Install latest package.
                    // setup.msi /passive
                }
                */
                #endregion
                _doneInstallEvent.Set();
            }
            #endregion
            
            return;
        }

        // ===================================================================

        public static String UpdateServer;
        public static String UpdatePath;
        public static List<Package> ReadXML(String filename)
        {
            List<Package> Packages = new List<Package>();
            
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(filename);

            foreach (XmlNode Node_Lv1 in xmldoc.DocumentElement.ChildNodes)
            {
                switch (Node_Lv1.Name)
                {
                    // =============================
                    case "update":
                        foreach (XmlAttribute Attr in Node_Lv1.Attributes)
                        {
                            switch (Attr.Name)
                            {
                                // ---------------------
                                case "base":
                                    Package.UpdateServer = Attr.Value;
                                    break;
                                // ---------------------
                            }
                        }
                        break;
                    // =============================
                    case "package":
                        Package This = new Package();
                        foreach (XmlAttribute Attr in Node_Lv1.Attributes)
                        {
                            switch (Attr.Name)
                            {
                                // ---------------------
                                case "name":
                                    This.Name = Attr.Value;
                                    break;
                                // ---------------------
                                case "version":
                                    This.Version = Attr.Value;
                                    break;
                                // ---------------------
                            }
                        }
                        foreach (XmlNode Node_Lv2 in Node_Lv1.ChildNodes)
                        {
                            switch (Node_Lv2.Name)
                            {
                                // ---------------------
                                //case "product":
                                //    foreach (XmlAttribute Attr in Node_Lv2.Attributes)
                                //    {
                                //        switch (Attr.Name)
                                //        {
                                //            // .............
                                //            case "code":
                                //                This.ProductCode = Attr.Value;
                                //                break;
                                //            // .............
                                //        }
                                //    }
                                //    break;
                                // ---------------------
                                case "run":
                                    This.toRun = true;
                                    foreach (XmlNode Node_Lv3 in Node_Lv2.ChildNodes)
                                    {
                                        switch (Node_Lv3.Name)
                                        {
                                            // .............
                                            case "as":
                                                This.RunAs = Node_Lv3.InnerText;
                                                break;
                                            // .............
                                            case "once":
                                                This.RunOnce = true;
                                                break;
                                            // .............
                                        }
                                    }
                                    break;
                                // ---------------------
                                //case "dependent":
                                //    foreach (XmlAttribute Attr in Node_Lv2.Attributes)
                                //    {
                                //        switch (Attr.Name)
                                //        {
                                //            // .............
                                //            case "name":
                                //                This.Dependents.Add(Attr.Value);
                                //                break;
                                //            // .............
                                //        }
                                //    }
                                //    break;
                                // ---------------------
                            }
                        }
                        Packages.Add(This);
                        break;
                    // =============================
                }
            }
            return Packages;
        }
        public static bool IsNeedToUpdateByComparePackages(Package Local, Package Remote)
        {
            if ((Local.Name).Equals(Remote.Name))
                if ((Local.Version).CompareTo(Remote.Version) < 0)
                    return true;
            return false;
        }
    }
}
