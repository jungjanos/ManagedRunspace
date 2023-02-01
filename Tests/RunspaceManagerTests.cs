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
                    //Task.Run<PsResult>(async () => await manager.InvokeAsync("gci")) // <- FAST
                    // Task.Run<PsResult>(async () => await manager.InvokeAsync("Get-Process")) //<- SLOW!!!!!!
                    Task.Run<PsResult>(async () => await manager.InvokeAsync("Get-Process | select Name,Id")) //<- FAST!!!!!!
                    );

                var results = await Task.WhenAll(tasks);
            }
        }

        [TestMethod]
        public async Task Scripts_by_default_are_using_global_scope()
        {
            using (var manager = RunspaceManager.Create("manager1"))
            {
                await manager.InvokeAsync("$a = 5; Write-Output $a");
                var res2 = await manager.InvokeAsync("Write-Output $a");

                Assert.AreEqual(5, res2.Results[0].ImmediateBaseObject);
            }
        }

        [TestMethod]
        public async Task Adding_item_to_completed_PsInvocationQueue_return_false()
        {
            var queue = new PsInvocationQueue();
            var item1 = new InvocationContext("script1", false, new TaskCompletionSource<PsResult>(), default);
            var item2 = new InvocationContext("script2", false, new TaskCompletionSource<PsResult>(), default);

            await queue.QueueAsync(item1, default);

            queue.Complete();
            Assert.IsFalse(await queue.QueueAsync(item2, default));
        }

    }
}
