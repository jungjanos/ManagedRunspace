using ManagedRunspace2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedRunspace2Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public async Task Sandbox_single_agent_with_defaults()
        {
            var settings = ManagedRunspaceSettings.Defaults;
            var isf = RunspaceAgentHelper.CreateInitialStateFactory("agent1", settings);
            var queue = new BlockingCollection<InvocationContext>();

            var agent = Agent<InvocationContext, RunspaceAgentState>.Start(queue, isf, RunspaceAgentHelper.DefaultProcessor);

            var invocation1 = new InvocationContext("gci", false, new TaskCompletionSource<PsResult>(), default);
            var invocation2 = new InvocationContext("Get-Host", true, new TaskCompletionSource<PsResult>(), default);
            var invocation3 = new InvocationContext("Get-Process", false, new TaskCompletionSource<PsResult>(), default);

            queue.Add(invocation1);
            queue.Add(invocation2);
            queue.Add(invocation3);


            var results = await Task.WhenAll(
                invocation1.TaskCompletionSource.Task,
                invocation2.TaskCompletionSource.Task,
                invocation3.TaskCompletionSource.Task
                );

            //queue.CompleteAdding();
        }

        [TestMethod]
        public async Task Sandbox_invoking_hundred()
        {
            var settings = ManagedRunspaceSettings.Defaults;
            var isf = RunspaceAgentHelper.CreateInitialStateFactory("agent1", settings);
            var queue = new BlockingCollection<InvocationContext>();

            var agent = Agent<InvocationContext, RunspaceAgentState>.Start(queue, isf, RunspaceAgentHelper.DefaultProcessor);

            var invocations = Enumerable.Range(1, 100).Select(
                //_ => new InvocationContext("gci", false, new TaskCompletionSource<PsResult>(), default)
                _ => new InvocationContext("Get-Process | select Name,Id", false, new TaskCompletionSource<PsResult>(), default)
                ).ToArray();
            
            foreach(var item in invocations)
                queue.Add(item);

            var results = await Task.WhenAll(invocations.Select(i => i.TaskCompletionSource.Task));            
        }


        [TestMethod]
        public async Task Sandbox_multiple_agents_with_defaults()
        {
            var settings = ManagedRunspaceSettings.Defaults;
            var isf1 = RunspaceAgentHelper.CreateInitialStateFactory("agent1", settings);
            var isf2 = RunspaceAgentHelper.CreateInitialStateFactory("agent2", settings);
            var isf3 = RunspaceAgentHelper.CreateInitialStateFactory("agent3", settings);
            var isf4 = RunspaceAgentHelper.CreateInitialStateFactory("agent4", settings);
            var queue = new BlockingCollection<InvocationContext>();

            var agent1 = Agent<InvocationContext, RunspaceAgentState>.Start(queue, isf1, RunspaceAgentHelper.DefaultProcessor);
            var agent2 = Agent<InvocationContext, RunspaceAgentState>.Start(queue, isf2, RunspaceAgentHelper.DefaultProcessor);
            var agent3 = Agent<InvocationContext, RunspaceAgentState>.Start(queue, isf3, RunspaceAgentHelper.DefaultProcessor);
            var agent4 = Agent<InvocationContext, RunspaceAgentState>.Start(queue, isf4, RunspaceAgentHelper.DefaultProcessor);

            var invocation1 = new InvocationContext("gci", false, new TaskCompletionSource<PsResult>(), default);
            var invocation2 = new InvocationContext("Get-Host", true, new TaskCompletionSource<PsResult>(), default);
            var invocation3 = new InvocationContext("Get-Process | select Name,Id", false, new TaskCompletionSource<PsResult>(), default);

            queue.Add(invocation1);
            queue.Add(invocation2);
            queue.Add(invocation3);


            var results = await Task.WhenAll(
                invocation1.TaskCompletionSource.Task,
                invocation2.TaskCompletionSource.Task,
                invocation3.TaskCompletionSource.Task
                );

            queue.CompleteAdding();
        }
    }
}
