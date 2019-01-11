using BDTest.Test;

namespace BDTest.Reporters
{
    public abstract class Reporter
    {

        public virtual void WriteStartLine()
        {
            WriteLine("-------------------------------");
        }

        public abstract void WriteLine(string text, params object[] args);

        public abstract void WriteStory(StoryText storyText);
        public abstract void WriteScenario(ScenarioText scenarioText);

        public void NewLine() => WriteLine("");

        public virtual void WriteEndLine()
        {
            WriteLine("-------------------------------");
            NewLine();
        }

        internal void Finish()
        {
            WriteEndLine();
            OnFinish();
        }

        public abstract void OnFinish();
    }
}
