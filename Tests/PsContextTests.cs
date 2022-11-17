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
    public class PsContextTests
    {
        [TestMethod]
        public void Remote_activate_runspace_wrapper()
        {
            // create appdomain, create and use proxy
            var t = typeof(PsContext);
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
                ) as PsContext;

            proxy.Init();

            proxy.Dispose();
        }


        [TestMethod]
        public void Invoke_script_on_runspace_wrapper()
        {
            // create appdomain, create and use proxy
            var t = typeof(PsContext);
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
                ) as PsContext;

            proxy.Init();

            var result = proxy.Invoke("Get-Host", false);

            Trace.WriteLine($"Returned {result.Results.Count} results");
            proxy.Dispose();
            AppDomain.Unload(ad1);
        }
        [TestMethod] // Works!!!
        public void Invoke_script_on_runspace_wrapper_custom_runspace()
        {
            // create appdomain, create and use proxy
            var t = typeof(PsContext);
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
                ) as PsContext;

            proxy.Init();

            var result = proxy.Invoke("Get-Host", false);

            Trace.WriteLine($"Returned {result.Results.Count} results");
            proxy.Dispose();
            AppDomain.Unload(ad1);
        }
    }
}
