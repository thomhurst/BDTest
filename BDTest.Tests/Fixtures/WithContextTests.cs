using System;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    public class WithContextTests : BDTestBase
    {
        private MyTestContext _context1;
        private MyTestContext _context2;

        [Test, Order(1)]
        public void TestWithContext1()
        {
            WithContext<MyTestContext>(context =>
            {
                _context1 = context;
                return When(() => Console.WriteLine())
                    .Then(() => Console.WriteLine())
                    .BDTest();
            });
        }
        
        [Test, Order(2)]
        public void TestWithContext2()
        {
            WithContext<MyTestContext>(context =>
            {
                _context2 = context;
                return When(() => Console.WriteLine())
                    .Then(() => Console.WriteLine())
                    .BDTest();
            });
        }
        
        [Test, Order(3)]
        public void EnsureContextsAreUnique()
        {
            Assert.That(_context1.Id, Is.Not.EqualTo(_context2.Id));
        }
    }
}