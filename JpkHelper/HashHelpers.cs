using System.Security.Cryptography;

namespace itp.Commads;

internal static class HashHelpers
{

    public static string CalculateMD5(string filename)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filename);
        var hash = md5.ComputeHash(stream);
        return Convert.ToBase64String(hash);
    }
}