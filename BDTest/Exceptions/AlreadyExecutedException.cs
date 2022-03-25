namespace BDTest.Exceptions;

public class AlreadyExecutedException : BDTestException
{
    public override string Message { get; }

    public AlreadyExecutedException(string message)
    {
        Message = message;
    }
}