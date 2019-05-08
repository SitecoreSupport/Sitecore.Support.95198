﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Sitecore.Jobs.AsyncUI;
using Sitecore.Collections;
using Sitecore.Globalization;
using Sitecore.Install.Files;
using Sitecore.Install.Framework;
using Sitecore.Install.Items;
using Sitecore.Install.Security;
using Sitecore.Install.Utils;

namespace Sitecore.Support.Shell.Applications.Install.Dialogs.InstallPackage
{
    internal class UiInstallerEvents : IItemInstallerEvents, IFileInstallerEvents, IAccountInstallerEvents
    {
        #region IItemInstallerEvents Members

        public Pair<BehaviourOptions, bool> AskUser(ItemInfo databaseItem, ItemInfo packageItem, IProcessingContext context)
        {
            bool applyToAll = false;
            BehaviourOptions installOptions = new BehaviourOptions();

            bool idCollision = databaseItem.ID.Equals(packageItem.ID);
            bool showMergeOption = idCollision || databaseItem.TemplateID.Equals(packageItem.TemplateID);

            Hashtable parameters = new Hashtable();
            parameters.Add("id", databaseItem.ID);
            parameters.Add("ph", databaseItem.Path);
            parameters.Add("pc", (!idCollision).ToString());
            parameters.Add("mo", showMergeOption);

            do
            {
                string result = JobContext.ShowModalDialog(parameters, "Installer.GetPasteMode", "600", "375");

                switch (result)
                {
                    case "cancel":
                    case "":
                    case null:
                        Thread.CurrentThread.Abort();
                        break;
                    default:
                        string[] options = result.Split('|');
                        if (options.Length != 3)
                        {
                            Thread.CurrentThread.Abort();
                        }
                        else
                        {
                            installOptions.ItemMode = (InstallMode)Enum.Parse(typeof(InstallMode), options[0]);
                            installOptions.ItemMergeMode = (MergeMode)Enum.Parse(typeof(InstallMode), options[1]);
                            applyToAll = bool.Parse(options[2]);
                        }
                        break;
                }
                if (installOptions.ItemMode == InstallMode.Undefined)
                {
                    JobContext.Alert(Translate.Text("You should select an install mode"));
                }
                else
                {
                    return new Pair<BehaviourOptions, bool>(installOptions, applyToAll);
                }
            }
            while (true);
        }

        #endregion

        #region IFileInstallerEvents Members

        public Pair<bool, bool> RequestOverwrite(string virtualPath, IProcessingContext context)
        {
            string messageText = Translate.Text(Texts.DoYouWishToOverwriteTheFile0, new object[] { virtualPath });
            string result = JobContext.ShowModalDialog(messageText, "YesNoCancelAll", "700", "190");
            switch (result)
            {
                case "no":
                    return new Pair<bool, bool>(false, false);
                case "no to all":
                    return new Pair<bool, bool>(false, true);
                case "yes":
                    return new Pair<bool, bool>(true, false);
                case "yes to all":
                    return new Pair<bool, bool>(true, true);
                case "cancel":
                default:
                    Thread.CurrentThread.Abort();
                    return new Pair<bool, bool>(false, true);
            }
        }

        public void BeforeCommit()
        {
            JobContext.SendMessage("installer:commitingFiles");
        }

        #endregion

        #region IAccountInstallerEvents Members

        readonly List<string> _skippedMessages = new List<string>();

        public void ShowWarning(string message, string warningType)
        {
            if (_skippedMessages.Contains(warningType))
            {
                return;
            }
            var result = JobContext.ShowModalDialog(message, "ContinueAlwaysAbort", "500", "190");
            switch (result)
            {
                case "continue":
                    return;
                case "always":
                    _skippedMessages.Add(warningType);
                    return;
                case "abort":
                    Thread.CurrentThread.Abort();
                    return;
                default:
                    throw new Exception("Unexpected dialog value");
            }
        }

        #endregion
    }
}