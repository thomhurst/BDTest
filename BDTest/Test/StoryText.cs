namespace BDTest.Test
{
    public class StoryText
    {
        public readonly string Story;

        public StoryText(string story)
        {
            Story = story;
        }

        protected bool Equals(StoryText other)
        {
            return string.Equals(Story, other.Story);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((StoryText) obj);
        }

        public override int GetHashCode()
        {
            return (Story != null ? Story.GetHashCode() : 0);
        }
    }
}
