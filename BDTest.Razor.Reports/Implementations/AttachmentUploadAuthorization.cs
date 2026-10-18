using System.Security.Cryptography;
using System.Text;

namespace BDTest.Razor.Reports.Implementations;

// A capability is issued only with a newly stored report. It never appears in report JSON or URLs.
internal static class AttachmentUploadAuthorization
{
    internal static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    internal static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    internal static bool IsValid(string hash, string token) => hash?.Length == 64 && token?.Length == 64
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(Hash(token)));
}
