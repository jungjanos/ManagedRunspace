﻿using System;
using System.IO;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedRunspacePool2
{
    public class RunspaceProxy : IDisposable
    {
        private bool _disposedValue;
        static readonly Type _activatedType = typeof(PsContext);

        PsContext _proxy;
        AppDomain _appDomain;

        public string Owner { get; }
        public string Name { get; }
        public DateTimeOffset Created { get; }
        public DateTimeOffset LastUsed { get; set; }

        private RunspaceProxy(PsContext proxy, AppDomain appDomain, string owner, string name, DateTimeOffset created)
        {
            _proxy = proxy;
            _appDomain = appDomain;
            Owner = owner;
            Name = name;
            Created = created;
        }

        public static RunspaceProxy Create(string ownerKey, DateTimeOffset timestamp, Func<Runspace> runspaceFactory = null)
        {
            ownerKey =
                !string.IsNullOrWhiteSpace(ownerKey)
                ? ownerKey
                : throw new ArgumentNullException(nameof(ownerKey));

            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(_activatedType.Assembly.Location), };
            var adName = ComposeAppDomainName(ownerKey, timestamp);

            AppDomain appDomain = AppDomain.CreateDomain(adName, null, ads);

            PsContext proxy = null;

            try
            {
                proxy = appDomain.CreateInstanceAndUnwrap(
                    _activatedType.Assembly.FullName,
                    _activatedType.FullName,
                    ignoreCase: false,
                    BindingFlags.Default,
                    null,
                    new object[]
                    {
                        runspaceFactory,
                    },
                    null,
                    null
                    ) as PsContext;

                proxy.Init(); 

                return new RunspaceProxy(proxy, appDomain, ownerKey, adName, timestamp);
            }
            catch
            {
                proxy?.Dispose();
                AppDomain.Unload(appDomain);
                throw;
            }
        }

        static string ComposeAppDomainName(string ownerKey, DateTimeOffset timestamp)
            => $"{ownerKey}.{timestamp.ToUnixTimeMilliseconds()}";


        public PsResult Invoke(string psScript)
        {
            psScript = !string.IsNullOrEmpty(psScript) ? psScript : throw new ArgumentNullException(nameof(psScript));
            return _proxy.Invoke(psScript);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _proxy?.Dispose();
                    _proxy = null;

                    try
                    {
                        var __appDomain = _appDomain;
                        if (__appDomain != null)
                        {
                            AppDomain.Unload(__appDomain);
                        }
                    }
                    catch { }
                    finally { _appDomain = null; }
                }

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

    public class InvocationDetails
    {
        public InvocationDetails(string script, TaskCompletionSource<PsResult> taskCompletionSource, CancellationToken clientCancellation)
        {
            Script = script;
            TaskCompletionSource = taskCompletionSource;
            ClientCancellation = clientCancellation;
        }

        public string Script { get; }
        public CancellationToken ClientCancellation { get; }
        public TaskCompletionSource<PsResult> TaskCompletionSource { get; }
    }
}