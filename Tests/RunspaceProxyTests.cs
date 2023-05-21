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
        public void Create_RunspaceProxy()
        {
            var now = DateTimeOffset.Now;
            using (var mrs1 = RunspaceProxy.Create("owner1", now))
            using (var mrs2 = RunspaceProxy.Create("owner2", now))
            {

            }
            
            // TODO: understand why 2x RunspaceProxy can be created with the same ownername/timestamp (owner1-now, owner1-now)
        }

        [TestMethod]
        public void Invoke_script_on_RunspaceProxy()
        {
            using (var mrs = RunspaceProxy.Create("owner1", DateTimeOffset.Now))
            {
                var result = mrs.Invoke(new Script("gci", true));
                Trace.WriteLine($"\"gci\" => {result.Results.Count} items");

                var result2 = mrs.Invoke("will get errors");

                mrs.Invoke($"$a = 5");
                var result3 = mrs.Invoke(new Script("$a", true));

                Assert.AreEqual(5, (int)result3.Results.First().ImmediateBaseObject);
            }
        }
    }
}
