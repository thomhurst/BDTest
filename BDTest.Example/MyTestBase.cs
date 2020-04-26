using BDTest.Example.Steps;
using BDTest.Example.Steps.Assertions;
using BDTest.NUnit;

namespace BDTest.Example
{
    public abstract class MyTestBase : NUnitBDTestBase<MyTestContext>
    {
        public MyStepClass Steps => new MyStepClass(Context);
        public MyAssertionStepClass Assertions => new MyAssertionStepClass(Context);
    }
}