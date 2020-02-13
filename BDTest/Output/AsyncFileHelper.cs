using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BDTest.Output
{
    public static class AsyncFileHelper
    {
        public static async Task AppendTextAsync(string filePath, string text)
        {
            var encodedText = Encoding.UTF8.GetBytes(text);

            using (var sourceStream = new FileStream(filePath,
                FileMode.Append, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
            }
        }
    }
}