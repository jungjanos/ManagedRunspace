using System;
using System.Management.Automation.Runspaces;

namespace ManagedRunspacePool2
{
    /// <summary>
    /// Settings for the house keeping tasks of remote runspaces
    /// </summary>
    public class RunspaceAgentSettings
    {
        public static RunspaceAgentSettings Defaults 
            => new RunspaceAgentSettings 
            { 
                RenewInterval = null,
                RunspaceFactory = null,
                ShutdownGracePeriod = null,
                InitScript = null,
                ClosingScript = null,
            };

        /// <summary>
        /// Time period for tear-down and re-creation of the remote runspace. Null = never
        /// </summary>
        public TimeSpan? RenewInterval { get; set; }
        
        /// <summary> Factory-method to be executed for the creation of remote runspaces </summary>
        public Func<Runspace> RunspaceFactory { get; set; }

        /// <summary>Time allowance to finishing running script execution tasks when the agent shuts down</summary>
        public TimeSpan? ShutdownGracePeriod { get; set; }

        // Script to be executed directly after the remote runspace has been created. After init script execution,
        // the runspace is considered to be ready for client script execution
        public Script InitScript { get; set; }

        // Script to be executed prior the remote runspace is closed. (e.g. closing sessions which were opened by the init-script)
        public Script ClosingScript { get; set; }
    }
}
