using System.Security.Cryptography;

namespace JpkHelper;

internal static class HashHelpers
{

    public static string CalculateMD5(string filename)
    {
        using var stream = File.OpenRead(filename);
        return CalculateMD5(stream);
    }

    public static string CalculateMD5(Stream stream)
    {
        var md5 = MD5.Create();
        var hash = md5.ComputeHash(stream);
        return Convert.ToBase64String(hash);
    }
}