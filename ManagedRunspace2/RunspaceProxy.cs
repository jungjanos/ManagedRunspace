﻿using System;
using System.IO;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace ManagedRunspace2
{
    public class RunspaceProxy : IDisposable
    {
        private bool _disposedValue;
        static readonly Type _activatedType = typeof(PsContext);

        PsContext _proxy;
        AppDomain _appDomain;

        public string Owner { get; }
        public string Name { get; }        

        private RunspaceProxy(PsContext proxy, AppDomain appDomain, string owner, string name)
        {
            _proxy = proxy;
            _appDomain = appDomain;
            Owner = owner;
            Name = name;            
        }

        public static RunspaceProxy Create(string ownerKey, DateTimeOffset timestamp, Func<Runspace> runspaceFactory = null)
        {
            ownerKey =
                !string.IsNullOrWhiteSpace(ownerKey)
                ? ownerKey
                : throw new ArgumentNullException(nameof(ownerKey));

            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(_activatedType.Assembly.Location), };
            var adName = ComposeAppDomainName(ownerKey, timestamp);

            AppDomain appDomain = null;            
            try { appDomain = AppDomain.CreateDomain(adName, null, ads); } 
            catch (Exception ex) { throw new ManagedRunsapceException(ManagedRunsapceException.ErrorType.RunspaceProxyCreation, $"Failed to create AppDomain: {adName}", ex); }

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
                return new RunspaceProxy(proxy, appDomain, ownerKey, adName);
            }            
            catch (Exception ex)
            {
                proxy?.Dispose();
                AppDomain.Unload(appDomain);
                throw new ManagedRunsapceException(ManagedRunsapceException.ErrorType.RunspaceProxyCreation, "Failed to create remote proxy", ex);
            }
        }

        static string ComposeAppDomainName(string ownerKey, DateTimeOffset timestamp)
            => $"{ownerKey ?? throw new ArgumentNullException(ownerKey)}.{timestamp.ToUnixTimeMilliseconds()}";


        public PsResult Invoke(Script script)
            => _proxy.Invoke(script);


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
}
