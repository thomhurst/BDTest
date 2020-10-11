using BDTest.Test;

namespace BDTest.Interfaces.Internal
{
    public interface ITypeMatcher
    {
        bool IsSuperClassOfAbstractContextBDTestBase(BDTestBase bdTestBase);
    }
}