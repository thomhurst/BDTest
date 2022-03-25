using BDTest.Example.Context;
using BDTest.Example.Steps;
using BDTest.Example.Steps.Assertions;
using BDTest.NUnit;

namespace BDTest.Example;

public abstract class MyTestBase : NUnitBDTestBase<MyTestContext>
{
    // Make sure these steps are properties with getters - Not fields. They should be constructed each time with the relevant context!
    public MyStepClass Steps => new(Context);
    public MyAssertionStepClass Assertions => new(Context);
}