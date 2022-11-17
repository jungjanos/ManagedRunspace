using ManagedRunspacePool2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class Sandbox
    {
        [TestMethod]
        public async Task Cancel_Delay_throws()
        {
            var cts = new CancellationTokenSource(100);

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => Task.Delay(int.MaxValue, cts.Token));
        }

        [TestMethod]
        public async Task WaitEitherEvent()
        {
            var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });

            var produce = Task.Run(async () =>
            {
                await Task.Delay(200);
                channel.Writer.TryWrite(1);
                await Task.Delay(1000);
                channel.Writer.TryWrite(2);
                await Task.Delay(200);
                channel.Writer.TryWrite(3);
                channel.Writer.Complete();
            });

            var waiter = GetWaiter();
            var itemavailable = ReadNext();
            var finish = QueueCompleted();

            while (true)
            {
                await Task.WhenAny(waiter, itemavailable, finish);

                if (waiter.IsCompleted)
                {
                    await waiter;
                    Trace.WriteLine("Waiter completed");
                    waiter = GetWaiter();
                }

                if (itemavailable.IsCompleted)
                {
                    var item = await itemavailable;
                    Trace.WriteLine($"Reader completed with {item}");
                    itemavailable = ReadNext();
                }

                if (finish.IsCompleted)
                {
                    await finish;
                    Trace.WriteLine($"Queue completed");
                    break;
                }
            }

            async Task GetWaiter() => await Task.Delay(500);

            async Task<int> ReadNext() => await channel.Reader.ReadAsync();

            async Task QueueCompleted() => await channel.Reader.Completion;
        }

        [TestMethod]
        public async Task WaitEitherEvent2()
        {
            var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });

            var produce = Task.Run(async () =>
            {
                await Task.Delay(200);
                channel.Writer.TryWrite(1);
                await Task.Delay(1000);
                channel.Writer.TryWrite(2);
                await Task.Delay(200);
                channel.Writer.TryWrite(3);
                channel.Writer.Complete();
            });

            var waiter = GetWaiter();
            var itemavailable = WaitForNext();
            var finish = QueueCompleted();

            while (true)
            {
                var completedTask = await Task.WhenAny(waiter, itemavailable, finish);

                if (completedTask == waiter)
                {
                    await waiter;
                    Trace.WriteLine("Waiter completed");
                    waiter = GetWaiter();
                }

                else if (completedTask == itemavailable)
                {
                    if (await itemavailable && channel.Reader.TryRead(out int item))
                    {
                        Trace.WriteLine($"Read item: {item}");
                        itemavailable = WaitForNext();
                    }
                }

                if (finish.IsCompleted)
                {
                    if (finish.IsFaulted)
                        await finish;

                    Trace.WriteLine($"Queue completed");
                    break;
                }
            }

            async Task GetWaiter() => await Task.Delay(500);

            async Task<bool> WaitForNext() => await channel.Reader.WaitToReadAsync();

            async Task QueueCompleted() => await channel.Reader.Completion;
        }


        [TestMethod]
        public async Task WaitOnAlreadyCompletedTask()
        {
            var t1 = Task.Delay(1000);
            var t2 = Task.Delay(100);

            await t1;

            await Task.WhenAny(t2);
            await Task.WhenAny(t2);
            await Task.WhenAny(t2);

            await Task.WhenAny(Task.CompletedTask);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task Read_from_closed_Channel_throws_ChannelClosedException()
        {
            var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });
            channel.Writer.Complete();

            await Assert.ThrowsExceptionAsync<ChannelClosedException>(() => channel.Reader.ReadAsync().AsTask());
        }

        [TestMethod]
        public async Task WaitToRead_from_closed_Channel_ReturnsFalse()
        {
            var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });
            channel.Writer.Complete();
            bool result = await channel.Reader.WaitToReadAsync();
            Trace.Write(result);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Executing_in_local_scope()
        {
            var rs = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
            rs.Open();
            var ps = PowerShell.Create();
            ps.Runspace = rs;

            ps.AddScript("$a = 5; Write-Output $a", useLocalScope: true);
            var res1 = ps.Invoke();
            ps.Dispose();


            var ps2 = PowerShell.Create();
            ps2.Runspace = rs;

            ps2.AddScript("Write-Output $a", useLocalScope: true);
            var res2 = ps2.Invoke();
            ps2.Dispose();

            Assert.IsNull(res2.First());
        }

        //[TestMethod]
        //public async Task RunspaceManager_with_Init_and_ClosingScript()
        //{
        //    using (var manager = RunspaceManager.Create("manager1", new RunspaceManagerSettings
        //    {
        //        InitScript = "$init= $true",
        //        ClosingScript = "$closed = $true",
        //        RenewInterval = TimeSpan.FromSeconds(30),
        //    }))
        //    {
        //        await Task.Delay(TimeSpan.FromSeconds(300));
        //    };
        //}

        [TestMethod]
        public async Task RunspaceManager_closing()
        {
            var script = "Start-Sleep -Seconds 1; Write-Output $init";

            var cts = new CancellationTokenSource(2000);

            using (var manager = RunspaceManager.Create("manager1", new RunspaceManagerSettings
            {
                InitScript = "$init= 5",
                ClosingScript = "$closed = $true",
                RenewInterval = TimeSpan.FromSeconds(10),
            }, cts.Token))
            {
                var tasks = Enumerable.Range(1, 20).Select
                    (_ => Task.Run(async () => await manager.InvokeAsync(script)))
                    .ToArray();
                PsResult[] results;

                try
                {
                      results = await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                { 
                }
            }
        }
    }
}
