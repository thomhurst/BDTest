using System;
using System.IO;
using System.Text;

namespace BDTest.Output
{
    internal class ConsoleOutputInterceptor : TextWriter
    {
        private readonly TextWriter _one;
        private readonly TestOutputData _two;

        public ConsoleOutputInterceptor()
        {
            _one = Console.Out;
            _two = new TestOutputData();
        }

        public override Encoding Encoding => _one.Encoding;

        public override void Flush()
        {
            _one.Flush();
            _two.Flush();
        }

        public override void Write(char value)
        {
            _one.Write(value);
            _two.Write(value);
        }

        public void ClearCurrentTaskData()
        {
            _two.ClearCurrentTaskData();
        }

    }
}
