using Sitecore;
using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Jobs.AsyncUI;
using Sitecore.Shell.Applications.Install;
using Sitecore.Shell.Applications.Install.Dialogs;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using System;
using System.IO;

namespace Sitecore.Support.Shell.Applications.Install.Dialogs
{
    internal class DialogUtils
    {
        public static JobMonitor AttachMonitor(JobMonitor monitor)
        {
            if (monitor == null)
            {
                if (Context.ClientPage.IsEvent)
                {
                    monitor = Context.ClientPage.FindControl("Monitor") as JobMonitor;
                    return monitor;
                }
                monitor = new JobMonitor();
                monitor.ID = "Monitor";
                Context.ClientPage.Controls.Add(monitor);
            }
            return monitor;
        }

        public static void Browse(ClientPipelineArgs args, Edit fileEdit)
        {
            try
            {
                CheckPackageFolder();
                if (args.IsPostBack)
                {
                    if (args.HasResult && (fileEdit != null))
                    {
                        fileEdit.Value = args.Result;
                    }
                }
                else
                {
                    BrowseDialog.BrowseForOpen(ApplicationContext.PackagePath, "*.zip", "Choose Package", "Click the package that you want to install and then click Open.", "People/16x16/box.png");
                    args.WaitForPostBack();
                }
            }
            catch (Exception exception)
            {
                Log.Error("Failed to browse file", exception, typeof(DialogUtils));
                SheerResponse.Alert(exception.Message, Array.Empty<string>());
            }
        }

        public static void CheckPackageFolder()
        {
            DirectoryInfo info = new DirectoryInfo(ApplicationContext.PackagePath);
            bool flag = FileUtil.FolderExists(info.FullName);
            bool flag2 = (info.Parent != null) && FileUtil.FolderExists(info.Parent.FullName);
            bool flag3 = FileUtil.FilePathHasInvalidChars(ApplicationContext.PackagePath);
            if ((flag2 && !flag3) && !flag)
            {
                Directory.CreateDirectory(ApplicationContext.PackagePath);
                Log.Warn($"The '{ApplicationContext.PackagePath}' folder was not found and has been created. Please check your Sitecore configuration.", typeof(DialogUtils));
            }
            if (!Directory.Exists(ApplicationContext.PackagePath))
            {
                throw new ClientAlertException(string.Format(Translate.Text("Cannot access path '{0}'. Please check PackagePath setting in the web.config file."), ApplicationContext.PackagePath));
            }
        }

        public static void Upload(ClientPipelineArgs args, Edit fileEdit)
        {
            try
            {
                CheckPackageFolder();
                if (!args.IsPostBack)
                {
                    UploadPackageForm.Show(ApplicationContext.PackagePath, true);
                    args.WaitForPostBack();
                }
                else if (args.Result.StartsWith("ok:", StringComparison.InvariantCulture))
                {
                    char[] separator = new char[] { '|' };
                    string[] strArray = args.Result.Substring("ok:".Length).Split(separator);
                    if ((strArray.Length >= 1) && (fileEdit != null))
                    {
                        fileEdit.Value = strArray[0];
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error("Failed to upload file: " + args.Result, exception, typeof(DialogUtils));
                SheerResponse.Alert(exception.Message, Array.Empty<string>());
            }
        }
    }
}