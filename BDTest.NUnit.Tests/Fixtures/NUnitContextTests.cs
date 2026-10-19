using System.Collections.Concurrent;
using BDTest;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that test contexts are different for each test",
    SoThat = "tests don't share state")]
[TestFixture]
public class UniqueContextTests
{
    [Parallelizable(ParallelScope.Fixtures)]
    public class NUnitContextTests : NUnitBDTestBase<MyTestContext>
    {
        private MyTestContext? _context1;
        private MyTestContext? _context2;

        [Test]
        public void TestWithContext1()
        {
            _context1 = Context;
        }

        [Test]
        public void TestWithContext2()
        {
            _context2 = Context;
        }

        [OneTimeTearDown]
        public void EnsureContextsAreUnique()
        {
            // Filtered runs may omit either context capture test.
            if (_context1 is null || _context2 is null) return;
            Assert.That(_context1.Id, Is.Not.EqualTo(_context2.Id));
        }

        [Test]
        public void GetStoryTextFromBaseClassObjectInContext()
        {
            Assert.That(BDTestContext.TestBase.GetStoryText(),
                Is.EqualTo(
                    $"As a BDTest developer{Environment.NewLine}I want to make sure that test contexts are different for each test{Environment.NewLine}So that tests don't share state{Environment.NewLine}"));
            Assert.That(BDTestContext.GetStoryText(),
                Is.EqualTo(
                    $"As a BDTest developer{Environment.NewLine}I want to make sure that test contexts are different for each test{Environment.NewLine}So that tests don't share state{Environment.NewLine}"));
        }

        [Test]
        [ScenarioText("I can get scenario text from the BDTestContext")]
        public void GetScenarioTextFromBaseClassObjectInContext()
        {
            Assert.That(BDTestContext.TestBase.GetScenarioText(),
                Is.EqualTo("I can get scenario text from the BDTestContext"));
            Assert.That(BDTestContext.GetScenarioText(),
                Is.EqualTo("I can get scenario text from the BDTestContext"));
        }
    }

    [Parallelizable(ParallelScope.Fixtures)]
    public class AsyncNUnitContextTests : NUnitBDTestBase<MyTestContext>
    {
        private MyTestContext? _context1;
        private MyTestContext? _context2;

        [Test]
        public async Task TestWithContext1()
        {
            await Task.Run(() => _context1 = Context);
        }

        [Test]
        public async Task TestWithContext2()
        {
            await Task.Run(() => _context2 = Context);
        }

        [OneTimeTearDown]
        public void EnsureContextsAreUnique()
        {
            // Filtered runs may omit either context capture test.
            if (_context1 is null || _context2 is null) return;
            Assert.That(_context1.Id, Is.Not.EqualTo(_context2.Id));
        }
    }

}
