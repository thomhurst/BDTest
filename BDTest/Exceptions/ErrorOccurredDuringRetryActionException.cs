using System;

namespace BDTest.Exceptions;

public class ErrorOccurredDuringRetryActionException : Exception
{
    private Exception Exception { get; }
        
    public override Exception GetBaseException()
    {
        return Exception;
    }

    public ErrorOccurredDuringRetryActionException(Exception exception)
    {
        Exception = exception;
    }
}