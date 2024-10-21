using System.Security.Cryptography;

namespace JpkHelper;

public static class EncryptionHelper
{
    public static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLower();

    public static byte[] Encrypt(byte[] aesKey, byte[] iv, byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Key = aesKey;
        aes.IV = iv;
        return aes.CreateEncryptor().TransformFinalBlock(plaintext, 0, plaintext.Length);

    }
}