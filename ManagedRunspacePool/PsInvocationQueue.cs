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



        // push cancellation
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

        public bool TryDequeue(out InvocationDetails item)
            => _channel.Reader.TryRead(out item);        
    }
}
