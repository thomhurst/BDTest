using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BDTest.Paths;

namespace BDTest.Output
{
    internal class TestOutputData : TextWriter
    {
        private static readonly Dictionary<Guid, StringBuilder> ThreadAndChars = new Dictionary<Guid, StringBuilder>();
        public static readonly ConsoleOutputInterceptor Instance = new ConsoleOutputInterceptor();
        private static readonly object Lock = new object();
        private static readonly AsyncLocal<Guid?> AsyncLocalTestId = new AsyncLocal<Guid?>();
        internal static Guid? TestId {
            get => AsyncLocalTestId.Value;
            set => AsyncLocalTestId.Value = value;
        }

        public override void Write(char value)
        {
            lock (Lock)
            {
                if (TestId == null)
                {
                    return;
                }
                
                if (ThreadAndChars.TryGetValue((Guid) TestId, out var existingStringBuilder))
                {
                    existingStringBuilder.Append(value);
                }
                else
                {
                    ThreadAndChars.Add((Guid) TestId, new StringBuilder(value.ToString()));
                }
            }
        }

        public override string ToString()
        {
            lock (Lock)
            {
                if (TestId == null)
                {
                    return string.Empty;
                }
                
                return ThreadAndChars.TryGetValue((Guid) TestId, out var stringBuilder)
                    ? stringBuilder.ToString()
                    : string.Empty;
            }
        }

        public static void ClearCurrentTaskData()
        {
            lock (Lock)
            {
                if (TestId == null)
                {
                    return;
                }
                
                ThreadAndChars.Remove((Guid) TestId);
            }
        }

        public override Encoding Encoding { get; } = Encoding.UTF8;

        internal static async Task WriteTearDownOutput(string testId, string text)
        {
            if (testId == null)
            {
                Console.WriteLine("Attempting to write tear down output but no unique test ID has been set in the base class");
                return;
            }

            Console.WriteLine(Environment.NewLine + text);
            await AsyncFileHelper.AppendTextAsync(FileLocations.ScenarioTeardownOutputFilePath(testId), $"{text}{Environment.NewLine}");
        }
    }
}
