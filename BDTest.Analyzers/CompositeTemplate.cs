namespace BDTest.Analyzers;

// Parse composite formats without rendering padding or executing a user's formatter.
// Follows the argument-hole grammar in System.Text.ValueStringBuilder.AppendFormatHelper.
internal static class CompositeTemplate
{
    internal static bool IsValid(string text, int count)
    {
        var position = 0;
        while (position < text.Length)
        {
            var brace = text[position++];
            if (brace != '{' && brace != '}') continue;
            if (position < text.Length && text[position] == brace)
            {
                position++;
                continue;
            }
            if (brace == '}' || !Number(text, ref position, out var index) || index >= count) return false;
            Spaces(text, ref position);
            if (position < text.Length && text[position] == ',')
            {
                position++;
                Spaces(text, ref position);
                if (position < text.Length && text[position] == '-') position++;
                if (!Number(text, ref position, out _)) return false;
                Spaces(text, ref position);
            }
            if (position < text.Length && text[position] == ':')
            {
                position++;
                while (position < text.Length && text[position] != '}')
                    if (text[position++] == '{') return false;
            }
            if (position >= text.Length || text[position++] != '}') return false;
        }
        return true;
    }

    private static bool Number(string text, ref int position, out int value)
    {
        value = 0;
        var start = position;
        // string.Format stops reading digits once the current value reaches this limit.
        while (position < text.Length && text[position] >= '0' && text[position] <= '9' && value < 1_000_000)
            value = value * 10 + text[position++] - '0';
        return position > start;
    }

    private static void Spaces(string text, ref int position)
    {
        while (position < text.Length && text[position] == ' ') position++;
    }
}
