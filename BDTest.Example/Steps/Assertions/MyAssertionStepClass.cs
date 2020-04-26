using System.Net;
using BDTest.Attributes;
using NUnit.Framework;

namespace BDTest.Example.Steps.Assertions
{
    public class MyAssertionStepClass
    {
        private readonly MyTestContext _context;

        public MyAssertionStepClass(MyTestContext context)
        {
            _context = context;
        }

        [StepText("the HTTP status code is {0}")]
        public void TheHttpStatusCodeIs(HttpStatusCode httpStatusCode)
        {
            Assert.That(_context.ApiContext.ResponseMessage.StatusCode, Is.EqualTo(httpStatusCode));
        }
    }
}