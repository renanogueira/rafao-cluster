using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RafaoCluster.Services;

public sealed class LlamaInstallerService
{
    private readonly HttpClient _http = new();

    public LlamaInstallerService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RafaoCluster", "0.1"));
    }

    public string? FindExecutable(string root, params string[] names)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, "*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(path => names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
    }

    public async Task<string> InstallLatestCuda12Async(string root, Action<string> log)
    {
        Directory.CreateDirectory(root);
        var json = await _http.GetStringAsync("https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=20");
        using var document = JsonDocument.Parse(json);

        foreach (var release in document.RootElement.EnumerateArray())
        {
            var assets = release.GetProperty("assets").EnumerateArray().ToList();
            var binaries = assets.FirstOrDefault(a =>
                System.Text.RegularExpressions.Regex.IsMatch(a.GetProperty("name").GetString() ?? "",
                    @"^llama-b\d+-bin-win-cuda-12\.4-x64\.zip$"));
            var runtime = assets.FirstOrDefault(a =>
                a.GetProperty("name").GetString() == "cudart-llama-bin-win-cuda-12.4-x64.zip");
            if (binaries.ValueKind == JsonValueKind.Undefined || runtime.ValueKind == JsonValueKind.Undefined)
                continue;

            var tag = release.GetProperty("tag_name").GetString() ?? "desconhecida";
            log($"Release compatível encontrada: {tag}");
            await DownloadAndExtract(binaries, root, log);
            await DownloadAndExtract(runtime, root, log);
            return tag;
        }
        throw new InvalidOperationException("Nenhuma release completa CUDA 12.4 foi encontrada.");
    }

    private async Task DownloadAndExtract(JsonElement asset, string root, Action<string> log)
    {
        var name = asset.GetProperty("name").GetString()!;
        var url = asset.GetProperty("browser_download_url").GetString()!;
        var temp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{name}");
        log($"Baixando {name}...");
        await using (var input = await _http.GetStreamAsync(url))
        await using (var output = File.Create(temp))
            await input.CopyToAsync(output);
        log($"Extraindo {name}...");
        ZipFile.ExtractToDirectory(temp, root, true);
        File.Delete(temp);
    }
}
