using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
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
    }
}
