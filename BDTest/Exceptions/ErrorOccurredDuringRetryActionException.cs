namespace BDTest.Exceptions;

public class ErrorOccurredDuringRetryActionException : BDTestException
{
    public ErrorOccurredDuringRetryActionException(Exception exception)
        : base($"An error occurred while running a retry action: {exception?.Message}", exception)
    {
    }
}
