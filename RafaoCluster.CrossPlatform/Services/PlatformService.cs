using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RafaoCluster.CrossPlatform.Services;

public sealed class PlatformService(ProcessRunner runner)
{
    public string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RafaoCluster");

    public string LlamaRoot => Path.Combine(DataRoot, "llama");
    public string ModelsRoot => Path.Combine(DataRoot, "models");
    private string MacBuildVersionFile => Path.Combine(LlamaRoot, "mac-rpc-version.txt");
    public bool IsMac => OperatingSystem.IsMacOS();
    public bool IsLinux => OperatingSystem.IsLinux();

    public bool CoordinatorInstalled => IsMac
        ? FindLocalBinary("llama-server") is not null && File.Exists(MacBuildVersionFile)
        : Find("llama-server") is not null;

    public bool WorkerInstalled =>
        FindLocalBinary("ggml-rpc-server") is not null ||
        (!IsMac && Find("rpc-server") is not null);

    public string PlatformName => IsMac
        ? $"macOS {RuntimeInformation.OSArchitecture}"
        : $"Linux {RuntimeInformation.OSArchitecture}";

    public string? Find(string name)
    {
        var local = FindLocalBinary(name);
        if (local is not null)
        {
            return local;
        }

        var searchDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Apps iniciados pelo Finder normalmente não herdam /opt/homebrew/bin.
        if (IsMac)
        {
            searchDirectories.AddRange([
                "/opt/homebrew/bin",
                "/opt/homebrew/opt/llama.cpp/bin",
                "/usr/local/bin",
                "/usr/local/opt/llama.cpp/bin"
            ]);
        }

        return searchDirectories
            .Distinct(StringComparer.Ordinal)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
    }

    private string? FindLocalBinary(string name) => Directory.Exists(LlamaRoot)
        ? Directory.EnumerateFiles(LlamaRoot, name, SearchOption.AllDirectories).FirstOrDefault()
        : null;

    public async Task<string> DiagnoseAsync()
    {
        var lines = new List<string> { $"{Environment.MachineName} · {PlatformName}" };
        try
        {
            if (IsMac)
            {
                var (code, cpu) = await runner.RunAsync("sysctl", ["-n", "machdep.cpu.brand_string"]);
                if (code != 0 || string.IsNullOrWhiteSpace(cpu))
                {
                    (_, cpu) = await runner.RunAsync("sysctl", ["-n", "hw.model"]);
                }

                var (_, memory) = await runner.RunAsync("sysctl", ["-n", "hw.memsize"]);
                lines.Add($"{cpu.Trim()} · {double.Parse(memory.Trim()) / 1073741824d:0.0} GB de memória unificada");
            }
            else
            {
                var memory = File.ReadLines("/proc/meminfo")
                    .FirstOrDefault(line => line.StartsWith("MemTotal"));
                lines.Add(memory ?? "RAM não identificada");
                var (_, gpu) = await runner.RunAsync("nvidia-smi", [
                    "--query-gpu=index,name,memory.total,memory.free,driver_version",
                    "--format=csv,noheader"
                ]);
                lines.Add(gpu.Trim());
            }
        }
        catch (Exception exception)
        {
            lines.Add("GPU/CPU: " + exception.Message);
        }

        foreach (var network in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(item => item.OperationalStatus == OperationalStatus.Up))
        {
            foreach (var address in network.GetIPProperties().UnicastAddresses.Where(item =>
                         item.Address.AddressFamily == AddressFamily.InterNetwork &&
                         !item.Address.ToString().StartsWith("127.")))
            {
                lines.Add($"{network.Name}: {address.Address} ({network.Speed / 1_000_000} Mb/s)");
            }
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    public async Task InstallAsync(Action<string> log)
    {
        Directory.CreateDirectory(LlamaRoot);
        if (IsMac)
        {
            await InstallMacAsync(log);
            return;
        }

        await InstallLinuxRelease(log);
    }

    private async Task InstallMacAsync(Action<string> log)
    {
        var brew = File.Exists("/opt/homebrew/bin/brew")
            ? "/opt/homebrew/bin/brew"
            : "/usr/local/bin/brew";

        if (!File.Exists(brew))
        {
            throw new InvalidOperationException("Homebrew não encontrado. Instale em brew.sh e tente novamente.");
        }

        log("Instalando llama.cpp pelo Homebrew...");
        var (exitCode, output) = await runner.RunAsync(brew, ["install", "llama.cpp"]);
        log(output);
        if (exitCode != 0)
        {
            throw new InvalidOperationException("O Homebrew não conseguiu instalar llama.cpp.");
        }

        if (Find("llama-server") is null)
        {
            throw new FileNotFoundException("O Homebrew terminou, mas llama-server não foi encontrado.");
        }

        var (versionExitCode, versionOutput) = await runner.RunAsync(
            brew,
            ["list", "--versions", "llama.cpp"]);
        var version = versionOutput
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .FirstOrDefault();
        if (versionExitCode != 0 || string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("Não foi possível identificar a versão do llama.cpp instalada pelo Homebrew.");
        }

        var versionTag = "v" + version;
        var installedVersion = File.Exists(MacBuildVersionFile)
            ? (await File.ReadAllTextAsync(MacBuildVersionFile)).Trim()
            : string.Empty;
        if (FindLocalBinary("llama-server") is not null &&
            FindLocalBinary("ggml-rpc-server") is not null &&
            installedVersion == versionTag)
        {
            log($"Backend RPC com Metal {versionTag} já está instalado.");
            return;
        }

        await InstallMacRpcAsync(brew, versionTag, log);
    }

    private async Task InstallMacRpcAsync(string brew, string versionTag, Action<string> log)
    {
        log("O worker RPC não faz parte da fórmula Homebrew; preparando a compilação com Metal...");
        var (dependencyExitCode, dependencyOutput) = await runner.RunAsync(brew, ["install", "cmake", "git"]);
        log(dependencyOutput);
        if (dependencyExitCode != 0)
        {
            throw new InvalidOperationException("Não foi possível instalar CMake e Git pelo Homebrew.");
        }

        var brewPrefix = Path.GetDirectoryName(brew)!;
        var git = Path.Combine(brewPrefix, "git");
        var cmake = Path.Combine(brewPrefix, "cmake");
        var source = Path.Combine(DataRoot, "llama.cpp-rpc-source");
        var build = Path.Combine(DataRoot, "llama.cpp-rpc-build");

        if (Directory.Exists(Path.Combine(source, ".git")))
        {
            log($"Preparando o código-fonte compatível ({versionTag})...");
            var (fetchExitCode, fetchOutput) = await runner.RunAsync(git, [
                "-C", source, "fetch", "--depth", "1", "origin", "tag", versionTag, "--force"
            ]);
            log(fetchOutput);
            if (fetchExitCode != 0)
            {
                throw new InvalidOperationException($"Não foi possível baixar o código-fonte {versionTag} do llama.cpp.");
            }

            var (checkoutExitCode, checkoutOutput) = await runner.RunAsync(git, [
                "-C", source, "checkout", "--force", "FETCH_HEAD"
            ]);
            log(checkoutOutput);
            if (checkoutExitCode != 0)
            {
                throw new InvalidOperationException($"Não foi possível selecionar o código-fonte {versionTag}.");
            }
        }
        else
        {
            if (Directory.Exists(source))
            {
                throw new InvalidOperationException($"A pasta de fontes está incompleta: {source}");
            }

            log("Baixando o código-fonte oficial do llama.cpp...");
            var (cloneExitCode, cloneOutput) = await runner.RunAsync(git, [
                "clone", "--depth", "1", "--branch", versionTag,
                "https://github.com/ggml-org/llama.cpp.git", source
            ]);
            log(cloneOutput);
            if (cloneExitCode != 0)
            {
                throw new InvalidOperationException("Não foi possível baixar o código-fonte do llama.cpp.");
            }
        }

        Directory.CreateDirectory(build);
        var binaryDirectory = Path.Combine(LlamaRoot, "bin");
        Directory.CreateDirectory(binaryDirectory);

        log("Configurando o worker RPC com Metal...");
        var (configureExitCode, configureOutput) = await runner.RunAsync(cmake, [
            "-S", source,
            "-B", build,
            "-DGGML_METAL=ON",
            "-DGGML_RPC=ON",
            "-DCMAKE_BUILD_TYPE=Release",
            $"-DCMAKE_RUNTIME_OUTPUT_DIRECTORY={binaryDirectory}",
            $"-DCMAKE_LIBRARY_OUTPUT_DIRECTORY={binaryDirectory}"
        ]);
        log(configureOutput);
        if (configureExitCode != 0)
        {
            throw new InvalidOperationException("O CMake não conseguiu configurar o worker RPC.");
        }

        log("Compilando llama-server e ggml-rpc-server com suporte RPC; isso pode levar alguns minutos...");
        var (buildExitCode, buildOutput) = await runner.RunAsync(cmake, [
            "--build", build,
            "--config", "Release",
            "--target", "llama-server", "ggml-rpc-server",
            "--parallel", Math.Max(2, Environment.ProcessorCount).ToString()
        ]);
        log(buildOutput);
        if (buildExitCode != 0 ||
            FindLocalBinary("llama-server") is null ||
            FindLocalBinary("ggml-rpc-server") is null)
        {
            throw new InvalidOperationException("A compilação do backend RPC não foi concluída.");
        }

        await File.WriteAllTextAsync(MacBuildVersionFile, versionTag);
        log("llama-server e worker RPC com Metal instalados.");
    }

    private async Task InstallLinuxRelease(Action<string> log)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RafaoCluster", "0.4.1"));
        var json = await http.GetStringAsync("https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=20");
        using var document = JsonDocument.Parse(json);

        foreach (var release in document.RootElement.EnumerateArray())
        {
            var assets = release.GetProperty("assets").EnumerateArray().ToList();
            var cuda = Find("nvidia-smi") is not null;
            var asset = assets.FirstOrDefault(item =>
            {
                var name = item.GetProperty("name").GetString() ?? string.Empty;
                return cuda
                    ? name.Contains("bin-ubuntu-cuda-12") && name.EndsWith("-x64.tar.gz")
                    : name.Contains("bin-ubuntu-x64.tar.gz");
            });

            if (asset.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            await Extract(http, asset, log);
            if (cuda)
            {
                var runtime = assets.FirstOrDefault(item =>
                {
                    var name = item.GetProperty("name").GetString() ?? string.Empty;
                    return name.Contains("cudart-") &&
                           name.Contains("ubuntu-cuda-12") &&
                           name.EndsWith("-x64.tar.gz");
                });
                if (runtime.ValueKind != JsonValueKind.Undefined)
                {
                    await Extract(http, runtime, log);
                }
            }

            return;
        }

        throw new InvalidOperationException("Release Linux compatível não encontrada.");
    }

    private async Task Extract(HttpClient http, JsonElement asset, Action<string> log)
    {
        var name = asset.GetProperty("name").GetString()!;
        log("Baixando " + name);
        await using var input = await http.GetStreamAsync(asset.GetProperty("browser_download_url").GetString());
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, LlamaRoot, true);
    }
}
