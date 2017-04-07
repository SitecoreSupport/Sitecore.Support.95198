using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Engines;
using Sitecore.Data.Proxies;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Install;
using Sitecore.Install.Events;
using Sitecore.Install.Files;
using Sitecore.Install.Framework;
using Sitecore.Install.Items;
using Sitecore.Install.Metadata;
using Sitecore.Install.Security;
using Sitecore.Install.Utils;
using Sitecore.Install.Zip;
using Sitecore.IO;
using Sitecore.Jobs;
using Sitecore.Jobs.AsyncUI;
using Sitecore.SecurityModel;
using Sitecore.Shell.Applications.Install.Dialogs.InstallPackage;
using Sitecore.Shell.Framework;
using Sitecore.Support.Install;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Sitecore.Support.Shell.Applications.Install.Dialogs.InstallPackage
{
    public class InstallPackageForm : WizardForm
    {
        protected Border AbortMessage;
        protected Radiobutton Accept;
        protected Edit Author;
        private readonly object CurrentStepSync = new object();
        protected Radiobutton Decline;
        protected Literal ErrorDescription;
        protected Border ErrorMessage;
        protected Literal FailingReason;
        protected Border LicenseAgreement;
        protected JobMonitor Monitor;
        protected Edit PackageFile;
        protected Edit PackageName;
        protected Edit Publisher;
        protected Memo ReadmeText;
        protected Checkbox Restart;
        protected Checkbox RestartServer;
        protected Border SuccessMessage;
        protected Edit Version;

        protected override void ActivePageChanged(string page, string oldPage)
        {
            base.ActivePageChanged(page, oldPage);
            base.NextButton.Header = this.OriginalNextButtonHeader;
            if ((page == "License") && (oldPage == "LoadPackage"))
            {
                base.NextButton.Disabled = !this.Accept.Checked;
            }
            if (page == "Installing")
            {
                base.BackButton.Disabled = true;
                base.NextButton.Disabled = true;
                base.CancelButton.Disabled = true;
                Context.ClientPage.SendMessage(this, "installer:startInstallation");
            }
            if (page == "Ready")
            {
                base.NextButton.Header = Translate.Text("Install");
            }
            if (page == "LastPage")
            {
                base.BackButton.Disabled = true;
            }
            if (!this.Successful)
            {
                base.CancelButton.Header = Translate.Text("Close");
                this.Successful = true;
            }
        }

        protected override bool ActivePageChanging(string page, ref string newpage)
        {
            bool flag = base.ActivePageChanging(page, ref newpage);
            if ((page == "LoadPackage") && (newpage == "License"))
            {
                flag = this.LoadPackage();
                if (!this.HasLicense)
                {
                    newpage = "Readme";
                    if (!this.HasReadme)
                    {
                        newpage = "Ready";
                    }
                }
                return flag;
            }
            if ((page == "License") && (newpage == "Readme"))
            {
                if (!this.HasReadme)
                {
                    newpage = "Ready";
                }
                return flag;
            }
            if ((page == "Ready") && (newpage == "Readme"))
            {
                if (!this.HasReadme)
                {
                    newpage = "License";
                    if (!this.HasLicense)
                    {
                        newpage = "LoadPackage";
                    }
                }
                return flag;
            }
            if (((page == "Readme") && (newpage == "License")) && !this.HasLicense)
            {
                newpage = "LoadPackage";
            }
            return flag;
        }

        protected void Agree()
        {
            base.NextButton.Disabled = false;
            Context.ClientPage.ClientResponse.SetReturnValue(true);
        }

        [HandleMessage("installer:browse", true)]
        protected void Browse(ClientPipelineArgs args)
        {
            ConstructorInfo info = Assembly.GetAssembly(typeof(Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm)).GetType("Sitecore.Shell.Applications.Install.Dialogs.DialogUtils").GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Any, new Type[0], null);
            object obj2 = info.Invoke(null);
            this.Monitor = (JobMonitor)obj2.GetType().GetMethod("Browse", BindingFlags.Public | BindingFlags.Static).Invoke(info.Invoke(null), new object[] { args, this.PackageFile });
        }

        public void Cancel()
        {
            if (base.Pages.IndexOf(base.Active) == (base.Pages.Count - 1))
            {
                this.EndWizard();
            }
            else
            {
                this.Cancelling = true;
                Context.ClientPage.Start(this, "Confirmation");
            }
        }

        protected void CopyErrorMessage()
        {
            Context.ClientPage.ClientResponse.Eval("window.clipboardData.setData('Text', scForm.browser.getControl('ErrorDescription').innerHTML)");
        }

        protected void CopyLicense()
        {
            Context.ClientPage.ClientResponse.Eval("window.clipboardData.setData('Text', scForm.browser.getControl('LicenseAgreement').innerHTML)");
        }

        protected void CopyReadme()
        {
            Context.ClientPage.ClientResponse.Eval("window.clipboardData.setData('Text', scForm.browser.getControl('ReadmeText').value)");
        }

        protected void Disagree()
        {
            base.NextButton.Disabled = true;
            Context.ClientPage.ClientResponse.SetReturnValue(true);
        }

        protected void Done()
        {
            base.Active = "LastPage";
            base.BackButton.Disabled = true;
            base.NextButton.Disabled = true;
            base.CancelButton.Disabled = false;
        }

        [HandleMessage("installer:doPostAction")]
        protected void DoPostAction(Message msg)
        {
            if (!string.IsNullOrEmpty(this.PostAction))
            {
                this.StartPostAction();
            }
        }

        protected override void EndWizard()
        {
            if (!this.Cancelling)
            {
                if (this.RestartServer.Checked)
                {
                    Sitecore.Install.Installer.RestartServer();
                }
                if (this.Restart.Checked)
                {
                    Context.ClientPage.ClientResponse.Broadcast(Context.ClientPage.ClientResponse.SetLocation(string.Empty), "Shell");
                }
            }
            Sitecore.Shell.Framework.Windows.Close();
        }

        private IProcessingContext GetContextWithMetadata()
        {
            string filename = Sitecore.Install.Installer.GetFilename(this.PackageFile.Value);
            IProcessingContext context = Sitecore.Install.Installer.CreatePreviewContext();
            ISource<PackageEntry> source = new PackageReader(MainUtil.MapPath(filename));
            MetadataView view = new MetadataView(context);
            MetadataSink sink = new MetadataSink(view);
            sink.Initialize(context);
            source.Populate(sink);
            return context;
        }

        private static string GetFullDescription(Exception e) =>
            e.ToString();

        private static string GetShortDescription(Exception e)
        {
            string message = e.Message;
            int index = message.IndexOf("(method:", StringComparison.InvariantCulture);
            if (index > -1)
            {
                return message.Substring(0, index - 1);
            }
            return message;
        }

        private void GotoLastPage(Result result, string shortDescription, string fullDescription)
        {
            this.ErrorDescription.Text = fullDescription;
            this.FailingReason.Text = shortDescription;
            this.Cancelling = result != Result.Success;
            SetVisibility(this.SuccessMessage, result == Result.Success);
            SetVisibility(this.ErrorMessage, result == Result.Failure);
            SetVisibility(this.AbortMessage, result == Result.Abort);
            InstallationEventArgs args = new InstallationEventArgs(new List<ItemUri>(), new List<FileCopyInfo>(), "packageinstall:ended");
            Event.RaiseEvent("packageinstall:ended", new object[] { args });
            this.Successful = result == Result.Success;
            base.Active = "LastPage";
        }

        private bool LoadPackage()
        {
            string path = this.PackageFile.Value;
            if (Path.GetExtension(path).Trim().Length == 0)
            {
                path = Path.ChangeExtension(path, ".zip");
                this.PackageFile.Value = path;
            }
            if (path.Trim().Length == 0)
            {
                Context.ClientPage.ClientResponse.Alert("Please specify a package.");
                return false;
            }
            path = Sitecore.Install.Installer.GetFilename(path);
            if (!FileUtil.FileExists(path))
            {
                Context.ClientPage.ClientResponse.Alert(Translate.Text("The package \"{0}\" file does not exist.", new object[] { path }));
                return false;
            }
            IProcessingContext context = Sitecore.Install.Installer.CreatePreviewContext();
            ISource<PackageEntry> source = new PackageReader(MainUtil.MapPath(path));
            MetadataView view = new MetadataView(context);
            MetadataSink sink = new MetadataSink(view);
            sink.Initialize(context);
            source.Populate(sink);
            if ((context == null) || (context.Data == null))
            {
                Context.ClientPage.ClientResponse.Alert(Translate.Text("The package \"{0}\" could not be loaded.\n\nThe file maybe corrupt.", new object[] { path }));
                return false;
            }
            this.PackageVersion = context.Data.ContainsKey("installer-version") ? 2 : 1;
            this.PackageName.Value = view.PackageName;
            this.Version.Value = view.Version;
            this.Author.Value = view.Author;
            this.Publisher.Value = view.Publisher;
            this.LicenseAgreement.InnerHtml = view.License;
            this.ReadmeText.Value = view.Readme;
            this.HasLicense = view.License.Length > 0;
            this.HasReadme = view.Readme.Length > 0;
            this.PostAction = view.PostStep;
            Registry.SetString("Packager/File", this.PackageFile.Value);
            return true;
        }

        private void Monitor_JobDisappeared(object sender, EventArgs e)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(e, "e");
            lock (this.CurrentStepSync)
            {
                switch (this.CurrentStep)
                {
                    case InstallationSteps.MainInstallation:
                        this.GotoLastPage(Result.Failure, Translate.Text("Installation could not be completed."), Translate.Text("Installation job was interrupted unexpectedly."));
                        break;

                    case InstallationSteps.WaitForFiles:
                        this.WatchForInstallationStatus();
                        break;

                    default:
                        this.Monitor_JobFinished(sender, e);
                        break;
                }
            }
        }

        private void Monitor_JobFinished(object sender, EventArgs e)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(e, "e");
            lock (this.CurrentStepSync)
            {
                switch (this.CurrentStep)
                {
                    case InstallationSteps.MainInstallation:
                        this.CurrentStep = InstallationSteps.WaitForFiles;
                        this.WatchForInstallationStatus();
                        goto Label_00B0;

                    case InstallationSteps.WaitForFiles:
                        this.CurrentStep = InstallationSteps.InstallSecurity;
                        this.StartInstallingSecurity();
                        goto Label_00B0;

                    case InstallationSteps.InstallSecurity:
                        this.CurrentStep = InstallationSteps.RunPostAction;
                        if (!string.IsNullOrEmpty(this.PostAction))
                        {
                            break;
                        }
                        this.GotoLastPage(Result.Success, string.Empty, string.Empty);
                        goto Label_00B0;

                    case InstallationSteps.RunPostAction:
                        this.GotoLastPage(Result.Success, string.Empty, string.Empty);
                        goto Label_00B0;

                    default:
                        goto Label_00B0;
                }
                this.StartPostAction();
                Label_00B0:;
            }
        }

        protected override void OnCancel(object sender, EventArgs formEventArgs)
        {
            this.Cancel();
        }

        [HandleMessage("installer:commitingFiles")]
        private void OnCommittingFiles(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            lock (this.CurrentStepSync)
            {
                if (this.CurrentStep == InstallationSteps.MainInstallation)
                {
                    this.CurrentStep = InstallationSteps.WaitForFiles;
                    this.WatchForInstallationStatus();
                }
            }
        }

        [HandleMessage("installer:aborted")]
        protected void OnInstallerAborted(Message message)
        {
            this.GotoLastPage(Result.Abort, string.Empty, string.Empty);
            this.CurrentStep = InstallationSteps.Failed;
        }

        [HandleMessage("installer:failed")]
        protected void OnInstallerFailed(Message message)
        {
            Job job = JobManager.GetJob(this.Monitor.JobHandle);
            Assert.IsNotNull(job, "Job is not available");
            Exception result = job.Status.Result as Exception;
            Error.AssertNotNull(result, "Cannot get any exception details");
            this.GotoLastPage(Result.Failure, GetShortDescription(result), GetFullDescription(result));
            this.CurrentStep = InstallationSteps.Failed;
        }

        protected override void OnLoad(EventArgs e)
        {
            if (!Context.ClientPage.IsEvent)
            {
                this.OriginalNextButtonHeader = base.NextButton.Header;
            }
            base.OnLoad(e);
            ConstructorInfo info = Assembly.GetAssembly(typeof(Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm)).GetType("Sitecore.Shell.Applications.Install.Dialogs.DialogUtils").GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Any, new Type[0], null);
            object obj2 = info.Invoke(null);
            this.Monitor = (JobMonitor)obj2.GetType().GetMethod("AttachMonitor", BindingFlags.Public | BindingFlags.Static).Invoke(info.Invoke(null), new object[] { this.Monitor });
            if (!Context.ClientPage.IsEvent)
            {
                this.PackageFile.Value = Registry.GetString("Packager/File");
                this.Decline.Checked = true;
                this.Restart.Checked = true;
                this.RestartServer.Checked = false;
            }
            this.Monitor.JobFinished += new EventHandler(this.Monitor_JobFinished);
            this.Monitor.JobDisappeared += new EventHandler(this.Monitor_JobDisappeared);
        }

        protected void RestartInstallation()
        {
            base.Active = "Ready";
        }

        [HandleMessage("installer:savePostAction")]
        protected void SavePostAction(Message msg)
        {
            string str = msg.Arguments[0];
            this.PostAction = str;
        }

        [HandleMessage("installer:setTaskId")]
        private void SetTaskID(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            Assert.IsNotNull(message["id"], "id");
            this.MainInstallationTaskID = message["id"];
        }

        private static void SetVisibility(Control control, bool visible)
        {
            Context.ClientPage.ClientResponse.SetStyle(control.ID, "display", visible ? "" : "none");
        }

        [HandleMessage("installer:startInstallation")]
        protected void StartInstallation(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            this.CurrentStep = InstallationSteps.MainInstallation;
            string filename = Sitecore.Install.Installer.GetFilename(this.PackageFile.Value);
            if (FileUtil.IsFile(filename))
            {
                this.StartTask(filename);
            }
            else
            {
                Context.ClientPage.ClientResponse.Alert("Package not found");
                base.Active = "Ready";
                base.BackButton.Disabled = true;
            }
        }

        private void StartInstallingSecurity()
        {
            string filename = Sitecore.Install.Installer.GetFilename(this.PackageFile.Value);
            this.Monitor.Start("InstallSecurity", "Install", new ThreadStart(new AsyncHelper(filename).InstallSecurity));
        }

        private void StartPostAction()
        {
            if (this.Monitor.JobHandle != Handle.Null)
            {
                Log.Info("Waiting for installation task completion", this);
                SheerResponse.Timer("installer:doPostAction", 100);
            }
            else
            {
                string postAction = this.PostAction;
                this.PostAction = string.Empty;
                if ((postAction.IndexOf("://", StringComparison.InvariantCulture) < 0) && postAction.StartsWith("/", StringComparison.InvariantCulture))
                {
                    postAction = WebUtil.GetServerUrl() + postAction;
                }
                this.Monitor.Start("RunPostAction", "Install", new ThreadStart(new AsyncHelper(postAction, this.GetContextWithMetadata()).ExecutePostStep));
            }
        }

        private void StartTask(string packageFile)
        {
            this.Monitor.Start("Install", "Install", new ThreadStart(new AsyncHelper(packageFile).Install));
        }

        [HandleMessage("installer:upload", true)]
        protected void Upload(ClientPipelineArgs args)
        {
            ConstructorInfo info = Assembly.GetAssembly(typeof(Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm)).GetType("Sitecore.Shell.Applications.Install.Dialogs.DialogUtils").GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Any, new Type[0], null);
            object obj2 = info.Invoke(null);
            this.Monitor = (JobMonitor)obj2.GetType().GetMethod("Upload", BindingFlags.Public | BindingFlags.Static).Invoke(info.Invoke(null), new object[] { args, this.PackageFile });
        }

        private void WatchForInstallationStatus()
        {
            string statusFileName = FileInstaller.GetStatusFileName(this.MainInstallationTaskID);
            this.Monitor.Start("WatchStatus", "Install", new ThreadStart(new AsyncHelper().SetStatusFile(statusFileName).WatchForStatus));
        }

        private bool Cancelling
        {
            get =>
                MainUtil.GetBool(Context.ClientPage.ServerProperties["__cancelling"], false);
            set
            {
                Context.ClientPage.ServerProperties["__cancelling"] = value;
            }
        }

        private InstallationSteps CurrentStep
        {
            get =>
                ((InstallationSteps)((int)base.ServerProperties["installationStep"]));
            set
            {
                lock (this.CurrentStepSync)
                {
                    base.ServerProperties["installationStep"] = (int)value;
                }
            }
        }

        public bool HasLicense
        {
            get =>
                MainUtil.GetBool(Context.ClientPage.ServerProperties["HasLicense"], false);
            set
            {
                Context.ClientPage.ServerProperties["HasLicense"] = value.ToString();
            }
        }

        public bool HasReadme
        {
            get =>
                MainUtil.GetBool(Context.ClientPage.ServerProperties["Readme"], false);
            set
            {
                Context.ClientPage.ServerProperties["Readme"] = value.ToString();
            }
        }

        private string MainInstallationTaskID
        {
            get =>
                StringUtil.GetString(base.ServerProperties["taskID"]);
            set
            {
                base.ServerProperties["taskID"] = value;
            }
        }

        private string OriginalNextButtonHeader
        {
            get =>
                StringUtil.GetString(Context.ClientPage.ServerProperties["next-header"]);
            set
            {
                Context.ClientPage.ServerProperties["next-header"] = value;
            }
        }

        private int PackageVersion
        {
            get =>
                int.Parse(StringUtil.GetString(base.ServerProperties["packageType"], "1"));
            set
            {
                base.ServerProperties["packageType"] = value;
            }
        }

        private string PostAction
        {
            get =>
                StringUtil.GetString(base.ServerProperties["postAction"]);
            set
            {
                base.ServerProperties["postAction"] = value;
            }
        }

        private bool Successful
        {
            get
            {
                object obj2 = base.ServerProperties["Successful"];
                if (obj2 is bool)
                {
                    return (bool)obj2;
                }
                return true;
            }
            set
            {
                base.ServerProperties["Successful"] = value;
            }
        }

        private class AsyncHelper
        {
            private IProcessingContext _context;
            private Language _language;
            private string _packageFile;
            private string _postAction;
            private StatusFile _statusFile;

            public AsyncHelper()
            {
                this._language = Context.Language;
            }

            public AsyncHelper(string package)
            {
                this._packageFile = package;
                this._language = Context.Language;
            }

            public AsyncHelper(string postAction, IProcessingContext context)
            {
                this._postAction = postAction;
                this._context = context;
                this._language = Context.Language;
            }

            private void CatchExceptions(ThreadStart start)
            {
                try
                {
                    start();
                }
                catch (ThreadAbortException)
                {
                    if (!Environment.HasShutdownStarted)
                    {
                        Thread.ResetAbort();
                    }
                    Log.Info("Installation was aborted", this);
                    JobContext.PostMessage("installer:aborted");
                    JobContext.Flush();
                }
                catch (Exception exception)
                {
                    Log.Error("Installation failed: " + exception, this);
                    JobContext.Job.Status.Result = exception;
                    JobContext.PostMessage("installer:failed");
                    JobContext.Flush();
                }
            }

            public void ExecutePostStep()
            {
                this.CatchExceptions(() => new Sitecore.Support.Install.Installer().ExecutePostStep(this._postAction, this._context));
            }

            public void Install()
            {
                this.CatchExceptions(delegate {
                    using (new SecurityDisabler())
                    {
                        using (new ProxyDisabler())
                        {
                            using (new SyncOperationContext())
                            {
                                using (new LanguageSwitcher(this._language))
                                {
                                    IProcessingContext context = Sitecore.Install.Installer.CreateInstallationContext();
                                    JobContext.PostMessage("installer:setTaskId(id=" + context.TaskID + ")");
                                    ConstructorInfo info = Assembly.GetAssembly(typeof(Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm)).GetType("Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.UiInstallerEvents").GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Any, new Type[0], null);
                                    context.AddAspect<IItemInstallerEvents>((IItemInstallerEvents)info.Invoke(null));
                                    context.AddAspect<IFileInstallerEvents>((IFileInstallerEvents)info.Invoke(null));
                                    new Sitecore.Support.Install.Installer().InstallPackage(PathUtils.MapPath(this._packageFile), context);
                                }
                            }
                        }
                    }
                });
            }

            public void InstallSecurity()
            {
                this.CatchExceptions(delegate {
                    using (new LanguageSwitcher(this._language))
                    {
                        IProcessingContext context = Sitecore.Install.Installer.CreateInstallationContext();
                        ConstructorInfo info = Assembly.GetAssembly(typeof(Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm)).GetType("Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.UiInstallerEvents").GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Any, new Type[0], null);
                        context.AddAspect<IAccountInstallerEvents>((IAccountInstallerEvents)info.Invoke(null));
                        new Sitecore.Support.Install.Installer().InstallSecurity(PathUtils.MapPath(this._packageFile), context);
                    }
                });
            }

            public Sitecore.Support.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm.AsyncHelper SetStatusFile(string filename)
            {
                this._statusFile = new StatusFile(filename);
                return this;
            }

            public void WatchForStatus()
            {
                this.CatchExceptions(delegate {
                    Assert.IsNotNull(this._statusFile, "Internal error: status file not set.");
                    bool flag = false;
                    do
                    {
                        StatusFile.StatusInfo info = this._statusFile.ReadStatus();
                        if (info != null)
                        {
                            switch (info.Status)
                            {
                                case StatusFile.Status.Finished:
                                    flag = true;
                                    break;

                                case StatusFile.Status.Failed:
                                    throw new Exception("Background process failed: " + info.Exception.Message, info.Exception);
                            }
                            Thread.Sleep(100);
                        }
                    }
                    while (!flag);
                });
            }
        }

        private enum InstallationSteps
        {
            MainInstallation,
            WaitForFiles,
            InstallSecurity,
            RunPostAction,
            None,
            Failed
        }

        private enum Result
        {
            Success,
            Failure,
            Abort
        }
    }
}