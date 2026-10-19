using BDTest;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

public class RetryAttributeTests
{
    public class RetryOnScenarioTests : RunTestBase
    {
        [Before(HookType.Test)]
        public void BindMetadata() => BeginTest(Required(TestContext.Current).Id, Required(TestContext.Current).Metadata.TestDetails.MethodMetadata.GetReflectionInfo());
        [After(HookType.Test)]
        public void FinishExecution() => EndTest();
        private int _counter;

        [Before(HookType.Test), BDTestRetrySetUp]
        public void Setup()
        {
            _counter++;
        }

        private int _testRetryCount = 0;
        [Test, ScenarioText("RetryOnScenarioTests"), BDTestRetry(3)]
        public async Task Test()
        {
            await Given(() => Console.WriteLine("I run a test with a retry attribute"))
                .And(() => IncrementCount(ref _testRetryCount))
                .When(() => ThrowIfLessThan(_testRetryCount))
                .Then(() => AssertCounters())
                .And(() => Console.WriteLine($"Counter: {_counter} | TestRetryCount: {_testRetryCount}"))
                .BDTestAsync();

            var scenarios = BDTestUtil.GetScenarios(TestRun).ToList();
            await Assert.That(scenarios.Count).IsEqualTo(1);
            await Assert.That(scenarios[0].Status).IsEqualTo(Status.Passed);
        }

        private void ThrowIfLessThan(int i)
        {
            if (i < 3)
            {
                throw new Exception();
            }
        }

        private async Task AssertCounters()
        {
            await Assert.That(_counter).IsEqualTo(_testRetryCount);
            await Assert.That(_counter).IsGreaterThan(0);
            await Assert.That(_testRetryCount).IsGreaterThan(0);
        }

        private void IncrementCount(ref int i)
        {
            i++;
        }
    }
        
    [BDTestRetry(3)]
    public class RetryOnStoryTests : RunTestBase
    {
        [Before(HookType.Test)]
        public void BindMetadata() => BeginTest(Required(TestContext.Current).Id, Required(TestContext.Current).Metadata.TestDetails.MethodMetadata.GetReflectionInfo());
        [After(HookType.Test)]
        public void FinishExecution() => EndTest();
        private int _counter;

        [Before(HookType.Test), BDTestRetrySetUp]
        public void Setup()
        {
            _counter++;
        }

        private int _testRetryCount = 0;
        [Test, ScenarioText("RetryOnStoryTests")]
        public async Task Test()
        {
            await Given(() => Console.WriteLine("I run a test with a retry attribute"))
                .And(() => IncrementCount(ref _testRetryCount))
                .When(() => ThrowIfLessThan(_testRetryCount))
                .Then(() => AssertCounters())
                .And(() => Console.WriteLine($"Counter: {_counter} | TestRetryCount: {_testRetryCount}"))
                .BDTestAsync();

            var scenarios = BDTestUtil.GetScenarios(TestRun).ToList();
            await Assert.That(scenarios.Count).IsEqualTo(1);
            await Assert.That(scenarios[0].Status).IsEqualTo(Status.Passed);
        }

        private void ThrowIfLessThan(int i)
        {
            if (i < 3)
            {
                throw new Exception();
            }
        }

        private async Task AssertCounters()
        {
            await Assert.That(_counter).IsEqualTo(_testRetryCount);
            await Assert.That(_counter).IsGreaterThan(0);
            await Assert.That(_testRetryCount).IsGreaterThan(0);
        }

        private void IncrementCount(ref int i)
        {
            i++;
        }
    }
}
