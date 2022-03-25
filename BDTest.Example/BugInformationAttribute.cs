using BDTest.Attributes;

namespace BDTest.Example;

public class BugInformationAttribute : TestInformationAttribute
{
    public BugInformationAttribute(string bugNumber) : base($"Bug #{bugNumber} - http://www.mybugtrackingsite.com/bugs/{bugNumber}")
    {
    }
}