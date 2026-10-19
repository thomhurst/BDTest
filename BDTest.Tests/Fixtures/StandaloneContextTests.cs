using System.Collections.Concurrent;
using BDTest.Attributes;
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
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class AsyncNonNUnitContextTests : RunContextTestBase<MyTestContext>
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

        [TearDown]
        public void CompleteExecution()
        {
            BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
        }
    }

    [Parallelizable(ParallelScope.Fixtures)]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class NonNUnitContextTests : RunContextTestBase<MyTestContext>
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

        [TearDown]
        public void CompleteExecution()
        {
            BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
        }
    }

    [Parallelizable(ParallelScope.Children)]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class NonNUnitParallelContextTests : RunContextTestBase<MyTestContext>
    {
        private readonly ConcurrentBag<MyTestContext> _contexts = new();
        private readonly TaskCompletionSource<bool> _completionSource = new();
        private readonly object _lock = new();

        [Test]
        public void Test1()
        {
            AddContext();
        }

        [Test]
        public void Test2()
        {
            AddContext();
        }

        [Test]
        public void Test3()
        {
            AddContext();
        }

        [Test]
        public void Test4()
        {
            AddContext();
        }

        [Test]
        public void Test5()
        {
            AddContext();
        }

        [Test]
        public void Test6()
        {
            AddContext();
        }

        [Test]
        public void Test7()
        {
            AddContext();
        }

        [Test]
        public void Test8()
        {
            AddContext();
        }

        public void AddContext()
        {
            _contexts.Add(Context);

            if (_contexts.Count == 8)
            {
                _completionSource.TrySetResult(true);
            }
        }

        [Test]
        public void ZZZ_EnsureContextsAreUnique()
        {
            Assert.That(_completionSource.Task.Wait(TimeSpan.FromSeconds(30)), Is.True, "Timed out waiting for other tests to complete");
                
            var distinctContexts = _contexts.Select(x => x.Id).Distinct().ToList();

            var distinctContextCount = distinctContexts.Count;
            Assert.That(distinctContextCount, Is.EqualTo(8));
        }

        [TearDown]
        public void CompleteExecution()
        {
            BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
        }
    }
}
