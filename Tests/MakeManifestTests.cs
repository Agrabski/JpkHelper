using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using JpkHelper.Commads;

namespace Tests;

public class MakeManifestTests
{
    [Fact]
    public async Task MakingManifestOfTwoFilesWorks()
    {
        var command = new MakeManifestCommand()
        {
            FilePaths = ["MakeManifestTestFiles/ITP_1.xml", "MakeManifestTestFiles/ITP_2.xml"],
            OutputPath = "./two-files",
            EnvironmentType = EnvironmentType.Test
        };
        await command.Execute();
        Assert.True(File.Exists($"./two-files/ITP_1-{MakeManifestCommand.ManifestFileName}"));
        Assert.True(File.Exists($"./two-files/ITP_2-{MakeManifestCommand.ManifestFileName}"));
        AssertFileCorectnes(
            $"./two-files/ITP_1-{MakeManifestCommand.ManifestFileName}",
            "MakeManifestTestFiles/ITP_1.xml",
            "./two-files/ITP_1.xml.zip.001.aes"
        );
        AssertFileCorectnes(
            $"./two-files/ITP_2-{MakeManifestCommand.ManifestFileName}",
            "MakeManifestTestFiles/ITP_2.xml",
            "./two-files/ITP_2.xml.zip.001.aes"
        );
    }

    private static void AssertFileCorectnes(string manifest, string file, string encryptedFile)
    {
        var m = XDocument.Load(manifest);
        var length = m
            .Descendants()
            .First(d => d.Name.LocalName == "ContentLength" && d.Parent.Name.LocalName == "FileSignature")
            .Value;
        Assert.Equal(new FileInfo(encryptedFile).Length, long.Parse(length));
        var md5Hash = m
            .Descendants()
            .First(d => d.Name.LocalName == "HashValue" && d.Parent.Name.LocalName == "FileSignature")
            .Value;
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(encryptedFile);
        var hash = md5.ComputeHash(stream);
        Assert.Equal(Convert.ToBase64String(hash), md5Hash);

        var shaHash = m
            .Descendants()
            .First(d => d.Name.LocalName == "HashValue" && d.Parent.Name.LocalName == "Document")
            .Value;
        using var sha256 = SHA256.Create();
        using var s = File.OpenRead(file);
        hash = sha256.ComputeHash(s);
        Assert.Equal(Convert.ToBase64String(hash), shaHash);
        
        var originalFileLength = m
            .Descendants()
            .First(d => d.Name.LocalName == "ContentLength" && d.Parent.Name.LocalName == "Document")
            .Value;
        Assert.Equal(new FileInfo(file).Length, long.Parse(originalFileLength));
    }
}