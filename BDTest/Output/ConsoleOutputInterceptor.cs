using System.Text;

namespace BDTest.Output;

internal class ConsoleOutputInterceptor : TextWriter
{
    internal readonly TextWriter One;
    internal readonly TestOutputData Two;

    public ConsoleOutputInterceptor()
    {
        One = Console.Out;
        Two = new TestOutputData();
    }

    public override Encoding Encoding => One.Encoding;

    public override void Flush()
    {
        One.Flush();
        Two.Flush();
    }

    public override void Write(char value)
    {
        One.Write(value);
        Two.Write(value);
    }

    public override string ToString()
    {
        return Two.ToString();
    }
}