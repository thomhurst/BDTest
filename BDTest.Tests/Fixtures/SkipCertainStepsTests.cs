using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that certain steps can be skipped conditionally",
    SoThat = "tests can be run with different behaviour for different test environments")]
public class SkipCertainStepsTests : RunTestBase
{
    [Before(HookType.Test)]
    public void Setup()
    {
        Options.SkipStepRules.Add<SkipAttribute1>(attr => true);
        Options.SkipStepRules.Add<SkipAttribute2>(attr => true);
        Options.SkipStepRules.Add<SkipAttribute3>(attr => true);
        Options.SkipStepRules.Add<SkipAttributeWithParameter>(attr => attr.ShouldSkip);
    }

    [Test]
    public async Task Skip1Step()
    {
        await Given(() => Console.WriteLine("A test"))
            .When(() => Console.WriteLine())
            .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
            .And(() => ThrowException1())
            .BDTestAsync();
    }
        
    [Test]
    public async Task Skip3Steps()
    {
        await Given(() => Console.WriteLine("A test"))
            .When(() => Console.WriteLine())
            .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
            .And(() => ThrowException1())
            .And(() => ThrowException2())
            .And(() => ThrowException3())
            .BDTestAsync();
    }
        
    [Test]
    public async Task ShouldSkipTrue()
    {
        await Given(() => Console.WriteLine("A test"))
            .When(() => Console.WriteLine())
            .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
            .And(() => ThrowExceptionShouldSkipTrue())
            .BDTestAsync();
    }
        
    [Test]
    public async Task ShouldSkipFalse()
    {
        try
        {
            await Given(() => Console.WriteLine("A test"))
                .When(() => Console.WriteLine())
                .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                .And(() => ThrowExceptionShouldSkipFalse())
                .BDTestAsync();
                
            Fail.Test("The last exception should not be skipped - And should stop us getting here.");
        }
        catch (Exception e) when(e.Message == "ThrowExceptionShouldSkipFalse")
        {
            return;
        }
            
        Fail.Test("");
    }
        
    [Test]
    public async Task NotRegisteredSkipException()
    {
        try
        {
            await Given(() => Console.WriteLine("A test"))
                .When(() => Console.WriteLine())
                .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                .And(() => ThrowException1())
                .And(() => ThrowException2())
                .And(() => ThrowException3())
                .And(() => ThrowNotRegisteredSkipException())
                .BDTestAsync();
                
            Fail.Test("The last exception should not be skipped - And should stop us getting here.");
        }
        catch (Exception e) when(e.Message == "ThrowNotRegisteredSkipException")
        {
            return;
        }
            
        Fail.Test("");
    }

    [NotRegisteredSkip]
    private void ThrowNotRegisteredSkipException()
    {
        throw new Exception("ThrowNotRegisteredSkipException");
    }

    [SkipAttribute1]
    private static Task ThrowException1()
    {
        throw new Exception("ThrowException1");
    }
        
    [SkipAttribute2]
    private static Task ThrowException2()
    {
        throw new Exception("ThrowException2");
    }
        
    [SkipAttribute3]
    private static Task ThrowException3()
    {
        throw new Exception("ThrowException3");
    }
        
    [SkipAttributeWithParameter(true)]
    private static Task ThrowExceptionShouldSkipTrue()
    {
        throw new Exception("ThrowExceptionShouldSkipTrue");
    }
        
    [SkipAttributeWithParameter(false)]
    private static Task ThrowExceptionShouldSkipFalse()
    {
        throw new Exception("ThrowExceptionShouldSkipFalse");
    }
}

class SkipAttribute1 : SkipStepAttribute
{
}
    
class SkipAttribute2 : SkipStepAttribute
{
}
    
class SkipAttribute3 : SkipStepAttribute
{
}
    
class SkipAttributeWithParameter : SkipStepAttribute
{
    public bool ShouldSkip { get; }

    public SkipAttributeWithParameter(bool shouldSkip)
    {
        ShouldSkip = shouldSkip;
    }
}
    
class NotRegisteredSkipAttribute : SkipStepAttribute
{
}
