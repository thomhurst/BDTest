using System;
using System.Collections.Generic;
using NUnit.Framework;
using BDTest.Attributes;
using BDTest.Test;

namespace TestTester
{
    [Story(
        AsA = "context",
        IWant = "to be unique",
        SoThat = "tests can run independently")]
    public class ContextTests : BDTestBase
    {
        private readonly List<TestContext> _contexts = new List<TestContext>();

        public void Action1(string str)
        {
            Console.WriteLine(str);
        }

        [StepText("I do something")]
        public void DummyStep()
        {
            
        }

        [StepText("the context should be new")]
        public void ContextShouldBeNew(TestContext context)
        {
            foreach (var previousContext in _contexts)
            {
                Assert.AreNotEqual(previousContext, context);
                Assert.AreNotEqual(previousContext.GetHashCode(), context.GetHashCode());
                Assert.AreNotEqual(previousContext.Number, context.Number);
            }

            context.Number++;
            _contexts.Add(context);
        }

        [Test]
        public void Test()
        {
            WithContext<TestContext>(context =>
                Given(() => DummyStep())
                .When(() => DummyStep())
                .Then(() => ContextShouldBeNew(context))
                .BDTest());
        }
    }
}
