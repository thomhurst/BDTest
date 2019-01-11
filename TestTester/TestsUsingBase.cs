using System;
using System.Linq.Expressions;
using NUnit.Framework;
using BDTest.Attributes;
using BDTest.Test;

namespace TestTester
{
    [Story(
        AsA = "Test User",
        IWant = "To Test Using the Base Class",
        SoThat = "Things Work")]
    public class TestsUsingBase : BDTestBase
    {
        [Test]
        [ScenarioText("Custom Scenario")]
        public void Test1()
        {
            Given(() => Action1())
                .When(() => Action2())
                .Then(() => Action3())
                .And(() => Action4())
                .BDTest();
        }

        private void Action4()
        {
            
        }

        public void Action1()
        {

        }

        [StepText("I perform my second action")]
        public void Action2()
        {
            
        }

        public void Action3()
        {
            Console.WriteLine(3);
        }

        public void Add(TestContext testContext)
        {
            testContext.Number++;
        }

        public void NumberShouldBe(TestContext testContext, int number)
        {
            Assert.AreEqual(number, testContext.Number);
        }

        public void Exception(TestContext testContext)
        {
            throw new Exception("");
        }

        public void AssertException(Expression<Action> code)
        {
            Assert.Throws<Exception>(code.Compile().Invoke);
        }


        private void NotImplemented(TestContext context)
        {
            throw new NotImplementedException();
        }

        [Test]
        public void Test4()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context))
                .When(() => Add(context))
                .Then(() => Add(context))
                .And(() => NumberShouldBe(context, 3))
                .BDTest());
        }

        [Test]
        public void Test5()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => NumberShouldBe(context, 3))
                    .BDTest());
        }

        [Test]
        public void ExceptionTest()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => Exception(context))
                    .BDTest());
        }


        [Test]
        public void NotImplementedTest()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => NotImplemented(context))
                    .BDTest());
        }

        [Test]
        [Ignore("")]
        public void IgnoreTest()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => Exception(context))
                    .BDTest());
        }
    }
}
