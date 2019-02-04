using System;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class StoryText : IEquatable<StoryText>
    {
        [JsonProperty]
        public string Story { get; private set; }

        public StoryText(string story)
        {
            Story = story;
        }

        [JsonConstructor]
        private StoryText()
        {

        }

        protected bool Equals(StoryText other)
        {
            return string.Equals(Story, other.Story);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((StoryText) obj);
        }

        public override int GetHashCode()
        {
            return (Story != null ? Story.GetHashCode() : 0);
        }

        bool IEquatable<StoryText>.Equals(StoryText other)
        {
            return Story == other.Story;
        }
    }
}
