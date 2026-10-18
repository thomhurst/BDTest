namespace BDTest.Attributes;

/// <summary>Adds a tag to a scenario method or every scenario in a story class.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class BDTestTagAttribute : Attribute
{
    /// <summary>The tag name, with leading and trailing whitespace removed.</summary>
    public string Tag { get; }

    public BDTestTagAttribute(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("A tag must contain at least one non-whitespace character.", nameof(tag));
        }

        Tag = tag.Trim();
    }
}
