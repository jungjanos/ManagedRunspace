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
                var result = mrs.Invoke("gci", true);
                Trace.WriteLine($"\"gci\" => {result.Results.Count} items");

                var result2 = mrs.Invoke("will get errors", false);

                mrs.Invoke($"$a = 5", false);
                var result3 = mrs.Invoke("$a", true);

                Assert.AreEqual(5, (int)result3.Results.First().ImmediateBaseObject);
            }
        }
    }
}
