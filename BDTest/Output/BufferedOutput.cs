using System.Text;

namespace BDTest.Output;

// Append without materializing the accumulated text; snapshot only when the report reads it.
internal sealed class BufferedOutput
{
    private readonly object _lock = new();
    private StringBuilder _builder;
    private string _value;

    internal string Value
    {
        get
        {
            lock (_lock)
            {
                return _builder?.ToString() ?? _value;
            }
        }
        set
        {
            lock (_lock)
            {
                _value = value;
                _builder = null;
            }
        }
    }

    internal void Append(char value)
    {
        lock (_lock)
        {
            (_builder ??= new StringBuilder(_value)).Append(value);
        }
    }

    internal void Append(string value, bool appendNewLine = false)
    {
        lock (_lock)
        {
            (_builder ??= new StringBuilder(_value)).Append(value);
            if (appendNewLine)
            {
                _builder.Append(Environment.NewLine);
            }
        }
    }
}
