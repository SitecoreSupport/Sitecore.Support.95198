using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Install.Framework;
using Sitecore.Install.Security;
using Sitecore.Jobs.AsyncUI;
using Sitecore.Security.Accounts;
using Sitecore.SecurityModel;
using Sitecore.SecurityModel.Cryptography;
using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Security;
using System.Xml;

namespace Sitecore.Support.Install.Security
{
    public class AccountInstaller : Sitecore.Install.Security.AccountInstaller
    {
        private bool _skipAll;
        private static readonly MethodInfo ContextUserHasEnoughRightsMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("ContextUserHasEnoughRights", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo GetAccountNameMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("GetAccountName", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo ReadXmlMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("ReadXml", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo SetUserProfileMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("SetUserProfile", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo SetUserPropertiesMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("SetUserProperties", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo SetUserRolesMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("SetUserRoles", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo TryingToInstallAdminMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("TryingToInstallAdmin", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo UIEventsPI = typeof(Sitecore.Install.Security.AccountInstaller).GetProperty("UIEvents", BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool ContextUserHasEnoughRights()
        {
            return ((bool)ContextUserHasEnoughRightsMI.Invoke(null, null));
        }
        private static string GetAccountName(string key)
        {
            return ((string)GetAccountNameMI.Invoke(null, new object[] { key }));
        }
        private static string GetPassword(XmlDocument xml)
        {
            XmlNodeList list = xml.SelectNodes("/user/password");
            if ((list != null) && (list.Count == 1))
            {
                return Encoding.Unicode.GetString(System.Convert.FromBase64String(list[0].InnerText));
            }
            try
            {
                MembershipProvider provider = Membership.Providers["sql"];
                return (provider as SqlMembershipProvider).GeneratePassword();
            }
            catch (Exception)
            {
            }
            return new PasswordGenerator().Generate();
        }

        protected new void InstallUser(PackageEntry entry)
        {
            string accountName = GetAccountName(entry.Key);
            if (User.Exists(accountName))
            {
                Log.Info($"Installing of entry '{accountName}' was skipped. User already exists.", this);
                string message = string.Format(Translate.Text("User '{0}' will not be installed since the user already exists."), accountName);
                this.UIEvents.ShowWarning(message, "user already exists");
            }
            else
            {
                XmlDocument xml = ReadXml(entry);
                if (Context.User.IsAdministrator || !this.TryingToInstallAdmin(xml, accountName))
                {
                    string password = GetPassword(xml);
                    User user = null;
                    try
                    {
                        user = User.Create(accountName, password);
                        this.SetUserProfile(user, xml);
                        this.SetUserProperties(user, xml);
                        this.SetUserRoles(user, xml);
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            if (user != null)
                            {
                                user.Delete();
                            }
                        }
                        catch
                        {
                        }
                        Log.Error($"Failed to install the user '{accountName}'", exception, this);
                        throw;
                    }
                    Log.Info($"User '{user.Name}' has been installed successfully", this);
                }
            }
        }

        public override void Put(PackageEntry entry)
        {
            this._skipAll = (bool)typeof(Sitecore.Install.Security.AccountInstaller).GetField("_skipAll", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
            if (!this._skipAll)
            {
                string[] strArray = entry.Key.Split(new char[] { '/' });
                if (strArray[0] == "security")
                {
                    if (!ContextUserHasEnoughRights())
                    {
                        JobContext.Alert(Translate.Text("You do not have enough permissions to install security accounts"));
                        this._skipAll = true;
                    }
                    else if (strArray.Length < 3)
                    {
                        Log.Error($"Bad entry key '{entry.Key}'", this);
                    }
                    else
                    {
                        if (strArray.Length > 3)
                        {
                            string domainName = strArray[2];
                            if (!DomainManager.DomainExists(domainName))
                            {
                                string accountName = GetAccountName(entry.Key);
                                string message = string.Format(Translate.Text("Unable to create the user '{0}' because domain '{1}' doesn't exist."), accountName, domainName);
                                this.UIEvents.ShowWarning(message, "domain doesn't exist" + domainName);
                                return;
                            }
                        }
                        string str4 = strArray[1];
                        try
                        {
                            if (str4 == "users")
                            {
                                this.InstallUser(entry);
                            }
                            else if (str4 == "roles")
                            {
                                base.InstallRole(entry);
                            }
                            else
                            {
                                Log.Error($"Unexpected account type '{entry.Key}'", this);
                            }
                        }
                        catch (ThreadAbortException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            Log.Error($"Error installing entry '{entry.Key}'", exception, this);
                        }
                    }
                }
            }
        }

        private static XmlDocument ReadXml(PackageEntry entry)
        {
            return ((XmlDocument)ReadXmlMI.Invoke(null, new object[] { entry }));
        }
            
        private void SetUserProfile(User user, XmlDocument xml)
        {
            SetUserProfileMI.Invoke(this, new object[] { user, xml });
        }

        private void SetUserProperties(User user, XmlDocument xml)
        {
            SetUserPropertiesMI.Invoke(this, new object[] { user, xml });
        }

        private void SetUserRoles(User user, XmlDocument xml)
        {
            SetUserRolesMI.Invoke(this, new object[] { user, xml });
        }

        private bool TryingToInstallAdmin(XmlDocument xml, string userName)
        {
            return ((bool)TryingToInstallAdminMI.Invoke(this, new object[] { xml, userName }));
        }
            
        private IAccountInstallerEvents UIEvents
        {
            get
            {
                return ((IAccountInstallerEvents)UIEventsPI.GetGetMethod(true).Invoke(this, null));
            }
        }
            
    }
}