using CommandLine;
using CommandLine.Text;
using JpkHelper.Commads;
using System.Reflection;
var parser = new Parser(c =>
{
    c.HelpWriter = null;
    c.CaseInsensitiveEnumValues = true;
    c.CaseSensitive = false;
});
var result = parser.ParseArguments<SendCommand, MakeManifestCommand>(args);
await result.MapResult<SendCommand, MakeManifestCommand, Task>(
    c => c.Execute(),
    c => c.Execute(),
    e =>
    {
        var helpText = HelpText.AutoBuild(
            result,
            h =>
            {
                var assembly = typeof(SendCommand).Assembly;
                var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
                var productName = assembly.GetCustomAttribute<AssemblyProductAttribute>();
                var version = assembly.GetName().Version;
                h.AddEnumValuesToHelpText = true;
                h.Copyright = copyright!.Copyright;
                h.Heading = $"{productName!.Product} {version}";
                return h;

            }
        );
        Console.Write(helpText.ToString());
        return Task.CompletedTask;
    }
);

