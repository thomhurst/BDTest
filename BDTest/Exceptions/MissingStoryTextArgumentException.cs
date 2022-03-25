namespace BDTest.Exceptions;

public class MissingStoryTextArgumentException : BDTestException
{
    private readonly string _argument;
    public override string Message => $"An argument has not been added to the story attribute: {_argument}";

    public MissingStoryTextArgumentException(string argument)
    {
        _argument = argument;
    }
}