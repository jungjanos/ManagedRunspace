using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace ManagedRunspace2
{
    public class PsContext : MarshalByRefObject, IDisposable
    {
        Runspace _psRunspace;
        private bool _disposedValue;
        private readonly Func<Runspace> _runspaceFactory;

        public PsContext(Func<Runspace> runspaceFactory)
        {
            _runspaceFactory =
                runspaceFactory
                ?? new Func<Runspace>(() => RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2()));
        }

        public void Init()
        {
            try { _psRunspace = _runspaceFactory(); }
            catch (Exception ex) { throw new ManagedRunsapceException(ManagedRunsapceException.ErrorType.RunspaceInitialization, "Failed to create runspace from factory", ex);}

            var state = _psRunspace.RunspaceStateInfo.State;

            if (state != RunspaceState.BeforeOpen && state != RunspaceState.Opened)
                throw new ManagedRunsapceException(ManagedRunsapceException.ErrorType.RunspaceInitialization, $"Created runspace was in unexpected state: {state}");

            if (_psRunspace.RunspaceStateInfo.State == RunspaceState.BeforeOpen)
            {
                try { _psRunspace.Open(); }
                catch (Exception ex) { throw new ManagedRunsapceException(ManagedRunsapceException.ErrorType.RunspaceInitialization, "Failed to open runspace", ex); }
            }
        }

        public PsResult Invoke(Script script)
        {
            using (var ps = PowerShell.Create())
            {
                ps.Runspace = _psRunspace;

                if (script.UseLocalScope)
                    ps.AddScript(script.ScriptText, useLocalScope: script.UseLocalScope);
                else
                    ps.AddScript(script.ScriptText);

                Collection<PSObject> results = null;
                object[] errors = null;
                Exception exception = null;

                try
                {
                    results = ps.Invoke();

                    if (ps.Streams.Error != null && ps.Streams.Error.Count > 0)
                        errors = ps.Streams.Error.Cast<object>().ToArray();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                return new PsResult(results, errors, exception);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _psRunspace?.Dispose();
                    _psRunspace = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
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
