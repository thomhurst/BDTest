using BDTest.Attributes;
using BDTest.NUnit;
using NUnit.Framework;

namespace TestTester
{
    [Story(
        AsA = "Test User",
        IWant = "To Test Using the Base Class",
        SoThat = "Things Work")]
    [Parallelizable(ParallelScope.All)]
    public class TestsUsingNUnitBaseWithContext : NUnitBDTestBase<TestContext>
    {
        private InheritedAbstractStep Steps => GetStep<InheritedAbstractStep>();
        
        [Test]
        public void Test1()
        {
            Given(() => Add(Context))
                .When(() => Add(Context))
                .Then(() => NumberShouldBe(Context, 2))
                .BDTest();
        }

        [Test]
        public void Test2()
        {
            Given(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .When(() => Add(Context))
                .Then(() => NumberShouldBe(Context, 5))
                .BDTest();
        }

        [Test]
        public void Test3()
        {
            Given(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .When(() => Add(Context))
                .Then(() => NumberShouldBe(Context, 10))
                .BDTest();
        }

        [Test]
        public void Test4()
        {
            Given(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .And(() => Add(Context))
                .When(() => Add(Context))
                .Then(() => NumberShouldBe(Context, 20))
                .BDTest();
        }

        public void Add(TestContext testContext)
        {
            testContext.Number++;
        }

        public void NumberShouldBe(TestContext testContext, int number)
        {
            Assert.AreEqual(number, testContext.Number);
        }
    }
}
