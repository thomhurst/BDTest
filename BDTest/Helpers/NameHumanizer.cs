using System.Text;

namespace BDTest.Helpers;

internal static class NameHumanizer
{
    internal static string Humanize(string memberName)
    {
        if (string.IsNullOrEmpty(memberName)) return memberName;

        // Preserve compiler-generated and other non-identifier names as a readable fallback.
        if (memberName.Any(character => character != '_' && !char.IsLetterOrDigit(character))) return memberName;

        string text;
        if (memberName.IndexOf('_') >= 0)
        {
            text = string.Join(" ", memberName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries));
            if (text.Length == 0) return memberName;
        }
        else
        {
            var builder = new StringBuilder(memberName.Length);
            var wordStart = 0;
            for (var index = 1; index < memberName.Length; index++)
            {
                var previous = memberName[index - 1];
                var current = memberName[index];
                var digitBoundary = char.IsDigit(previous) != char.IsDigit(current);
                var capitalBoundary = char.IsUpper(current) && (char.IsLower(previous)
                    || (char.IsUpper(previous) && index + 1 < memberName.Length && char.IsLower(memberName[index + 1])));
                if (!digitBoundary && !capitalBoundary) continue;

                AppendWord(builder, memberName.Substring(wordStart, index - wordStart));
                wordStart = index;
            }
            AppendWord(builder, memberName.Substring(wordStart));
            text = builder.ToString();
        }

        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static void AppendWord(StringBuilder builder, string word)
    {
        if (builder.Length > 0) builder.Append(' ');
        var acronym = word.Length >= 2 && word.All(char.IsUpper);
        builder.Append(acronym ? word : word.ToLowerInvariant());
    }
}
