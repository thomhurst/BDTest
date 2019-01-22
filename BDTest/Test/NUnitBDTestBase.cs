using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnitTestContext = NUnit.Framework.TestContext;
using MSTestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace BDTest.Test
{
    public class NUnitBDTestBase<TContext> : BDTestBase where TContext : new()
    {
        protected NUnitBDTestBase()
        {
            var nunitPackage = AppDomain.CurrentDomain
                .GetAssemblies().FirstOrDefault(it => it.FullName.ToLower().StartsWith("nunit.framework"));
            if (nunitPackage == null)
            {
                throw new Exception("You do not have NUnit installed in order to extend from NUnitBDTestBase!");
            }
        }

        private readonly IDictionary<string, TContext> _contexts = new ConcurrentDictionary<string, TContext>();


        public TContext Context
        {
            get
            {
                var testContextId = NUnitTestContext.CurrentContext.Test.ID;
                _contexts.TryGetValue(testContextId, out var context);

                if (context != null)
                {
                    return context;
                }

                context = Activator.CreateInstance<TContext>();
                _contexts[testContextId] = context;
                return context;
            }
        }
    }
}
