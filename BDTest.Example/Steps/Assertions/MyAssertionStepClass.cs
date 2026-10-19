using System.Net;
using BDTest;
using BDTest.Example.Context;
using NUnit.Framework;

namespace BDTest.Example.Steps.Assertions;

public class MyAssertionStepClass
{
    private readonly MyTestContext _context;

    public MyAssertionStepClass(MyTestContext context)
    {
        _context = context;
    }

    [StepText("the HTTP status code is {httpStatusCode}")]
    public void TheHttpStatusCodeIs(HttpStatusCode httpStatusCode)
    {
        var response = _context.ApiContext.ResponseMessage
            ?? throw new InvalidOperationException("Execute a request before asserting its status");
        Assert.That(response.StatusCode, Is.EqualTo(httpStatusCode));
    }
}