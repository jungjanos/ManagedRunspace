using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ManagedRunspace2
{
    public class Agent<TWorkItem, TState>
    {
        private readonly BlockingCollection<TWorkItem> _workItems;
        private readonly Func<TState, TWorkItem, TState> _process;

        public static Agent<TWorkItem, TState> Start(BlockingCollection<TWorkItem> workItems, Func<TState> createState, Func<TState, TWorkItem, TState> process)
        {
            if (createState is null)
                throw new ArgumentNullException(nameof(createState));

            if (process is null)
                throw new ArgumentNullException(nameof(process));

            var agent = new Agent<TWorkItem, TState>(workItems, process);
            agent.Run(createState);

            return agent;
        }

        Agent(BlockingCollection<TWorkItem> workItems, Func<TState, TWorkItem, TState> processState)
        {
            _workItems = workItems ?? throw new ArgumentNullException(nameof(workItems));
            _process = processState ?? throw new ArgumentNullException(nameof(processState));
        }

        void Run(Func<TState> createState)
        {
            var initialState = createState();

            Task.Factory.StartNew(() =>
            {
                try { Process(initialState); }
                catch { }
            }, TaskCreationOptions.LongRunning);
        }

        void Process(TState initialState)
        {
            TState state = initialState;

            try
            {
                while (true)
                {
                    if (_workItems.IsCompleted)
                        break;

                    _workItems.TryTake(out TWorkItem item, TimeSpan.FromMilliseconds(10));
                    state = _process(state, item);
                }
            }

            finally
            {
                (state as IDisposable)?.Dispose();
                state = default;
            }
        }
    }

    public static class RunspaceAgentHelper
    {
        public static Func<RunspaceAgentState> CreateInitialStateFactory(string agentName, ManagedRunspaceSettings settings, Func<DateTimeOffset> timeProvider = null)
            => () =>
            {
                if (agentName is null)
                    throw new ArgumentNullException(nameof(agentName));

                if (settings is null)
                    throw new ArgumentNullException(nameof(settings));

                IRunspaceAgentStateOperations runspaceAgentStateOperations = RunspaceAgentState
                                    .CreateEmpty(agentName, settings, timeProvider)
                                    .CreateAndInitRunspace();

                return (OpenState)runspaceAgentStateOperations;
            };

        public static Func<RunspaceAgentState, InvocationContext, RunspaceAgentState> DefaultProcessor => DefaultProcessor_;

        static RunspaceAgentState DefaultProcessor_(RunspaceAgentState state, InvocationContext input)
        {
            IRunspaceAgentStateOperations currentState = state;
            var time = state.TimeProvider();

            if (state.Settings.IsPeriodicRenewConfigured() && time - state.RunspaceCreated > state.Settings.RenewInterval)
                currentState = currentState.RenewRunspace();

            if (input != null)
                currentState = currentState.ProcessInvocation(input);

            return (RunspaceAgentState)currentState;
        }
    }

    public interface IRunspaceAgentStateOperations 
    {
        IRunspaceAgentStateOperations CreateAndInitRunspace();        
        IRunspaceAgentStateOperations RenewRunspace();
        IRunspaceAgentStateOperations CloseRunspace();
        IRunspaceAgentStateOperations ProcessInvocation(InvocationContext invocation);
    }

    public class EmptyState : RunspaceAgentState
    {
        internal EmptyState(string agentName, ManagedRunspaceSettings settings, Func<DateTimeOffset> timeProvider) 
            : base(agentName, default, null, settings, timeProvider) { }

        public override IRunspaceAgentStateOperations CreateAndInitRunspace()
        {
            var proxy = CreateAndInitProxy();
            return new OpenState(AgentName, TimeProvider(), proxy, Settings, TimeProvider);            
        }

        public override IRunspaceAgentStateOperations CloseRunspace() => this;

        public override IRunspaceAgentStateOperations RenewRunspace()
            => throw new InvalidOperationException("Operation not available in this state");

        public override IRunspaceAgentStateOperations ProcessInvocation(InvocationContext invocation)
            => throw new InvalidOperationException("No runspace opened");
    }

    public class OpenState : RunspaceAgentState
    {
        internal OpenState(string agentName, DateTimeOffset runspaceCreated, RunspaceProxy runspace, ManagedRunspaceSettings settings, Func<DateTimeOffset> timeProvider)
            : base(agentName, runspaceCreated, runspace, settings, timeProvider) { }
        

        public override IRunspaceAgentStateOperations CreateAndInitRunspace()
            => throw new InvalidOperationException("Operation not available in this state");

        public override IRunspaceAgentStateOperations RenewRunspace()
        {
            CloseRunspace();
            var newRunspace = CreateAndInitProxy();

            return new OpenState(AgentName, TimeProvider(), newRunspace, Settings, TimeProvider);            
        }

        public override IRunspaceAgentStateOperations CloseRunspace()
        {
            if (Settings.ClosingScript != null)
                Runspace?.Invoke(Settings.ClosingScript);

            // !!!!!!!!!!             
            Runspace?.Dispose();
            Runspace = null;

            return new EmptyState(AgentName, Settings, TimeProvider);
        }

        public override IRunspaceAgentStateOperations ProcessInvocation(InvocationContext invocation)
        {
            if (invocation is null) 
                throw new ArgumentNullException(nameof(invocation));

            var script = invocation.Script;
            var tcs = invocation.TaskCompletionSource;
            var clientCancel = invocation.ClientCancellation;

            if (clientCancel.IsCancellationRequested)
                tcs.TrySetCanceled(clientCancel);

            else
            {
                PsResult result = null;
                Exception exception = null;
                
                try { result = Runspace.Invoke(script); }
                catch (Exception ex) { exception = ex; }

                if (exception != null)
                    tcs.TrySetException(exception);
                else
                    tcs.SetResult(result);
            }

            return this;
        }
    }

    public abstract class RunspaceAgentState : IRunspaceAgentStateOperations
    {
        public readonly DateTimeOffset RunspaceCreated;
        public RunspaceProxy Runspace;

        public readonly string AgentName;
        public readonly ManagedRunspaceSettings Settings;
        public readonly Func<DateTimeOffset> TimeProvider;

        protected RunspaceAgentState(string agentName, DateTimeOffset runspaceCreated, RunspaceProxy runspace, ManagedRunspaceSettings settings, Func<DateTimeOffset> timeProvider)
        {
            AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            TimeProvider = timeProvider ?? new Func<DateTimeOffset>(() => DateTimeOffset.Now);

            RunspaceCreated = runspaceCreated;
            Runspace = runspace;
        }

        public static RunspaceAgentState CreateEmpty(string agentName, ManagedRunspaceSettings settings, Func<DateTimeOffset> timeProvider = null)
        => new EmptyState(agentName, settings, timeProvider);

        public abstract IRunspaceAgentStateOperations RenewRunspace();
        public virtual IRunspaceAgentStateOperations CloseRunspace()
        {
            if (Settings.ClosingScript != null)
                Runspace?.Invoke(Settings.ClosingScript);

            // !!!!!!!!!!             
            Runspace?.Dispose();
            Runspace = null;

            return new EmptyState(AgentName, Settings, TimeProvider);
        }
        public abstract IRunspaceAgentStateOperations CreateAndInitRunspace();
        public abstract IRunspaceAgentStateOperations ProcessInvocation(InvocationContext invocation);

        protected RunspaceProxy CreateAndInitProxy()
        {
            var newRunspace = RunspaceProxy.Create(AgentName, TimeProvider(), Settings.RunspaceFactory);
            if (Settings.InitScript != null)
                newRunspace.Invoke(Settings.InitScript);

            return newRunspace;
        }
    }
}

