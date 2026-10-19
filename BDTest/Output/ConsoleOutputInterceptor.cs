using System.Text;

namespace BDTest.Output;

internal class ConsoleOutputInterceptor : TextWriter
{
    internal readonly TextWriter One;
    internal readonly TestOutputData Two;
    internal TextWriter SynchronizedWriter { get; }

    public ConsoleOutputInterceptor() : this(Console.Out)
    {
    }

    internal ConsoleOutputInterceptor(TextWriter originalWriter)
    {
        One = originalWriter;
        Two = new TestOutputData();
        // Console.SetOut synchronizes its argument. Reuse that wrapper so Unix
        // ConsolePal and concurrent writers always acquire the same monitor.
        SynchronizedWriter = TextWriter.Synchronized(this);
    }

    internal void WriteLineToConsoleOnly(string? value)
    {
        // Unix ConsolePal locks Console.Out while writing to the original stream.
        // Take that outer lock first, just like writes through the interceptor.
        lock (SynchronizedWriter)
        {
            One.WriteLine(value);
        }
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

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        One.Write(value);
        Two.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        // Validate the slice before either destination sees any output.
        Write(new string(buffer, index, count));
    }

    public override void WriteLine() => Write(NewLine);

    public override void WriteLine(string? value) => Write(value + NewLine);

    public override void WriteLine(char value) => Write(value + NewLine);

    public override void WriteLine(char[]? buffer) => WriteLine(buffer == null ? null : new string(buffer));

    public override void WriteLine(char[] buffer, int index, int count) => WriteLine(new string(buffer, index, count));

    public override string ToString()
    {
        return Two.ToString();
    }
}