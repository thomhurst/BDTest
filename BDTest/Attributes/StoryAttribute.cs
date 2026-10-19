using BDTest.Exceptions;

namespace BDTest;

[AttributeUsage(AttributeTargets.Class)]
public class StoryAttribute : Attribute
{
    public string? AsA { get; set; }
    public string? AsAn { get; set; }
    public string? IWant { get; set; }
    public string? SoThat { get; set; }

    public string GetStoryText()
    {
        ValidateArguments();
            
        var role = GetRoleText();
        var want = AddPrefix(IWant ?? throw new MissingStoryTextArgumentException(nameof(IWant)), "I want");
        var benefit = AddPrefix(SoThat ?? throw new MissingStoryTextArgumentException(nameof(SoThat)), "So that");

        return $"{role}{Environment.NewLine}{want}{Environment.NewLine}{benefit}{Environment.NewLine}";
    }

    private string GetRoleText()
    {
        var role = (string.IsNullOrEmpty(AsAn) ? AsA : AsAn)
            ?? throw new MissingStoryTextArgumentException(nameof(AsA));
        if (HasWordPrefix(role, "As a") || HasWordPrefix(role, "As an"))
        {
            return role;
        }

        if (HasWordPrefix(role, "a") || HasWordPrefix(role, "an"))
        {
            return $"As {role}";
        }

        var startsWithVowel = "aeiouAEIOU".IndexOf(role[0]) >= 0;
        var useAn = !string.IsNullOrEmpty(AsAn)
                    || (startsWithVowel && !role.StartsWith("user", StringComparison.InvariantCultureIgnoreCase));
        return $"As {(useAn ? "an" : "a")} {role}";
    }

    private static string AddPrefix(string text, string prefix)
    {
        return HasWordPrefix(text, prefix) ? text : $"{prefix} {text}";
    }

    private static bool HasWordPrefix(string text, string prefix)
    {
        return text.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)
               && (text.Length == prefix.Length
                   || (!char.IsLetterOrDigit(text[prefix.Length]) && text[prefix.Length] != '_'));
    }

    private void ValidateArguments()
    {
        if (string.IsNullOrEmpty(AsA) && string.IsNullOrEmpty(AsAn))
        {
            throw new MissingStoryTextArgumentException(nameof(AsA));
        }
            
        if (!string.IsNullOrEmpty(AsA) && !string.IsNullOrEmpty(AsAn))
        {
            throw new DuplicateStoryTextArgumentException(nameof(AsA), nameof(AsAn));
        }
            
        if (string.IsNullOrEmpty(IWant))
        {
            throw new MissingStoryTextArgumentException(nameof(IWant));
        }
            
        if (string.IsNullOrEmpty(SoThat))
        {
            throw new MissingStoryTextArgumentException(nameof(SoThat));
        }
    }
}