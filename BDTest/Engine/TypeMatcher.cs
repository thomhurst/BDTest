using BDTest.Interfaces.Internal;
using BDTest.Test;

namespace BDTest.Engine
{
    public class TypeMatcher : ITypeMatcher
    {
        public bool IsSuperClassOfAbstractContextBDTestBase(BDTestBase bdTestBase)
        {
            var type = bdTestBase.GetType();
            do
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AbstractContextBDTestBase<>))
                {
                    return true;
                }

                type = type.BaseType;
            } while (type != null);

            return false;
        }
    }
}