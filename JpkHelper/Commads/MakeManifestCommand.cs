using CommandLine;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using System.Xml.Schema;

namespace JpkHelper.Commads;
[Verb("make-manifest", aliases: ["make"], HelpText = "Przygotowywuje plik InitUpload.xml oraz szyfruje pliki podlegające wysyłce")]
internal class MakeManifestCommand
{
    private const string ManifestFileName = "initUpload.xml";

    [Option('f', "files", HelpText = "Pliki xml które mają podlegać wysyłce")]
    public required IEnumerable<string> FilePaths { get; set; }
    [Option('k', "aes-key-behaviour", Default = AESKeyBehaviour.None, HelpText = "Czy pokazać wygenerowany klucz symetryczny AES")]
    public AESKeyBehaviour AESKeyBehaviour { get; set; }
    [Option('e', "enviroment-type", Default = EnvironmentType.Test, HelpText = "Typ środowiska")]
    public EnvironmentType EnvironmentType { get; set; }
    [Option('o', "output", HelpText = "Folder w którym umieszczony zostanie wynik operacji", Default = "./out/")]
    public required string OutputPath { get; set; }
    [Option('c', "certificate-file", HelpText = "Ścieżka do pliku z certyfikatem publicznym ministerstwa. Jeśli argument nie zostanie podany, program wybierze klucz na podstawie środowiska i zapisanych danych", Default = null)]
    public string? CertificateFile { get; set; }

    private string CertificatesDirectoryPath => Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory)!, "Certificates");

    internal async Task Execute()
    {
        var certificatePath = PickCertificatePath();
        var aesKey = new byte[32];
        var iv = new byte[16];
        Random.Shared.NextBytes(aesKey);
        Random.Shared.NextBytes(iv);
        Directory.CreateDirectory(OutputPath);
        var compressedAndZippedFiles = new List<CompressedFileInfo>();
        foreach (var file in FilePaths)
        {
            var fileName = Path.GetFileName(file);
            var compressed = await Compress(file);
            var result = Encrypt(aesKey, iv, compressed);
            var compressedFileName = $"{fileName}.zip.001.aes";
            using var destination = File.Create(Path.Combine(OutputPath, compressedFileName));
            destination.Write(result);
            compressedAndZippedFiles.Add(new(
                fileName,
                compressedFileName,
                new FileInfo(file).Length,
                result.LongLength,
                CalculateSha256Hash(file),
                HashHelpers.CalculateMD5(new MemoryStream(result))
            ));
        }
        var manifest = CreateManifest(compressedAndZippedFiles, aesKey, iv, certificatePath);
        var schemaSet = new XmlSchemaSet();
        schemaSet.Add("http://e-dokumenty.mf.gov.pl", "https://www.podatki.gov.pl/media/5881/initupload.xsd");
        manifest.Validate(schemaSet, null);
        await File.WriteAllTextAsync(Path.Combine(OutputPath, ManifestFileName), manifest.ToString());
        switch (AESKeyBehaviour)
        {
            case AESKeyBehaviour.ToFile:
                var path = Path.Combine(OutputPath, "aes_key.base64.txt");
                Console.WriteLine($"Zapisuję klucz AES (base64 encoded) do pliku {path}");
                await File.WriteAllTextAsync(path, Convert.ToBase64String(aesKey));
                break;
            case AESKeyBehaviour.ToConsole:
                Console.WriteLine($"AES key (base64 encoded): '{Convert.ToBase64String(aesKey)}'");
                break;
            default:
                break;
        }
        Console.WriteLine($"Pakiet plików gotowy do wysyłki w folderze \"{Path.GetFullPath(OutputPath)}\"");
        Console.WriteLine($"Aby dokonać wysyłki, wywołaj ten program ponownie w następujący sposób:");
        Console.WriteLine($"{AppDomain.CurrentDomain.FriendlyName} send -e {EnvironmentType} -p \"{Path.Combine(OutputPath, ManifestFileName)}");

    }

    private string PickCertificatePath()
    {
        if (CertificateFile is not null)
            return CertificateFile;
        return EnvironmentType switch
        {
            EnvironmentType.Test => Path.Combine(CertificatesDirectoryPath, "test_env.pem"),
            _ => throw new NotImplementedException("Certyfikat dla tego środowiska nie został zapisany w programie")
        };
    }

    private XDocument CreateManifest(List<CompressedFileInfo> compressedAndZippedFiles, byte[] aesKey, byte[] iv, string certificatePath)
    {
        var encryptedAndEncodedAesKey = EncryptAndEncodeAesKey(aesKey, certificatePath);
        var xmlns = XNamespace.Get("http://e-dokumenty.mf.gov.pl");
        var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");
        var schemaLocation = XNamespace.Get("http://e-dokumenty.mf.gov.pl https://www.podatki.gov.pl/media/5881/initupload.xsd");

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
                new XElement(xmlns + "DocumentList", compressedAndZippedFiles.Select(f => CreateDocumentNode(f, iv, xmlns)))
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
                new XElement(
                    xmlns + "FileSignature",
                    new XElement(xmlns + "OrdinalNumber", 1),
                    new XElement(xmlns + "FileName", info.CompressedFileName),
                    new XElement(xmlns + "ContentLength", info.CompressedContentLength),
                    new XElement(
                        xmlns + "HashValue",
                        new XAttribute("algorithm", "MD5"),
                        new XAttribute("encoding", "Base64"),
                        info.MD5HashValueBase64
                    )
                )
            )
        );
    }

    private string EncryptAndEncodeAesKey(byte[] aesKey, string certificatePath)
    {
        using var cert = new X509Certificate2(certificatePath);
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
        string CompressedFileName,
        long ContentLength,
        long CompressedContentLength,
        byte[] SHA256HashValue,
        string MD5HashValueBase64
    );
}
