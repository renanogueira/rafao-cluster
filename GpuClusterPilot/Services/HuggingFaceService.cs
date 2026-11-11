using RafaoCluster.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RafaoCluster.Services;

public sealed class HuggingFaceService
{
    public static IReadOnlyList<GgufCatalogModel> FreeCatalog { get; } =
    [
        new("Gemma 3 1B Instruct", "ggml-org/gemma-3-1b-it-GGUF", "0,8 GB", "teste rápido", "Gemma"),
        new("Qwen3 4B", "Qwen/Qwen3-4B-GGUF", "2,5 GB", "PC modesto", "Apache 2.0"),
        new("Qwen2.5 7B Instruct", "Qwen/Qwen2.5-7B-Instruct-GGUF", "4,7 GB", "chat geral", "Apache 2.0"),
        new("DeepSeek R1 Distill Qwen 7B", "unsloth/DeepSeek-R1-Distill-Qwen-7B-GGUF", "4,7 GB", "raciocínio", "Apache 2.0"),
        new("Qwen3 8B", "Qwen/Qwen3-8B-GGUF", "5,0 GB", "chat e raciocínio", "Apache 2.0"),
        new("Qwen3 14B", "Qwen/Qwen3-14B-GGUF", "9 GB", "primeiro teste distribuído", "Apache 2.0"),
        new("Mistral Small 3.1 24B", "bartowski/mistralai_Mistral-Small-3.1-24B-Instruct-2503-GGUF", "15 GB", "cluster intermediário", "Apache 2.0"),
        new("Qwen3 32B", "Qwen/Qwen3-32B-GGUF", "20 GB", "cluster maior", "Apache 2.0")
    ];

    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public HuggingFaceService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RafaoCluster", "0.2"));
    }

    public async Task<IReadOnlyList<HuggingFaceFile>> ListGgufFilesAsync(
        string repository,
        string? token,
        CancellationToken cancellationToken = default)
    {
        ValidateRepository(repository);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://huggingface.co/api/models/{EncodePath(repository.Trim())}");
        AddToken(request, token);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Este repositório exige acesso e um token válido do Hugging Face.");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("siblings", out var siblings))
            return [];

        return siblings.EnumerateArray()
            .Where(item => item.TryGetProperty("rfilename", out var name) &&
                           (name.GetString()?.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(item => new HuggingFaceFile(item.GetProperty("rfilename").GetString()!))
            .OrderBy(file => QuantizationRank(file.Name))
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string> DownloadAsync(
        string repository,
        HuggingFaceFile file,
        string destinationFolder,
        string? token,
        IProgress<(long Downloaded, long? Total)> progress,
        CancellationToken cancellationToken = default)
    {
        ValidateRepository(repository);
        Directory.CreateDirectory(destinationFolder);
        var destination = Path.Combine(destinationFolder, Path.GetFileName(file.Name));
        var partial = destination + ".partial";
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://huggingface.co/{EncodePath(repository.Trim())}/resolve/main/{EncodePath(file.Name)}?download=true");
        AddToken(request, token);

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                throw new InvalidOperationException("Download bloqueado. Confirme o acesso ao modelo e o token.");
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            var buffer = new byte[1024 * 1024];
            long downloaded = 0;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloaded += read;
                progress.Report((downloaded, total));
            }
            await output.FlushAsync(cancellationToken);
            File.Move(partial, destination, true);
            return destination;
        }
        catch
        {
            try { if (File.Exists(partial)) File.Delete(partial); } catch { }
            throw;
        }
    }

    private static void ValidateRepository(string repository)
    {
        var parts = repository.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts.Any(part => part is "." or ".."))
            throw new InvalidOperationException("Use o formato autor/repositorio, por exemplo ggml-org/gemma-3-1b-it-GGUF.");
    }

    private static void AddToken(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
    }

    private static string EncodePath(string value) =>
        string.Join('/', value.Split('/').Select(Uri.EscapeDataString));

    private static int QuantizationRank(string name)
    {
        var upper = name.ToUpperInvariant();
        if (upper.Contains("Q4_K_M")) return 0;
        if (upper.Contains("Q5_K_M")) return 1;
        if (upper.Contains("Q4_K_S")) return 2;
        if (upper.Contains("Q6_K")) return 3;
        if (upper.Contains("Q8_0")) return 4;
        return 10;
    }
}
