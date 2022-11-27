using System.Threading;
using System.Threading.Tasks;

namespace ManagedRunspace2
{
    public class InvocationContext
    {
        public InvocationContext(string script, bool useLocalScope, TaskCompletionSource<PsResult> taskCompletionSource, CancellationToken clientCancellation)
        {
            Script = (script, useLocalScope);
            TaskCompletionSource = taskCompletionSource;
            ClientCancellation = clientCancellation;
        }

        public Script Script { get; }
        public CancellationToken ClientCancellation { get; }
        public TaskCompletionSource<PsResult> TaskCompletionSource { get; }
    }
}
