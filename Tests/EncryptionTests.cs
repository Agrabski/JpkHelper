using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using JpkHelper;
using System.Text;

namespace Tests;

public class EncryptionTests
{
    [Fact]
    public async Task EncryptionUtilityEncryptsFilesTheSameWayAsMinistryCommandAsync()
    {
        var key = Enumerable.Range(0, 32).Select(x => ((byte)x)).ToArray();
        var iv = Enumerable.Range(0, 16).Select(x => ((byte)x)).ToArray();
        var testFile = Path.GetFullPath(Path.Combine("EncryptionTestFiles", "test-file.txt"));
        var reference = await GetReferenceFileAsync(key, iv, testFile);
        var test = EncryptionHelper.Encrypt(key, iv, await File.ReadAllBytesAsync(testFile));

        Assert.Equal(reference, test);

    }

    private static async Task<byte[]> GetReferenceFileAsync(byte[] key, byte[] iv, string testFile)
    {

        var container = new ContainerBuilder()
            .WithImage("salrashid123/openssl")
            .WithCommand(
                "enc",
                "-aes-256-cbc",
                "-K",
                ToLinuxHex(key),
                "-iv",
                ToLinuxHex(iv),
                "-a",
                "-nosalt",
                "-in",
                "/var/test-file",
                "-out",
                "/var/out-file"
            )
            .WithBindMount(testFile, "/var/test-file", AccessMode.ReadOnly)
            .Build();
        await container.StartAsync();
        var base64Encoded = Encoding.ASCII.GetString(await container.ReadFileAsync("/var/out-file"));
        return Convert.FromBase64String(base64Encoded);
    }

    private static string ToLinuxHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLower();
}
