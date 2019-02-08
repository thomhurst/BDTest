using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
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
                Action.Compile().Invoke();
            }
        }

    }
}
