﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDTest.Output
{
    internal class TestOutputData : TextWriter
    {
        private static readonly List<KeyValuePair<int?, char>> ThreadAndChars = new List<KeyValuePair<int?, char>>();
        public static readonly ConsoleOutputInterceptor Instance = new ConsoleOutputInterceptor();
        private static readonly object Lock = new object();

        public override void Write(char value)
        {
            lock (Lock)
            {
                ThreadAndChars.Add(new KeyValuePair<int?, char>(Task.CurrentId, value));
            }
        }

        public override string ToString()
        {
            lock (Lock)
            {
                var thisThreadValues = ThreadAndChars.Where(it => it.Key == Task.CurrentId).Select(it => it.Value)
                    .ToList();
                return string.Join("", thisThreadValues);
            }
        }

        public static void ClearCurrentTaskData()
        {
            lock (Lock)
            {
                ThreadAndChars.RemoveAll(it => it.Key == Task.CurrentId);
            }
        }

        public override Encoding Encoding { get; } = Encoding.UTF8;
    }
}
