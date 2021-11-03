using System;
using BDTest.Exceptions;

namespace BDTest.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class StoryAttribute : Attribute
    {
        public string AsA { get; set; }
        public string AsAn { get; set; }
        public string IWant { get; set; }
        public string SoThat { get; set; }

        public string GetStoryText()
        {
            ValidateArguments();
            
            PopulateAsA();

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

        private void PopulateAsA()
        {
            if (!string.IsNullOrEmpty(AsAn))
            {
                AsA = AsAn;
            }

            if (AsA.StartsWith("as a", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }

            if (!string.IsNullOrEmpty(AsAn))
            {
                AsA = $"As an {AsAn}";
            }
            else if(AsA.StartsWith("a", StringComparison.InvariantCultureIgnoreCase))
            {
                AsA = $"As an {AsA}";
            } 
            else 
            {
                AsA = $"As a {AsA}";
            }
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
}
