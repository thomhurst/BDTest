using BDTest.Test;

namespace BDTest.Reporters
{
    public abstract class Reporter
    {
        public abstract void WriteLine(string text, params object[] args);

        public abstract void WriteStory(StoryText storyText);
        public abstract void WriteScenario(ScenarioText scenarioText);

        public void NewLine() => WriteLine("");

        public virtual void WriteDashedLineSeparator()
        {
            WriteLine("-------------------------------");
            NewLine();
        }

        internal void Finish()
        {
            WriteDashedLineSeparator();
            OnFinish();
        }

        public abstract void OnFinish();
    }
}
