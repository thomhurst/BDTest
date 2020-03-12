using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace BDTest
{
    internal class Runnable
    {

        public Expression<Func<Task>> Task { get; }
        public Expression<Action> Action { get; }

        public Runnable(Expression<Func<Task>> task)
        {
            Task = task;
        }

        public Runnable(Expression<Action> action)
        {
            Action = action;
        }

        internal async Task Run()
        {
            if (Task != null)
            {
                await Task.Compile().Invoke();
            }
            else
            {
                var compiledAction = Action.Compile();
                if (IsThisAsync(compiledAction))
                {
                    await System.Threading.Tasks.Task.Run(compiledAction);
                }
                else
                {
                    compiledAction.Invoke();
                }
            }
        }

        private static bool IsThisAsync(Action action)
        {
            return action.Method.IsDefined(typeof(AsyncStateMachineAttribute),
                false);
        }
    }
}
