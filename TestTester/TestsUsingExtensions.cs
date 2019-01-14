using System;
using System.Linq.Expressions;
using BDTest.Attributes;
using NUnit.Framework;

namespace TestTester
{
    [Story(
        AsA = "Test User",
        IWant = "To Test using static extensions",
        SoThat = "Things Work")]
    public class TestsUsingExtensions
    {

        [Test]
        [ScenarioText("Custom Scenario")]
        public void Test1()
        {
            this.Given(() => Action1("yo"))
                .When(() => Action2())
                .Then(() => Action3())
                .And(() => Action3())
                .BDTest();
        }

        public void Action1(string str)
        {
            Console.WriteLine(str);
        }

        [StepText("Action 2 is custom text")]
        public void Action2()
        {
            Console.WriteLine(2);
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
            this.WithContext<TestContext>(context =>
                this.Given(() => Add(context))
                .When(() => Add(context))
                .Then(() => Add(context))
                .And(() => NumberShouldBe(context, 3))
                .BDTest());
        }

        [Test]
        public void Test5()
        {
            this.WithContext<TestContext>(context =>
                this.Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => NumberShouldBe(context, 3))
                    .BDTest());
        }

        [Test]
        public void ExceptionTest()
        {
            this.WithContext<TestContext>(context =>
                this.Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => Exception(context))
                    .BDTest());
        }


        [Test]
        public void NotImplementedTest()
        {
            this.WithContext<TestContext>(context =>
                this.Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => NotImplemented(context))
                    .BDTest());
        }

        [Test]
        [Ignore("")]
        public void IgnoreTest()
        {
            this.WithContext<TestContext>((context) =>
                this.Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => Add(context))
                    .And(() => Exception(context))
                    .BDTest());
        }
    }
}
