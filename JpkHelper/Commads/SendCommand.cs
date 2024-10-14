using CommandLine;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace JpkHelper.Commads;

[Verb("send", HelpText = "Wysyła pliki do API jpk")]
internal class SendCommand
{
    [Option('p', "path-to-initUpload", HelpText = "Ścieżka do pliku init upload (nie folderu)", Required = true)]
    public required string Path { get; set; }
    [Option(shortName: 'e', longName: "enviroment", Default = EnvironmentType.Test)]
    public EnvironmentType Environment { get; set; }


    public async Task Execute()
    {
        var folderPath = System.IO.Path.GetDirectoryName(Path);
        var apiUrl = Environment switch
        {
            EnvironmentType.Test => new Uri("https://test-e-dokumenty.mf.gov.pl"),
            EnvironmentType.Production => new("https://e-dokumenty.mf.gov.pl"),
            _ => throw new ArgumentException(nameof(Environment))
        };

        var response = await InitUploadSigned(apiUrl, false, Path);
        Console.WriteLine($"Init upload OK. Reference number='{response.ReferenceNumber}'");
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        foreach (var item in response.RequestToUploadFileList)
        {
            Console.WriteLine($"Start upload blob {item.FileName} {item.BlobName} ");
            await UploadBlob(item, directory);
            Console.WriteLine($"Upload blob {item.FileName} {item.BlobName} OK");
        }
        await FinishUploadAsync(apiUrl, response.ReferenceNumber, response.RequestToUploadFileList.Select(f => f.BlobName).ToArray());
        Console.WriteLine("Finish upload OK");

    }


    static async Task<OkResponse> InitUploadSigned(Uri uri, bool verifySignature, string fileToUploadPath)
    {
        using var client = new HttpClient()
        {
            BaseAddress = uri,
        };
        var text = await File.ReadAllTextAsync(fileToUploadPath);
        using var content = new StringContent(text);
        var response = await client.PostAsync(
            new Uri($"api/storage/inituploadsigned?enableValidateQualifiedSignature={verifySignature}", UriKind.Relative),
            content
        );
        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<OkResponse>(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    static async Task UploadBlob(RequestToUploadFile request, string path)
    {
        using var client = new HttpClient();
        var pathToFile = System.IO.Path.Combine(path, request.FileName);
        using var stream = File.OpenRead(pathToFile);
        var message = new HttpRequestMessage()
        {
            Method = HttpMethod.Parse(request.Method),
            Content = new StreamContent(stream),
            RequestUri = request.Url
        };
        var md5 = HashHelpers.CalculateMD5(pathToFile);
        foreach (var header in request.HeaderList)
            if (header.Key == "Content-MD5")
                message.Content.Headers.ContentMD5 = Convert.FromBase64String(header.Value);
            else
                message.Headers.Add(header.Key, header.Value);
        var x = client.Send(message);
        if (!x.IsSuccessStatusCode)
            throw new Exception(await x.Content.ReadAsStringAsync());
    }

    static async Task FinishUploadAsync(Uri uri, string refernceNumber, string[] blobNameList)
    {
        using var client = new HttpClient()
        {
            BaseAddress = uri,
        };
        var body = new Dictionary<string, object>
        {
            ["ReferenceNumber"] = refernceNumber,
            ["AzureBlobNameList"] = blobNameList
        };
        var options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = null
        };
        var message = new HttpRequestMessage()
        {
            Content = new StringContent(JsonSerializer.Serialize(body, options).Replace(" ", "").Replace("\n", "").Replace("\r", ""), MediaTypeHeaderValue.Parse("application/json")),
            RequestUri = new Uri("api/storage/finishupload", UriKind.Relative),
            Method = HttpMethod.Post,
        };
        message.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
        message.Headers.TransferEncodingChunked = null;
        message.Headers.TransferEncoding.Clear();
        var response = await client.SendAsync(message);
        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());
        try
        {
            Console.WriteLine($"FinishUpload response: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
        catch (Exception) { }

    }

    private class OkResponse
    {
        public required string ReferenceNumber { get; set; }
        public required List<RequestToUploadFile> RequestToUploadFileList { get; set; }
    }

    private class RequestToUploadFile
    {
        public required string BlobName { get; set; }
        public required string FileName { get; set; }
        public required Uri Url { get; set; }
        public required string Method { get; set; }
        public required List<Header> HeaderList { get; set; }

    }

    private class Header
    {
        public required string Key { get; set; }
        public required string Value { get; set; }
    }

}
