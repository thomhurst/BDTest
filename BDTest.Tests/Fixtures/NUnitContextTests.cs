using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.NUnit;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that test contexts are different for each test",
        SoThat = "tests don't share state")]
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
}