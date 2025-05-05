using System.Security.Cryptography;

namespace JpkHelper;

public static class EncryptionHelper
{
    public const int BlockSize = 128;
    public static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLower();

    public static byte[] Encrypt(byte[] aesKey, byte[] iv, byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Key = aesKey;
        aes.IV = iv;
        return aes.EncryptCbc(plaintext,iv);

    }
}