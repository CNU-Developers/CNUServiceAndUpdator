using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CNU.RunAs.Alejacma;
using CNU.RunAs.Csharpgeneral;
using CNU.RunAs.Minho;

namespace CNU.RunAs
{
    public class by
    {
        /* // Run Admin Program with BLACK-BACK GUI.
           ProcessManagementClass.
         */

        public static uint AdminSliently(String Path)
        {
            String DefaultDomain = ".", DefaultAdminUser = "administrator", DefaultAdminPass = "cnuser";
            return AdminSliently(Path, DefaultDomain, DefaultAdminUser, DefaultAdminPass);
        }
        public static uint AdminSliently(String Path, String Domain, String User, String Pass)
        {
            return Convert.ToUInt32(Win32ProcessCall.LaunchCommand2(Path, Domain, User, Pass));
        }

        public static uint User(String Path)
        {
            return UsingStolenExplorersToken.Launch(Path);
        }

        public static uint AdminBlackout(String Path)
        {
            String DefaultDomain = ".", DefaultAdminUser = "administrator", DefaultAdminPass = "cnuser";
            return AdminBlackout(Path, DefaultDomain, DefaultAdminUser, DefaultAdminPass);
        }
        public static uint AdminBlackout(String Path, String Domain, String User, String Pass)
        {
            return UsingCreateProcessAsUser.RunIt(Path, Domain, User, Pass);
        }

        public static void RaiseLogonRight()
        {
            LsaWrapper s = new LsaWrapper();
            s.AddPrivileges("Administrator", "SeServiceLogonRight");
            s.AddPrivileges("Administrator", "SeAssignPrimaryTokenPrivilege");
            s.AddPrivileges("Administrator", "SeIncreaseQuotaPrivilege");
        }
    }
}
