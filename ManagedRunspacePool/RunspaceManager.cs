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
            while (true)
            {
                bool shouldDelay = true;
                
                if (_periodicRenewSet)
                {
                    RenewRunspace();
                    shouldDelay = false;
                }
                
                if (_psInvocationQueue.TryDequeue(out InvocationDetails invocationDetails))
                {
                    var script = invocationDetails.Script;
                    var tcs = invocationDetails.TaskCompletionSource;

                    var result = _runspaceProxy.Invoke(script);
                    tcs.SetResult(result);
                    shouldDelay = false;
                }

                if (shouldDelay)
                    await Task.Delay(10);
            }
        }

        void RenewRunspace()
        {
            _runspaceProxy?.Dispose();
            _runspaceProxy = null;

            // ToDo insert RS factory from settings
            _runspaceProxy = RunspaceProxy.Create(Name, DateTimeOffset.Now, null);
        }
        bool _periodicRenewSet => Settings.RenewInterval != null;

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
}
