using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using BDTest.Test;

namespace TestTester
{
    [TestFixture]
    public class TestClass : BDTestBase
    {

        [Test]
        public void Test1()
        {
            WithContext<StringWriter>(context =>
                Given(() => Something())
                    .When(() => Something())
                    .Then(() => Something())
                    .BDTest()
            );
        }

        public void Something()
        {
            Thread.Sleep(new Random().Next(500));
        }
    }
}
