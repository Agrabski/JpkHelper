using CommandLine;
using JpkHelper.Localisation;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Schema;

namespace JpkHelper.Commads;

[Verb("make", HelpText = nameof(Commands.MakeCommandHelpText), ResourceType = typeof(Commands))]
public partial class MakeManifestCommand
{
    private readonly Dictionary<Regex, string> _schemaPaths = new()
    {
        [ITPRegex()] = "Schemas/ITP_v2-2.xsd"
    };

    public const string ManifestFileName = "initUpload.xml";

    [Option('f', "files", HelpText = nameof(Commands.MakeManifestFilesProperty), ResourceType = typeof(Commands),
        Min = 1)]
    public required IEnumerable<string> FilePaths { get; set; }

    [Option('k', "aes-key-behaviour", Default = AESKeyBehaviour.None,
        HelpText = nameof(Commands.MakeManifestAESKeyBehaviourProperty), ResourceType = typeof(Commands))]
    public AESKeyBehaviour AESKeyBehaviour { get; set; }

    [Option('e', "enviroment-type", Default = EnvironmentType.Test,
        HelpText = nameof(Commands.MakeManifestEnviromentTypeProperty), ResourceType = typeof(Commands))]
    public EnvironmentType EnvironmentType { get; set; }

    [Option('o', "output", Default = "./out/", HelpText = nameof(Commands.MakeManifestOutputPathProperty),
        ResourceType = typeof(Commands))]
    public required string OutputPath { get; set; }

    [Option('c', "certificate-file", Default = null, HelpText = nameof(Commands.MakeManifestCertificateFileProperty),
        ResourceType = typeof(Commands))]
    public string? CertificateFile { get; set; }

    [Option('q', "no-help-text", Default = true, HelpText = nameof(Commands.MakeManifestNoHelpTextProperty),
        ResourceType = typeof(Commands))]
    public bool NoHelpText { get; set; }

    [Option("fail-on-expired-certificate", Default = true,
        HelpText = nameof(Commands.MakeManifestFailOnExpiredCertificateProperty), ResourceType = typeof(Commands))]
    public bool FailOnExpiredCertificate { get; set; }

    private static string CertificatesDirectoryPath => Path.Combine(BinDirectory, "Certificates");
    private static string SchemasDirectoryPath => Path.Combine(BinDirectory, "Schemas");

    private static string BinDirectory => Path.GetDirectoryName(AppContext.BaseDirectory) ?? "";

    private const int
        CompressedFileSizeLimit =
            60 * 1024 * 1024; // 60 MB (Windows flavour so actually 60MiB) TODO: ask the ministry of finance for clarification

    public async Task Execute()
    {
        Directory.CreateDirectory(OutputPath);
        var certificatePath = PickCertificatePath();
        var sendCommands = new List<string>();
        foreach (var file in FilePaths)
        {
            var aesKey = new byte[32];
            var iv = new byte[16];
            Random.Shared.NextBytes(aesKey);
            Random.Shared.NextBytes(iv);
            var fileName = Path.GetFileName(file);
            var compressed = await Compress(file);
            var parts = compressed.Chunk(CompressedFileSizeLimit).Select((chunk, index) =>
            {
                var result = Encrypt(aesKey, iv, chunk);
                var partName = $"{fileName}.zip.{index + 1:D3}.aes";
                using var destination = File.Create(Path.Combine(OutputPath, partName));
                destination.Write(result);
                return new CompressedFilePartInfo(
                    partName,
                    result.Length,
                    HashHelpers.CalculateMD5(new MemoryStream(result))
                );
            });
            var compressedAndZippedFile = new CompressedFileInfo(
                fileName,
                new FileInfo(file).Length,
                CalculateSha256Hash(file),
                parts.ToArray()
            );
            await ValidateDocumentSchemaAsync(file);

            var manifest = CreateManifest(compressedAndZippedFile, aesKey, iv, certificatePath);
            var schemaSet = new XmlSchemaSet();
            schemaSet.Add("http://e-dokumenty.mf.gov.pl", "https://www.podatki.gov.pl/media/5881/initupload.xsd");
            manifest.Validate(schemaSet, null);
            var manifestFileName = PickFileName(FilePaths.Count() > 1, file);
            await File.WriteAllTextAsync(Path.Combine(OutputPath, manifestFileName), manifest.ToString());
            switch (AESKeyBehaviour)
            {
                case AESKeyBehaviour.ToFile:
                    var path = Path.Combine(OutputPath, "aes_key.base64.txt");
                    Console.WriteLine(string.Format(Strings.SavingBase64AesKey, path));
                    await File.WriteAllTextAsync(path, Convert.ToBase64String(aesKey));
                    break;
                case AESKeyBehaviour.ToConsole:
                    Console.WriteLine(string.Format(Strings.AesKey, Convert.ToBase64String(aesKey)));
                    break;
                default:
                    break;
            }
            sendCommands.Add($"{AppDomain.CurrentDomain.FriendlyName} send -e {EnvironmentType} -p \"{Path.Combine(OutputPath, manifestFileName)}");
        }

        if (!NoHelpText)
        {
            Console.WriteLine(string.Format(Strings.FilesReadyToSend, Path.GetFullPath(OutputPath)));
            Console.WriteLine(Strings.ToSendEnterFollowingCommand);
            Console.WriteLine(string.Join("\n", sendCommands));
        }
    }

    private string PickFileName(bool multipleFiles, string file)
    {
        if (!multipleFiles)
            return ManifestFileName;
        return $"{Path.GetFileNameWithoutExtension(file)}-{ManifestFileName}";
    }

    private string PickCertificatePath()
    {
        if (CertificateFile is not null)
            return CertificateFile;
        return EnvironmentType switch
        {
            EnvironmentType.Test => Path.Combine(CertificatesDirectoryPath, "test_env.pem"),
            EnvironmentType.Production => Path.Combine(CertificatesDirectoryPath, "production_env.pem"),
            _ => throw new NotImplementedException(Strings.CertificateNotSaved)
        };
    }

    private async Task ValidateDocumentSchemaAsync(string documentPath)
    {
        var schema = await PickSchemaAsync(Path.GetFileName(documentPath));
        var document = XDocument.Load(documentPath);
        Console.WriteLine(string.Format(Strings.ValidatingFile, documentPath));
        var success = true;
        try
        {
            document.Validate(schema, (o, e) =>
            {
                ValidationErrorHandler(o, e);
                success = false;
            });
            if (success)
                Console.WriteLine("OK");
            else
            {
                Console.WriteLine(Strings.ValidationFailed);
                throw new FileFailedValidationException();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(Strings.ValidationErrorMessage, documentPath, ex.Message));
            throw;
        }
    }

    private void ValidationErrorHandler(object? sender, ValidationEventArgs e)
    {
        Console.WriteLine(e.Message);
    }

    private async Task<XmlSchemaSet> PickSchemaAsync(string v)
    {
        var result = new XmlSchemaSet();
        foreach (var (regex, schema) in _schemaPaths)
            if (regex.IsMatch(v))
            {
                using var reader = File.OpenText(schema);
                var xmlSchema = XmlSchema.Read(reader, ValidationErrorHandler);

                result.Add(xmlSchema!);
                await LoadSchemasRecursivley(xmlSchema, result);
                result.Compile();
                return result;
            }

        throw new Exception("Unknown file type");
    }

    private async Task LoadSchemasRecursivley(XmlSchema schema, XmlSchemaSet schemaSet)
    {
        foreach (var include in schema.Includes)
        {
            using var data = await LoadSchema(include);
            var newSchema = XmlSchema.Read(data, ValidationErrorHandler)!;
            schemaSet.Add(newSchema);
            await LoadSchemasRecursivley(newSchema, schemaSet);
        }
    }

    private async Task<Stream> LoadSchema(XmlSchemaObject include)
    {
        var path = include switch
        {
            XmlSchemaImport import => import.SchemaLocation,
            XmlSchemaInclude inc => inc.SchemaLocation,
            _ => include.SourceUri
        } ?? throw new Exception($"Failed to find schema location for element: {include}");

        return await (await new HttpClient().GetAsync(path!)).Content.ReadAsStreamAsync();
    }

    private XDocument CreateManifest(CompressedFileInfo compressedAndZippedFile, byte[] aesKey, byte[] iv,
        string certificatePath)
    {
        var encryptedAndEncodedAesKey = EncryptAndEncodeAesKey(aesKey, certificatePath);
        var xmlns = XNamespace.Get("http://e-dokumenty.mf.gov.pl");
        var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");
        var schemaLocation =
            XNamespace.Get("http://e-dokumenty.mf.gov.pl https://www.podatki.gov.pl/media/5881/initupload.xsd");

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                xmlns + "InitUpload",
                new XAttribute("xmlns", xmlns),
                new XAttribute(xsi + "schemaLocation", schemaLocation),
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XElement(xmlns + "DocumentType", "JPK"),
                new XElement(xmlns + "Version", "01.02.01.20160617"),
                new XElement(
                    xmlns + "EncryptionKey",
                    new XAttribute("algorithm", "RSA"),
                    new XAttribute("encoding", "Base64"),
                    new XAttribute("mode", "ECB"),
                    new XAttribute("padding", "PKCS#1"),
                    encryptedAndEncodedAesKey
                ),
                new XElement(xmlns + "DocumentList", CreateDocumentNode(compressedAndZippedFile, iv, xmlns))
            )
        );
    }

    private XElement CreateDocumentNode(CompressedFileInfo info, byte[] iv, XNamespace xmlns)
    {
        return new XElement(
            xmlns + "Document",
            new XElement(
                xmlns + "FormCode",
                new XAttribute("schemaVersion", "2-2"),
                new XAttribute("systemCode", "ITP (2)"),
                "ITP"
            ),
            new XElement(xmlns + "FileName", info.FileName),
            new XElement(xmlns + "ContentLength", info.ContentLength),
            new XElement(
                xmlns + "HashValue",
                new XAttribute("algorithm", "SHA-256"),
                new XAttribute("encoding", "Base64"),
                Convert.ToBase64String(info.SHA256HashValue)
            ),
            new XElement(
                xmlns + "FileSignatureList",
                new XAttribute("filesNumber", "1"),
                new XElement(
                    xmlns + "Packaging",
                    new XElement(
                        xmlns + "SplitZip",
                        new XAttribute("mode", "zip"),
                        new XAttribute("type", "split")
                    )
                ),
                new XElement(
                    xmlns + "Encryption",
                    new XElement(xmlns +
                                 "AES",
                        new XAttribute("block", 16),
                        new XAttribute("mode", "CBC"),
                        new XAttribute("padding", "PKCS#7"),
                        new XAttribute("size", 256),
                        new XElement(
                            xmlns + "IV",
                            new XAttribute("bytes", 16),
                            new XAttribute("encoding", "Base64"),
                            Convert.ToBase64String(iv)
                        )
                    )
                ),
                info.Parts.Select((part, index) =>
                    new XElement(
                        xmlns + "FileSignature",
                        new XElement(xmlns + "OrdinalNumber", index + 1),
                        new XElement(xmlns + "FileName", part.Name),
                        new XElement(xmlns + "ContentLength", part.ContentLength),
                        new XElement(
                            xmlns + "HashValue",
                            new XAttribute("algorithm", "MD5"),
                            new XAttribute("encoding", "Base64"),
                            part.MD5HashValueBase64
                        )
                    )
                )
            )
        );
    }

    private string EncryptAndEncodeAesKey(byte[] aesKey, string certificatePath)
    {
        using var cert = new X509Certificate2(certificatePath);
        if (cert.NotAfter < DateTime.Now)
        {
            Console.WriteLine(Strings.CertificateExpiredWarning);
            if (FailOnExpiredCertificate)
                throw new Exception("Expired certificate");
        }

        var rsa = cert.PublicKey.GetRSAPublicKey() ?? throw new Exception();
        var result = rsa.Encrypt(aesKey, RSAEncryptionPadding.Pkcs1);
        return Convert.ToBase64String(result);
    }

    private byte[] CalculateSha256Hash(string file)
    {
        using var hash = SHA256.Create();
        using var f = File.OpenRead(file);
        return hash.ComputeHash(f);
    }

    private static byte[] Encrypt(byte[] aesKey, byte[] iv, byte[] compressed)
    {
        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        return aes.EncryptCbc(compressed.AsSpan(), iv, PaddingMode.PKCS7);
    }

    private static async Task<byte[]> Compress(string filePath)
    {
        var entryName = Path.GetFileName(filePath);
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            using var sourceStream = File.OpenRead(filePath);
            await sourceStream.CopyToAsync(entryStream);
            entryStream.Close();
        }

        return buffer.ToArray();
    }

    private readonly record struct CompressedFileInfo(
        string FileName,
        long ContentLength,
        byte[] SHA256HashValue,
        CompressedFilePartInfo[] Parts
    );

    private readonly record struct CompressedFilePartInfo(
        string Name,
        long ContentLength,
        string MD5HashValueBase64
    );

    [GeneratedRegex("^ITP.*")]
    private static partial Regex ITPRegex();
}