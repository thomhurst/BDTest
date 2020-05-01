using System;
using BDTest.NUnit;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    public class CustomPassExceptionsTests : NUnitBDTestBase<MyTestContext>
    {
        [Test]
        public void NUNitSuccessException()
        {
            var scenario = Given(() => Console.WriteLine("NUnit throws a success exception"))
                .When(() => Assert.Pass())
                .Then(() => Console.WriteLine("the test has passed"))
                .BDTest();
            
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        }

        [Test]
        public void CustomException()
        {
            BDTestSettings.SuccessExceptionTypes.Add(typeof(MyAllowedException));
            
            var scenario = Given(() => Console.WriteLine("NUnit throws a success exception"))
                .When(() => ThrowException())
                .Then(() => Console.WriteLine("the test has passed"))
                .BDTest();
            
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        }

        private void ThrowException()
        {
            throw new MyAllowedException();
        }
    }

    public class MyAllowedException : Exception
    {
        
    }
}