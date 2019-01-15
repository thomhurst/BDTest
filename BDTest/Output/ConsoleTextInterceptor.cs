using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDTest.Output
{
    class ConsoleTextInterceptor : TextWriter
    {
        private static readonly List<KeyValuePair<int?, char>> ThreadAndChars = new List<KeyValuePair<int?, char>>();
        public static readonly ConsoleTextInterceptor Instance = new ConsoleTextInterceptor(Encoding.UTF8);

        internal ConsoleTextInterceptor(Encoding encoding)
        {
            Encoding = encoding;
        }

        public override void Write(char value)
        {
            ThreadAndChars.Add(new KeyValuePair<int?, char>(Task.CurrentId, value));
            base.Write(value);
        }

        public override string ToString()
        {
            var thisThreadValues = ThreadAndChars.Where(it => it.Key == Task.CurrentId).Select(it => it.Value).ToList();
            return string.Join("", thisThreadValues);
        }

        public void ClearCurrentTaskData()
        {
            ThreadAndChars.RemoveAll(it => it.Key == Task.CurrentId);
        }

        public override Encoding Encoding { get; }
    }
}
