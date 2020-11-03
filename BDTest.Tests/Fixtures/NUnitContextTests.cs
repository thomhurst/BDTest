using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.NUnit;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that test contexts are different for each test",
        SoThat = "tests don't share state")]
    [TestFixture]
    public class UniqueContextTests
    {
        [Parallelizable(ParallelScope.Fixtures)]
        public class NUnitContextTests : NUnitBDTestBase<MyTestContext>
        {
            private MyTestContext _context1;
            private MyTestContext _context2;

            [Test, Order(1)]
            public void TestWithContext1()
            {
                _context1 = Context;
            }

            [Test, Order(2)]
            public void TestWithContext2()
            {
                _context2 = Context;
            }

            [Test, Order(3)]
            public void EnsureContextsAreUnique()
            {
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
            private MyTestContext _context1;
            private MyTestContext _context2;

            [Test, Order(1)]
            public async Task TestWithContext1()
            {
                await Task.Run(() => _context1 = Context);
            }

            [Test, Order(2)]
            public async Task TestWithContext2()
            {
                await Task.Run(() => _context2 = Context);
            }

            [Test, Order(3)]
            public void EnsureContextsAreUnique()
            {
                Assert.That(_context1.Id, Is.Not.EqualTo(_context2.Id));
            }
        }

        [Parallelizable(ParallelScope.Fixtures)]
        public class AsyncNonNUnitContextTests : AbstractContextBDTestBase<MyTestContext>
        {
            private MyTestContext _context1;
            private MyTestContext _context2;

            [Test, Order(1)]
            public async Task TestWithContext1()
            {
                await Task.Run(() => _context1 = Context);
            }

            [Test, Order(2)]
            public async Task TestWithContext2()
            {
                await Task.Run(() => _context2 = Context);
            }

            [Test, Order(3)]
            public void EnsureContextsAreUnique()
            {
                Assert.That(_context1.Id, Is.Not.EqualTo(_context2.Id));
            }

            [TearDown]
            protected override void MarkTestAsComplete()
            {
                base.MarkTestAsComplete();
            }
        }

        [Parallelizable(ParallelScope.Fixtures)]
        public class NonNUnitContextTests : AbstractContextBDTestBase<MyTestContext>
        {
            private MyTestContext _context1;
            private MyTestContext _context2;

            [Test, Order(1)]
            public void TestWithContext1()
            {
                _context1 = Context;
            }

            [Test, Order(2)]
            public void TestWithContext2()
            {
                _context2 = Context;
            }

            [Test, Order(3)]
            public void EnsureContextsAreUnique()
            {
                Assert.That(_context1.Id, Is.Not.EqualTo(_context2.Id));
            }

            [TearDown]
            protected override void MarkTestAsComplete()
            {
                base.MarkTestAsComplete();
            }
        }

        [Parallelizable(ParallelScope.Children)]
        public class NonNUnitParallelContextTests : AbstractContextBDTestBase<MyTestContext>
        {
            private readonly ConcurrentBag<MyTestContext> _contexts = new ConcurrentBag<MyTestContext>();
            private readonly TaskCompletionSource<bool> _completionSource = new TaskCompletionSource<bool>();
            private readonly object _lock = new object();

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

            [Test, Timeout(30000)]
            public void ZZZ_EnsureContextsAreUnique()
            {
                _completionSource.Task.GetAwaiter().GetResult();
                
                var distinctContexts = _contexts.Select(x => x.Id).Distinct().ToList();

                var distinctContextCount = distinctContexts.Count;
                Assert.That(distinctContextCount, Is.EqualTo(8));
            }

            [TearDown]
            protected override void MarkTestAsComplete()
            {
                base.MarkTestAsComplete();
            }
        }
    }
}