using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ManagedRunspacePool2
{
    public class PsInvocationQueue
    {
        Channel<InvocationDetails> _channel;
        const int DEFAULT_CAPACITY = 1;
        public PsInvocationQueue()
        {
            var options = new BoundedChannelOptions(DEFAULT_CAPACITY)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            };
            
            _channel = Channel.CreateBounded<InvocationDetails>(options);
        }
        // ToDo: set Complete on queue



        // ToDo => optimize away async-await
        public async Task QueueAsync(InvocationDetails details, CancellationToken cancel)
        {
            while (true)
            {
                cancel.ThrowIfCancellationRequested();

                if (_channel.Writer.TryWrite(details))
                    return;

                await _channel.Writer.WaitToWriteAsync(cancel);
            }
        }

        public void Complete() => _channel.Writer.TryComplete();

        public bool TryDequeue(out InvocationDetails item)
            => _channel.Reader.TryRead(out item);

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancel = default) =>
            _channel.Reader.WaitToReadAsync(cancel);

        public Task Completion => _channel.Reader.Completion;
    }
}
