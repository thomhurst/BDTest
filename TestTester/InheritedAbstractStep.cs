using System;
using BDTest.UserModels;

namespace TestTester
{
    public class InheritedAbstractStep : AbstractStep<TestContext>
    {
        public InheritedAbstractStep()
        {
            Console.WriteLine(Context.Number);
        }
    }
}