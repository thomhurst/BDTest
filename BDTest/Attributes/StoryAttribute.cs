using System;
using System.Linq;

namespace BDTest.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class StoryAttribute : Attribute
    {
        public string AsA { get; set; }
        public string IWant { get; set; }
        public string SoThat { get; set; }

        public string GetStoryText()
        {
            var firstChar = AsA.ToLowerInvariant()
                .Replace("as an", String.Empty)
                .Replace("as a", String.Empty)
                .Trim()
                .FirstOrDefault();

            var asAPrefix = "As a";
            if ("aeiouAEIOU".IndexOf(firstChar) >= 0)
            {
                asAPrefix = "As an";
            }
            
            if (!AsA.ToLowerInvariant().StartsWith("as a"))
            {
                AsA = $"{asAPrefix} {AsA}";
            }

            if (!IWant.ToLowerInvariant().StartsWith("i want"))
            {
                IWant = $"I want {IWant}";
            }

            if (!SoThat.ToLowerInvariant().StartsWith("so that"))
            {
                SoThat = $"So that {SoThat}";
            }

            return $"{AsA}" +
                   $"{Environment.NewLine}{IWant}" +
                   $"{Environment.NewLine}{SoThat}{Environment.NewLine}";
        }
    }
}
