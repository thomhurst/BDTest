using System.Reflection;
using System.Runtime.CompilerServices;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class CoreAssemblyTests
{
    [Test]
    public void Internals_are_visible_only_to_required_tests()
    {
        var friends = typeof(BDTestBase).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => new AssemblyName(attribute.AssemblyName).Name);

        Assert.That(friends, Is.EquivalentTo(new[]
        {
            "BDTest.Tests"
        }));
    }
}
