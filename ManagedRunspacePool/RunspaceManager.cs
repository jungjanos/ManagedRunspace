using System;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedRunspacePool2
{
    public class RunspaceManager : IDisposable
    {
        RunspaceProxy _runspaceProxy = null;
        PsInvocationQueue _psInvocationQueue = null;
        private bool _disposedValue;
        private readonly CancellationToken _cancel;

        public string Name { get; }
        public RunspaceManagerSettings Settings { get; }


        private RunspaceManager(string name, RunspaceManagerSettings settings = null, CancellationToken cancel = default)
        {
            Name = name;
            Settings = settings ?? RunspaceManagerSettings.Defaults;
            _psInvocationQueue = new PsInvocationQueue();
            _cancel = cancel;

            _cancel.Register(() => _psInvocationQueue.Complete());
        }

        // ToDo inconsistent cancelation results when
        // manager is already shutting down but receives invocation
        // invocation is already in the queue but manager deceides to shut down
        public async Task<PsResult> InvokeAsync(string script, bool useLocalScope = false, CancellationToken cancel = default)
        {
            var tcs = new TaskCompletionSource<PsResult>();

            if (_cancel.IsCancellationRequested) // Manager is shutting down, ingest is closed 
                tcs.SetException(new InvalidOperationException($"{nameof(RunspaceManager)} is already shutting down"));

            else if (cancel.IsCancellationRequested) // client originated cancellation            
                tcs.SetCanceled();

            else // prepare and queue command
            {
                var details = new InvocationContext(script, useLocalScope, tcs, cancel);
                var queuingSuccess = await _psInvocationQueue.QueueAsync(details, cancel);

                if (!queuingSuccess)
                    tcs.SetException(new InvalidOperationException($"{nameof(RunspaceManager)} is already shutting down"));
            }

            return await tcs.Task;
        }


        public static RunspaceManager Create(string name, RunspaceManagerSettings settings = null, CancellationToken cancel = default)
        {
            var obj = new RunspaceManager(name, settings, cancel);
            obj.Start();
            return obj;
        }

        private void Start()
        {
            _cancel.ThrowIfCancellationRequested();

            RenewRunspace();

            Task.Run(Process);
        }

        // ToDo: push cancellation, !!!
        // catch exceptions
        private async Task Process()
        {
            var renewTimer = WaitForNextRenew();
            var itemWaiter = WaitForNext();
            var completionWaiter = WaitForComplete();
            var abortWaiter = GetAbortWaiter();

            while (true)
            {
                await Task.WhenAny(abortWaiter, completionWaiter, renewTimer, itemWaiter);

                if (abortWaiter.IsCompleted)
                {
                    TryAbortInvocations();
                    break;
                }

                if (completionWaiter.IsCompleted)
                {
                    await completionWaiter;
                    break; // queue gracefully finished
                }

                if (renewTimer.IsCompleted)
                {
                    CloseRunspace();
                    RenewRunspace();
                    renewTimer = WaitForNextRenew();
                }

                if (itemWaiter.IsCompleted)
                {
                    var queueStatus = await itemWaiter;
                    itemWaiter?.Dispose();

                    if (queueStatus)
                    {
                        if (_psInvocationQueue.TryDequeue(out InvocationContext invocationDetails))
                        {
                            var script = invocationDetails.Script;
                            var tcs = invocationDetails.TaskCompletionSource;
                            var clientCancel = invocationDetails.ClientCancellation;

                            if (clientCancel.IsCancellationRequested)
                                tcs.TrySetCanceled(clientCancel);

                            else
                            {
                                var result = _runspaceProxy.Invoke(script);
                                tcs.SetResult(result);
                            }
                        }

                        itemWaiter = WaitForNext();
                    }
                }
            }

            CloseRunspace();

            // wait for graceful completion of processing
            Task WaitForComplete() => _psInvocationQueue.Completion;            
            
            Task WaitForNextRenew() => Settings.CreateRenewTimer();
            
            Task<bool> WaitForNext() => _psInvocationQueue.WaitToReadAsync().AsTask(); // ToDo Do we need AsTask() ??? Any chance for double awaition?            

            // wait for processing abortion deadline. (RunspaceManager's cancel + grace period) 
            Task GetAbortWaiter()
            {
                var tcs = new TaskCompletionSource<bool>();

                _cancel.Register(async () =>
                {
                    await Task.Delay(Settings.ShutdownGracePeriod ?? TimeSpan.Zero);
                    tcs.SetResult(true);
                });

                return tcs.Task;
            };
        }

        void CloseRunspace()
        {
            if (Settings.ClosingScript != null && _runspaceProxy != null)
                _runspaceProxy?.Invoke(Settings.ClosingScript);

            _runspaceProxy?.Dispose();
            _runspaceProxy = null;
        }

        void RenewRunspace()
        {
            _runspaceProxy?.Dispose();
            _runspaceProxy = null;

            _runspaceProxy = RunspaceProxy.Create(Name, DateTimeOffset.Now, Settings.RunspaceFactory);

            if (Settings.InitScript != null)
                _runspaceProxy.Invoke(Settings.InitScript);
        }

        private void TryAbortInvocations()
        {
            while(_psInvocationQueue.TryDequeue(out InvocationContext invocation))            
                invocation.TaskCompletionSource.TrySetException(new TaskCanceledException($"Invocation processing aborted due to {nameof(RunspaceManager)} shutting down"));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _runspaceProxy?.Dispose();
                }

                _disposedValue = true;
                _runspaceProxy = null;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
