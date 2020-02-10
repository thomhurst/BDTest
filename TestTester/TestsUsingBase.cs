using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Test;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace TestTester
{
    [Story(
        AsA = "Test User",
        IWant = "To Test Using the Base Class",
        SoThat = "Things Work")]
    public class TestsUsingBase : BDTestBase
    {

        [Test]
        [ScenarioText("Asynchronous Scenario")]
        public async Task AsyncTest()
        {
            await Given(() => Action1())
                .When(() => Action2())
                .Then(() => Action3())
                .And(() => Action4())
                .BDTestAsync();
        }

        [Test]
        [ScenarioText("Asynchronous Scenario Failure")]
        public async Task AsyncTestFailure()
        {
            await Given(() => Action1())
                .When(() => Action2())
                .Then(() => Action3())
                .And(() => Action4())
                .And(() => Exception(new TestContext()))
                .BDTestAsync();
        }

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
            Console.WriteLine("Action 4");
        }

        public void Action1()
        {
            Console.WriteLine("Action 1");
        }

        [StepText("I perform my second action")]
        public void Action2()
        {
            Console.WriteLine("Action 2");
        }

        public void Action3()
        {
            Console.WriteLine("Action 3");
        }

        public void Add(TestContext testContext)
        {
            testContext.Number++;
        }

        [StepText("the number should be {1}")]
        public void NumberShouldBe(TestContext testContext, int number)
        {
            Assert.AreEqual(number, testContext.Number);
        }

        public void Exception(TestContext testContext)
        {
            throw new Exception("The test threw an exception");
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
        [NUnit.Framework.Ignore("")]
        public void IgnoreTest()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => Exception(context))
                    .BDTest());
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            BDTestReportGenerator.Generate();
        }
    }
}
