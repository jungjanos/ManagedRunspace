﻿using ManagedRunspacePool2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class RunspaceManagerTests
    {
        [TestMethod]
        public async Task Create()
        {
            var manager = RunspaceManager.Create("manager1");

            var results = await manager.InvokeAsync("Get-Host");
            var result = results.Results.First();
        }

        [TestMethod]
        public async Task RunParallel()
        {
            using (var manager = RunspaceManager.Create("manager1"))
            {
                var tasks = Enumerable.Range(1, 100).Select(
                    _ =>
                    Task.Run<PsResult>(async () => await manager.InvokeAsync("gci"))
                    );

                var results = await Task.WhenAll(tasks);
            }
        }
    }
}