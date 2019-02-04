using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BDTest.Test;

namespace BDTest.Reporters
{
    internal class FileReporter : Reporter
    {
        private static readonly string DirectoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string FilePath = Path.Combine(DirectoryName, "test_output.txt");
        private readonly StringBuilder _contents = new StringBuilder();

        static FileReporter()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }

        public override void WriteLine(string text, params object[] args)
        {
            _contents.AppendLine(text);
        }

        public override void WriteStory(StoryText storyText)
        {
            if (storyText?.Story == null)
            {
                return;
            }

            WriteLine(storyText.Story);
        }

        public override void WriteScenario(ScenarioText scenarioText)
        {
            if (scenarioText?.Scenario == null)
            {
                return;
            }

            WriteLine(scenarioText.Scenario);
        }

        public override void OnFinish()
        {
            WriteOutputToFile(_contents.ToString());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void WriteOutputToFile(string contents)
        {
            File.AppendAllText(FilePath, contents);
        }
    }
}
