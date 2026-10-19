using System.Reflection;
using System.Runtime.CompilerServices;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

public class CoreAssemblyTests
{
    [Test]
    public async Task Internals_are_visible_only_to_required_tests()
    {
        var friends = typeof(BDTestBase).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => new AssemblyName(attribute.AssemblyName).Name);


        await Assert.That(friends).IsEquivalentTo((new[]
        {
            "BDTest.Tests"
        }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }
}
