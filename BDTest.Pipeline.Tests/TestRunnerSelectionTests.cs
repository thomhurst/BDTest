using BDTest.Pipeline.Modules;
using NUnit.Framework;

namespace BDTest.Pipeline.Tests;

public class TestRunnerSelectionTests
{
    [TestCase("BDTest.TUnit.Tests.csproj", true)]
    [TestCase("bdtest.tunit.tests.CSPROJ", true)]
    [TestCase("Other.TUnit.Tests.csproj", false)]
    [TestCase("BDTest.TUnit.Tests.Extra.csproj", false)]
    [TestCase("Prefix.BDTest.TUnit.Tests.csproj", false)]
    [TestCase("BDTest.Tests.csproj", false)]
    public void Selects_TUnit_only_for_the_intended_project_filename(string filename, bool expected)
    {
        Assert.That(RunUnitTestsModule.UsesTUnitRunner(filename), Is.EqualTo(expected));
    }

    [TestCase("BDTest.TUnit.Tests.csproj", true)]
    [TestCase("BDTest.Tests.csproj", false)]
    [TestCase("Other.TUnit.Tests.csproj", false)]
    public void Parent_directory_names_do_not_change_the_runner(string filename, bool expected)
    {
        var projectPath = Path.Combine("checkout-with-TUnit-in-name", "TUnit", filename);

        Assert.That(RunUnitTestsModule.UsesTUnitRunner(projectPath), Is.EqualTo(expected));
    }
}
