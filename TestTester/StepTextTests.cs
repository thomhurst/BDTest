using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Test;
using NUnit.Framework;

namespace TestTester
{
    public class StepTextTests : BDTestBase
    {
        [Test]
        public async Task ConstantArguments()
        {
            await Given(() => Action("blah1"))
                .When(() => Action("blah2"))
                .Then(() => Action("blah3"))
                .And(() => Action("blah4"))
                .BDTestAsync();
        }
        
        [TestCase("Blah")]
        public async Task DynamicArguments(string arg)
        {
            await Given(() => Action(arg))
                .When(() => Action(arg))
                .Then(() => Action(arg))
                .And(() => Action(arg))
                .BDTestAsync();
        }
        
        [StepText("Steptext is {0}")]
        public async Task Action(string blah) {
            // Blah
        }
    }
}