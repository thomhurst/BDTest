using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

namespace BDTest.Compiler;

// Shared by the runtime and compiler tools: replacement text is never parsed again.
internal static class NamedStepTemplate
{
    internal sealed class Token
    {
        internal Token(string text, string? name = null, string? format = null, bool positional = false)
        {
            Text = text;
            Name = name;
            Format = format;
            IsPositional = positional;
        }

        internal string Text { get; }
        internal string? Name { get; }
        internal string? Format { get; }
        internal bool IsPositional { get; }
    }

    internal static IReadOnlyList<Token> Parse(string text)
    {
        var tokens = new List<Token>();
        var literal = new StringBuilder();
        for (var position = 0; position < text.Length; position++)
        {
            var character = text[position];
            if ((character == '{' || character == '}') && position + 1 < text.Length && text[position + 1] == character)
            {
                literal.Append(character);
                position++;
                continue;
            }
            if (character != '{')
            {
                literal.Append(character);
                continue;
            }

            var end = text.IndexOf('}', position + 1);
            if (end < 0)
            {
                literal.Append(text, position, text.Length - position);
                break;
            }
            var content = text.Substring(position + 1, end - position - 1);
            var colon = content.IndexOf(':');
            var name = colon < 0 ? content : content.Substring(0, colon);
            var positional = IsPositionalName(name);
            if (content.IndexOf('{') >= 0 || (!IsName(name) && !positional))
            {
                literal.Append(text, position, end - position + 1);
                position = end;
                continue;
            }
            if (literal.Length != 0)
            {
                tokens.Add(new Token(literal.ToString()));
                literal.Clear();
            }
            tokens.Add(new Token(text.Substring(position, end - position + 1), name,
                colon < 0 ? null : content.Substring(colon + 1), positional));
            position = end;
        }
        if (literal.Length != 0) tokens.Add(new Token(literal.ToString()));
        return tokens;
    }

    internal static IReadOnlyList<string> Validate(string template, IEnumerable<string> parameterNames,
        IEnumerable<string?>? overrides)
    {
        var names = new HashSet<string>(parameterNames, StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var token in Parse(template))
        {
            if (token.IsPositional)
                errors.Add("StepText positional placeholder '" + token.Text + "' is unsupported; use a parameter name");
            else if (token.Name != null && !names.Contains(token.Name))
                errors.Add("StepText placeholder '" + token.Name + "' does not name a visible parameter");
        }
        if (overrides == null)
            errors.Add("StepText overrides must not be a null array");
        else
            foreach (var value in overrides)
            {
                if (!TryParseOverride(value, out var name, out _))
                    errors.Add("StepText overrides must use 'parameterName=text'; positional overrides are unsupported");
                else if (!names.Contains(name))
                    errors.Add("StepText override '" + name + "' does not name a visible parameter");
            }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static bool TryParseOverride(string? text, out string name, out string replacement)
    {
        var equals = text?.IndexOf('=') ?? -1;
        name = equals > 0 ? text!.Substring(0, equals) : "";
        replacement = equals > 0 ? text!.Substring(equals + 1) : "";
        return IsName(name);
    }

    private static bool IsName(string name)
    {
        if (name.Length == 0) return false;
        for (var position = 0; position < name.Length; position++)
        {
            // Classify Unicode scalars, so supplementary characters do not become
            // two invalid surrogate code units. Unpaired surrogates are rejected.
            var category = CharUnicodeInfo.GetUnicodeCategory(name, position);
            var letter = category == UnicodeCategory.UppercaseLetter || category == UnicodeCategory.LowercaseLetter ||
                category == UnicodeCategory.TitlecaseLetter || category == UnicodeCategory.ModifierLetter ||
                category == UnicodeCategory.OtherLetter || category == UnicodeCategory.LetterNumber;
            if (!letter && name[position] != '_' && (position == 0 ||
                (category != UnicodeCategory.DecimalDigitNumber && category != UnicodeCategory.ConnectorPunctuation &&
                 category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.SpacingCombiningMark &&
                 category != UnicodeCategory.Format))) return false;
            if (char.IsHighSurrogate(name[position])) position++;
        }
        return true;
    }

    // Recognize obsolete composite alignment without interpreting malformed brace text.
    private static bool IsPositionalName(string name)
    {
        var comma = name.IndexOf(',');
        var index = (comma < 0 ? name : name.Substring(0, comma)).TrimEnd(' ');
        if (index.Length == 0 || !index.All(c => c >= '0' && c <= '9')) return false;
        if (comma < 0) return true;
        var alignment = name.Substring(comma + 1).Trim(' ');
        if (alignment.StartsWith("-", StringComparison.Ordinal)) alignment = alignment.Substring(1);
        return alignment.Length > 0 && alignment.All(c => c >= '0' && c <= '9');
    }
}
