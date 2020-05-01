using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;
using TestTester.CustomAttributes;

namespace TestTester
{
    [Story(
        AsA = "Test User",
        IWant = "To Test Using the Base Class",
        SoThat = "Things Work")]
    public class TestsUsingBase : BDTestBase
    {
        [SetUp]
        public void Setup()
        {
            WriteStartupOutput("Blah-Startup");
            WriteStartupOutput("Blah-Startup");
            WriteStartupOutput("Blah-Startup");
        }
        
        [TearDown]
        public void Teardown()
        {
            WriteTearDownOutput("Blah-Teardown");
            WriteTearDownOutput("Blah-Teardown");
            WriteTearDownOutput("Blah-Teardown");
        }
        
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

        [ScenarioText("Asynchronous Scenario With Custom Awaiter")]
        [Test]
        public async Task AsyncTestCustomAwaiter()
        {
            await Given(() => Action1())
                .When(() => Action2())
                .Then(() => Action3())
                .And(() => Action4());
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
        [ScenarioText("Synchronous Scenario Failure")]
        public async Task SyncTestFailure()
        {
            Given(() => Action1())
                .When(() => Action2())
                .Then(() => Action3())
                .And(() => Action4())
                .And(() => Exception(new TestContext()))
                .BDTest();
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
        
        [Test]
        [CustomTestInformation("Some information!")]
        [ScenarioText("Test Information Attribute Test")]
        public void TestInformationAttribute()
        {
            Given(() => Action1())
                .When(() => Action2())
                .Then(() => Action3())
                .And(() => Action4())
                .BDTest();
        }
        
        [Test]
        [CustomTestInformation("Some information!")]
        [CustomTestInformation("Some information again!")]
        [CustomTestInformationAttribute2("Some information 2!")]
        [ScenarioText("Test Information Attribute Test")]
        public void TestInformationAttribute2()
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
        
        [Test]
        public void FuncStepTextTest()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => FuncReturnsStepTextCorrectly(() => "Blah"))
                    .BDTest());
        }

        [StepText("Text is: {0}")]
        public void FuncReturnsStepTextCorrectly(Func<string> func)
        {
            
        }
        
        [StepText("Text with a function returning {0} which is overridden", "0:Overridden Successfully")]
        public void FuncReturnsStepTextOverriddenCorrectly(Func<string, string> func)
        {
            
        }
        
        [StepText("the step text can display the list {0}")]
        public void StepWithListType(IList<string> list)
        {
            
        }
        
        [Test]
        public void FuncStepTextReverserTest()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context))
                    .When(() => Add(context))
                    .Then(() => FuncReturnsStepTextOverriddenCorrectly(str => str.Reverse().ToString()))
                    .BDTest());
        }
        
        [Test]
        public void WithStepText()
        {
            WithContext<TestContext>(context =>
                Given(() => Add(context)).WithStepText(() => $"this is my Given step {context.Number}")
                    .And(() => Add(context)).WithStepText(() => $"this is my AndGiven step {context.Number}")
                    .When(() => Add(context)).WithStepText(() => $"this is my When step {context.Number}")
                    .Then(() => Add(context)).WithStepText(() => $"this is my Then step {context.Number}")
                    .And(() => Add(context)).WithStepText(() => $"this is my AndThen step {context.Number}")
                    .BDTest());
        }

        [Test]
        public void WithStepTextList()
        {
            Given(() => Console.WriteLine("Empty Step"))
                .When(() => Console.WriteLine("Empty Step"))
                .Then(() => StepWithListType(new List<string> {"Blah1", "Blah2", "Blah3"}))
                .BDTest();
        }
        
        [Test]
        public void SkipGivenStep()
        {
            When(() => Console.WriteLine("Empty Step"))
                .Then(() => Console.WriteLine("Empty Step"))
                .BDTest();
        }
        
        [Test]
        public void ReportFolderNameProperty()
        {
            BDTestSettings.ReportFolderName = "BDTestReports";
            
            Given(() => Console.WriteLine("Empty Step"))
                .When(() => Console.WriteLine("Empty Step"))
                .Then(() => Console.WriteLine("Empty Step"))
                .BDTest();

            BDTestSettings.ReportFolderName = null;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            BDTestReportGenerator.Generate();
        }
    }
}
