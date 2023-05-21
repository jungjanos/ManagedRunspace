using ManagedRunspacePool2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace Tests
{
    [TestClass]
    public class RemoteRunspaceTests
    {
        [TestMethod]
        public void Remote_activate_runspace_wrapper()
        {
            // create appdomain, create and use proxy
            var t = typeof(RemoteRunspace);
            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(t.Assembly.Location), };
            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(
                t.Assembly.FullName,
                t.FullName,
                ignoreCase: false,
                BindingFlags.Default,
                null,
                new object[] { null },
                null,
                null
                ) as RemoteRunspace;

            proxy.Init();

            proxy.Dispose();
        }


        [TestMethod]
        public void Invoke_script_on_runspace_wrapper()
        {
            // create appdomain, create and use proxy
            var t = typeof(RemoteRunspace);
            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(t.Assembly.Location), };
            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(
                t.Assembly.FullName,
                t.FullName,
                ignoreCase: false,
                BindingFlags.Default,
                null,
                new object[] { null },
                null,
                null
                ) as RemoteRunspace;

            proxy.Init();

            var result = proxy.Invoke("Get-Host");

            Trace.WriteLine($"Returned {result.Results.Count} results");
            proxy.Dispose();
            AppDomain.Unload(ad1);

            Assert.AreEqual(1, result.Results.Count);
        }
        [TestMethod] // Works!!!
        public void Invoke_script_on_RemoteRunspace_runspace()
        {
            // create appdomain, create and use proxy
            var t = typeof(RemoteRunspace);
            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(t.Assembly.Location), };
            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(
                t.Assembly.FullName,
                t.FullName,
                ignoreCase: false,
                BindingFlags.Default,
                null,
                new object[]
                {
                    new Func<Runspace>(() =>
                    {
                        var iss = InitialSessionState.CreateDefault();
                        iss.ImportPSModule(new string [] {"Microsoft.PowerShell.Utility" });
                        return RunspaceFactory.CreateRunspace(iss);
                    })
                },
                null,
                null
                ) as RemoteRunspace;

            proxy.Init();

            var result = proxy.Invoke("Get-Host");

            Trace.WriteLine($"Returned {result.Results.Count} results");
            proxy.Dispose();
            AppDomain.Unload(ad1);

            Assert.AreEqual(1, result.Results.Count);
        }
    }
}
