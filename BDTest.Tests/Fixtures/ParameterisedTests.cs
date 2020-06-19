using System;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    public class ParameterisedTests : BDTestBase
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Test(int integer)
        {
            When(() => Console.WriteLine(integer))
                .Then(() => Console.WriteLine(integer))
                .BDTest();
        }
    }
}