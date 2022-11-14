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

        public string Name { get; }
        public RunspaceManagerSettings Settings { get; }


        private RunspaceManager(string name, RunspaceManagerSettings settings = null)
        {
            Name = name;
            Settings = settings ?? RunspaceManagerSettings.Defaults;

            _psInvocationQueue = new PsInvocationQueue();
        }

        public async Task<PsResult> InvokeAsync(string script, CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();

            var details = new InvocationDetails(script, new TaskCompletionSource<PsResult>(), cancel);

            await _psInvocationQueue.QueueAsync(details, cancel);

            return await details.TaskCompletionSource.Task;
        }

        public static RunspaceManager Create(string name, RunspaceManagerSettings settings = null)
        {
            var obj = new RunspaceManager(name, settings);
            obj.Start();
            return obj;
        }

        private void Start()
        {
            RenewRunspace();

            // insert cancellation, pin TCS
            Task.Run(Process);
        }

        // ToDo: push cancellation, track queue completed !!!
        // catch exceptions
        private async Task Process()
        {
            var renewTimer = WaitForNextRenew();
            var itemWaiter = WaitForNext();
            var completionWaiter = WaitForComplete();

            while (true)
            {
                await Task.WhenAny(completionWaiter, renewTimer, itemWaiter);

                if (completionWaiter.IsCompleted)
                {
                    await completionWaiter;
                    break; // queue gracefully finished
                }

                if (renewTimer.IsCompleted)
                {
                    RenewRunspace();                    
                    renewTimer = WaitForNextRenew();
                }

                if (itemWaiter.IsCompleted)
                {
                    var queueStatus = await itemWaiter;
                    itemWaiter?.Dispose();

                    if (queueStatus)
                    {
                        if (_psInvocationQueue.TryDequeue(out InvocationDetails invocationDetails))
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

            Task WaitForComplete() => _psInvocationQueue.Completion;
            Task WaitForNextRenew() => Settings.CreateRenewTimer();
            Task<bool> WaitForNext() => _psInvocationQueue.WaitToReadAsync().AsTask(); // ToDo Do we need AsTask() ??? Any chance for double awaition?
        }

        void RenewRunspace()
        {
            _runspaceProxy?.Dispose();
            _runspaceProxy = null;

            // ToDo insert RS factory from settings
            _runspaceProxy = RunspaceProxy.Create(Name, DateTimeOffset.Now, null);
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

    public class RunspaceManagerSettings
    {
        public static RunspaceManagerSettings Defaults = new RunspaceManagerSettings { };

        public TimeSpan? RenewInterval { get; set; }
    }

    static class RunspaceManagerSettingsExt
    {
        static Task _never = new TaskCompletionSource<bool>().Task; // never completes

        public static Task CreateRenewTimer(this RunspaceManagerSettings settings)
            =>
            settings.RenewInterval.HasValue
            ? Task.Delay(settings.RenewInterval.Value)
            : _never;

        public static bool IsPeriodicRenewConfigured(this RunspaceManagerSettings settings)
            => settings.RenewInterval != null;
    }
}
