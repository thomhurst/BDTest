using System.Collections.Concurrent;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ExplicitStepConstructionTests : AbstractContextBDTestBase<MyTestContext>
{
    public InjectableStep1 InjectableStep1 => new(Context);
    public InjectableStep2 InjectableStep2 => new(Context);
    public InjectableStep3 InjectableStep3 => new();

    private readonly ConcurrentBag<string> _usedIds = new();

    [Test]
    public async Task Test()
    {
        var scenario = await Given(() => InjectableStep1.Print())
            .When(() => InjectableStep2.Print())
            .Then(() => InjectableStep3.Print())
            .BDTestAsync();
        
        Assert.That(scenario.Steps[0].Output, Does.StartWith("1:"));
        Assert.That(scenario.Steps[1].Output, Does.StartWith("2:"));
        Assert.That(scenario.Steps[2].Output, Does.StartWith("3."));
        
        Assert.That(_usedIds, Does.Not.Contain(scenario.Steps[0].Output));
        _usedIds.Add(scenario.Steps[0].Output);
    }
    
    [Test]
    public async Task Test2()
    {
        var scenario = await Given(() => InjectableStep1.Print())
            .When(() => InjectableStep2.Print())
            .Then(() => InjectableStep3.Print())
            .BDTestAsync();
        
        Assert.That(scenario.Steps[0].Output, Does.StartWith("1:"));
        Assert.That(scenario.Steps[1].Output, Does.StartWith("2:"));
        Assert.That(scenario.Steps[2].Output, Does.StartWith("3."));
        
        Assert.That(_usedIds, Does.Not.Contain(scenario.Steps[0].Output));
        _usedIds.Add(scenario.Steps[0].Output);
    }
    
    [Test]
    public async Task Test3()
    {
        var scenario = await Given(() => InjectableStep1.Print())
            .When(() => InjectableStep2.Print())
            .Then(() => InjectableStep3.Print())
            .BDTestAsync();
        
        Assert.That(scenario.Steps[0].Output, Does.StartWith("1:"));
        Assert.That(scenario.Steps[1].Output, Does.StartWith("2:"));
        Assert.That(scenario.Steps[2].Output, Does.StartWith("3."));
        
        Assert.That(_usedIds, Does.Not.Contain(scenario.Steps[0].Output));
        _usedIds.Add(scenario.Steps[0].Output);
    }
}

public class InjectableStep1
{
    private readonly MyTestContext _myTestContext;

    public InjectableStep1(MyTestContext myTestContext)
    {
        _myTestContext = myTestContext;
    }

    public void Print() => Console.WriteLine($"1: {_myTestContext.Id}");
}

public class InjectableStep2
{
    private readonly MyTestContext _myTestContext;

    public InjectableStep2(MyTestContext myTestContext)
    {
        _myTestContext = myTestContext;
    }

    public void Print() => Console.WriteLine($"2: {_myTestContext.Id}");
}

public class InjectableStep3
{
    public void Print() => Console.WriteLine($"3.");
}