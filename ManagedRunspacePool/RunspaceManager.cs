using ManagedRunspacePool2;
using System;

namespace ManagedRunspacePool
{
    public class RunspaceManager : IDisposable
    {
        private bool disposedValue;


        public void CreateAgent(string name, RunspaceAgentSettings settings)
        {

        }

        public void GetAgent(string name)
        {

        }

        public void ShutDown()
        {

        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
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
