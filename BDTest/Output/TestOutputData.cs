using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BDTest.Output
{
    internal class TestOutputData : TextWriter
    {
        private static readonly Dictionary<int?, StringBuilder> ThreadAndChars = new Dictionary<int?, StringBuilder>();
        public static readonly ConsoleOutputInterceptor Instance = new ConsoleOutputInterceptor();
        private static readonly object Lock = new object();

        public override void Write(char value)
        {
            lock (Lock)
            {
                if (ThreadAndChars.TryGetValue(Task.CurrentId ?? 0, out var existingStringBuilder))
                {
                    existingStringBuilder.Append(value);
                }
                else
                {
                    ThreadAndChars.Add(Task.CurrentId ?? 0, new StringBuilder(value.ToString()));
                }
            }
        }

        public override string ToString()
        {
            lock (Lock)
            {
                return ThreadAndChars.TryGetValue(Task.CurrentId ?? 0, out var stringBuilder)
                    ? stringBuilder.ToString()
                    : string.Empty;
            }
        }

        public static void ClearCurrentTaskData()
        {
            lock (Lock)
            {
                ThreadAndChars.Remove(Task.CurrentId ?? 0);
            }
        }

        public override Encoding Encoding { get; } = Encoding.UTF8;
    }
}
