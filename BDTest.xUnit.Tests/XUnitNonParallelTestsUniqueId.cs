using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace BDTest.xUnit.Tests
{
    [CollectionDefinition("Non-Parallel Tests", DisableParallelization = true)]
    public class XUnitNonParallelTests : XUnitBDTestBase<XUnitContext>
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private static readonly List<string> Ids = new();

        public XUnitNonParallelTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Test1()
        {
            var id = BDTestContext.CurrentScenarioBDTestExecutionId;
            _testOutputHelper.WriteLine($"XUnit Test ID is: {id}");
            Assert.DoesNotContain(id, Ids);
            Ids.Add(id);
        }

        [Fact]
        public void Test2()
        {
            var id = BDTestContext.CurrentScenarioBDTestExecutionId;
            _testOutputHelper.WriteLine($"XUnit Test ID is: {id}");
            Assert.DoesNotContain(id, Ids);
            Ids.Add(id);
        }

        [Fact]
        public void Test3()
        {
            var id = BDTestContext.CurrentScenarioBDTestExecutionId;
            _testOutputHelper.WriteLine($"XUnit Test ID is: {id}");
            Assert.DoesNotContain(id, Ids);
            Ids.Add(id);
        }

        [Fact]
        public void Test4()
        {
            var id = BDTestContext.CurrentScenarioBDTestExecutionId;
            _testOutputHelper.WriteLine($"XUnit Test ID is: {id}");
            Assert.DoesNotContain(id, Ids);
            Ids.Add(id);
        }

        [Fact]
        public void Test5()
        {
            var id = BDTestContext.CurrentScenarioBDTestExecutionId;
            _testOutputHelper.WriteLine($"XUnit Test ID is: {id}");
            Assert.DoesNotContain(id, Ids);
            Ids.Add(id);
        }
    }
}