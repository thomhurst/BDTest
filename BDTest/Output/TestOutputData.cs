using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDTest.Output
{
    internal class TestOutputData : TextWriter
    {
        private static readonly ConcurrentDictionary<int?, StringBuilder> ThreadAndChars = new ConcurrentDictionary<int?, StringBuilder>();
        public static readonly ConsoleOutputInterceptor Instance = new ConsoleOutputInterceptor();

        public override void Write(char value)
        {
            if (ThreadAndChars.TryGetValue(Task.CurrentId ?? 0, out var existingStringBuilder))
            {
                existingStringBuilder.Append(value);
            }
            else
            {
                ThreadAndChars.TryAdd(Task.CurrentId ?? 0, new StringBuilder(value.ToString()));
            }
        }

        public override string ToString()
        {
            return ThreadAndChars[Task.CurrentId].ToString();
        }

        public static void ClearCurrentTaskData()
        {
            ThreadAndChars.TryRemove(Task.CurrentId ?? 0, out _);
        }

        public override Encoding Encoding { get; } = Encoding.UTF8;
    }
}
