// See https://aka.ms/new-console-template for more information
using CommandLine;
using CommandLine.Text;
using itp.Commads;
using System.Reflection;
var parser = new Parser(c =>
{
    c.AutoHelp = true;
    c.HelpWriter = null;
});
var result = parser.ParseArguments<SendCommand>(args);
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
await Parser.Default.ParseArguments<SendCommand>(args)
    .WithParsedAsync(c => c.Execute())
    ;

