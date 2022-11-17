using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ManagedRunspacePool2
{
    public class PsInvocationQueue
    {
        Channel<InvocationContext> _channel;
        const int DEFAULT_CAPACITY = 1;
        public PsInvocationQueue()
        {
            var options = new BoundedChannelOptions(DEFAULT_CAPACITY)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            };
            
            _channel = Channel.CreateBounded<InvocationContext>(options);
        }        

        public async Task<bool> QueueAsync(InvocationContext details, CancellationToken cancel)
        {
            while (true)
            {
                cancel.ThrowIfCancellationRequested();

                if (_channel.Writer.TryWrite(details))
                    return true;                
                
                if (!await _channel.Writer.WaitToWriteAsync(cancel))
                    return false;
            }
        }

        public void Complete() => _channel.Writer.TryComplete();

        public bool TryDequeue(out InvocationContext item)
            => _channel.Reader.TryRead(out item);

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancel = default) 
            => _channel.Reader.WaitToReadAsync(cancel);

        public Task Completion => _channel.Reader.Completion;
    }    
}
