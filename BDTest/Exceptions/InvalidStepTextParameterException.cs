namespace BDTest.Exceptions;

public class InvalidStepTextParameterException : BDTestException
{
    public override string Message { get; }

    public InvalidStepTextParameterException(string message)
    {
        Message = message;
    }
}