namespace BDTest.Exceptions
{
    public class DuplicateStoryTextArgumentException : BDTestException
    {
        private readonly string _argument1;
        private readonly string _argument2;

        public override string Message => $"An argument has been duplication on the story attribute: {_argument1} & {_argument2}\nPlease supply just one.";

        public DuplicateStoryTextArgumentException(string argument1, string argument2)
        {
            _argument1 = argument1;
            _argument2 = argument2;
        }
    }
}