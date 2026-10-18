using System.Runtime.Serialization;

namespace BDTest.Exceptions;

public class BDTestException : Exception
{
    public BDTestException()
    {
    }

#if NET8_0_OR_GREATER
    [Obsolete("Formatter-based serialization is obsolete.")]
#endif
    protected BDTestException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public BDTestException(string? message) : base(message)
    {
    }

    public BDTestException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}