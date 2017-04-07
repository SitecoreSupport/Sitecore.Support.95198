using Sitecore.Diagnostics;
using Sitecore.Install;
using Sitecore.Install.Framework;
using Sitecore.Install.Zip;
using Sitecore.Support.Install.Security;
using System;

namespace Sitecore.Support.Install
{
    public class Installer : Sitecore.Install.Installer
    {
        public new void InstallSecurity(string path, IProcessingContext context)
        {
            Assert.ArgumentNotNullOrEmpty(path, "path");
            Assert.ArgumentNotNull(context, "context");
            Log.Info("Installing security from package: " + path, this);
            PackageReader reader = new PackageReader(path);
            AccountInstaller sink = new AccountInstaller();
            sink.Initialize(context);
            reader.Populate(sink);
            sink.Flush();
            sink.Finish();
        }
    }
}