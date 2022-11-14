using ManagedRunspacePool2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class RunspaceProxyTests
    {
        [TestMethod]
        public void Create_managed_runspace()
        {
            using (var mrs = RunspaceProxy.Create("owner1", DateTimeOffset.Now))
            {

            }
        }

        [TestMethod]
        public void Invoke_script()
        {
            using (var mrs = RunspaceProxy.Create("owner1", DateTimeOffset.Now))
            {
                var result = mrs.Invoke("gci");
                Trace.WriteLine($"\"gci\" => {result.Results.Count} items");

                var result2 = mrs.Invoke("will get errors");

                mrs.Invoke($"$a = 5");
                var result3 = mrs.Invoke("$a");

                Assert.AreEqual(5, (int)result3.Results.First().ImmediateBaseObject);
            }
        }
    }
}
