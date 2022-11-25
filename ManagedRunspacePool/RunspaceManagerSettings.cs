using System;
using System.Management.Automation.Runspaces;

namespace ManagedRunspacePool2
{
    public class ManagedRunspaceSettings
    {
        public static ManagedRunspaceSettings Defaults 
            => new ManagedRunspaceSettings 
            { 
                RenewInterval = null,
                RunspaceFactory = null,
                ShutdownGracePeriod = null,
                InitScript = null,
                ClosingScript = null,
            };

        public TimeSpan? RenewInterval { get; set; }
        public Func<Runspace> RunspaceFactory { get; set; }
        public TimeSpan? ShutdownGracePeriod { get; set; }

        public Script InitScript { get; set; }
        public Script ClosingScript { get; set; }
    }
}
