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
            OutputPath = "./two-files"
        };
        await command.Execute();
        Assert.True(File.Exists($"./two-files/ITP_1/{MakeManifestCommand.ManifestFileName}"));
        Assert.True(File.Exists($"./two-files/ITP_2/{MakeManifestCommand.ManifestFileName}"));
    }
}
