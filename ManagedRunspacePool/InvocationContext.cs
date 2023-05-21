using System.Threading;
using System.Threading.Tasks;

namespace ManagedRunspacePool2
{
    /// <summary>
    /// Model carrying the script invocation and the Task/TaskCompletionSource that will contain the invocation's result
    /// in a single object traversing a producer-consumer queue.
    /// </summary>
    public class InvocationContext
    {
        
        /// <param name="clientCancellation">for client side cancel of script execution</param>
        public InvocationContext(string script, bool useLocalScope, TaskCompletionSource<PsResult> taskCompletionSource, CancellationToken clientCancellation)
        {
            Script = (script, useLocalScope);            
            TaskCompletionSource = taskCompletionSource;
            ClientCancellation = clientCancellation;
        }

        public Script Script { get; }

        /// <summary>for client side cancel of script execution</summary>
        public CancellationToken ClientCancellation { get; }

        /// <summary>Contains the Task'PsResult' that the client needs to await on</summary>
        public TaskCompletionSource<PsResult> TaskCompletionSource { get; }
    }
}
