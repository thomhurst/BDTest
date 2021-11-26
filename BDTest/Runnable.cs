using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BDTest.Reporters;

namespace BDTest
{
    internal class Runnable
    {
        internal Expression<Func<Task>> Task { get; }
        internal Expression<Action> Action { get; }

        internal Runnable(Expression<Func<Task>> task)
        {
            Task = task;
        }

        public Runnable(Expression<Action> action)
        {
            Action = action;
        }

        internal async Task Run()
        {
            if (Task == null && Action == null)
            {
                ConsoleReporter.WriteLine($"{nameof(Task)} and {nameof(Action)} are both null in {nameof(Runnable)}");
                return;
            }
            
            if (Task != null)
            {
                await Task.Compile().Invoke();
                return;
            }

            var compiledAction = Action.Compile();
            if (IsThisAsync(compiledAction))
            {
                await System.Threading.Tasks.Task.Run(compiledAction);
                return;
            }

            compiledAction.Invoke();
        }

        private static bool IsThisAsync(Action action)
        {
            if (action.Method.ReturnType.GetMethod(nameof(System.Threading.Tasks.Task.GetAwaiter)) != null)
            {
                return true;
            }
            
            return action.Method.IsDefined(typeof(AsyncStateMachineAttribute),
                false);
        }
    }
}
