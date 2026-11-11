using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RafaoCluster.Services;

public sealed class LlamaInstallerService
{
    public const string CompatibleBuildTag = "b12110";
    private readonly HttpClient _http = new();

    public LlamaInstallerService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RafaoCluster", "0.1"));
    }

    public string? FindExecutable(string root, params string[] names)
    {
        var pinnedRoot = Path.Combine(root, "builds", CompatibleBuildTag);
        if (!Directory.Exists(pinnedRoot) ||
            !File.Exists(Path.Combine(pinnedRoot, "llama-build.txt"))) return null;
        return Directory.EnumerateFiles(pinnedRoot, "*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(path => names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
    }

    public async Task<string> InstallLatestCuda12Async(string root, Action<string> log)
    {
        var installRoot = Path.Combine(root, "builds", CompatibleBuildTag);
        Directory.CreateDirectory(installRoot);
        var json = await _http.GetStringAsync(
            $"https://api.github.com/repos/ggml-org/llama.cpp/releases/tags/{CompatibleBuildTag}");
        using var document = JsonDocument.Parse(json);
        var release = document.RootElement;
        var assets = release.GetProperty("assets").EnumerateArray().ToList();
        var binaries = assets.FirstOrDefault(asset =>
            asset.GetProperty("name").GetString() ==
            $"llama-{CompatibleBuildTag}-bin-win-cuda-12.4-x64.zip");
        var runtime = assets.FirstOrDefault(asset =>
            asset.GetProperty("name").GetString() == "cudart-llama-bin-win-cuda-12.4-x64.zip");
        if (binaries.ValueKind == JsonValueKind.Undefined || runtime.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException($"A release {CompatibleBuildTag} não contém o pacote CUDA 12.4 completo.");

        log($"Instalando build compatível fixada: {CompatibleBuildTag}");
        await DownloadAndExtract(binaries, installRoot, log);
        await DownloadAndExtract(runtime, installRoot, log);
        await File.WriteAllTextAsync(Path.Combine(installRoot, "llama-build.txt"), CompatibleBuildTag);
        return CompatibleBuildTag;
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
