using RafaoCluster.Models;
using RafaoCluster.Services;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;

namespace RafaoCluster;

public partial class MainWindow : Window
{
    private readonly ProcessRunner _runner = new();
    private readonly HardwareDiscoveryService _hardware;
    private readonly SettingsService _settingsService = new();
    private readonly LlamaInstallerService _installer = new();
    private readonly FirewallService _firewall = new();
    private ClusterSettings _settings = new();
    private Process? _activeProcess;
    private bool _isCoordinator = true;
    private CancellationTokenSource? _telemetryCancellation;
    private int _remoteGraphCount;
    private double _rpcAllocatedMiB;
    private static readonly Regex AllocationPattern = new(@"\[alloc_buffer\].*?size:\s*([0-9.]+)\s*(MiB|GiB)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DevicePattern = new(@"CUDA\d+:\s*([^\(]+)\s*\((\d+) MiB,\s*(\d+) MiB free", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MainWindow()
    {
        InitializeComponent();
        _hardware = new HardwareDiscoveryService(_runner);
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => StopActiveProcess();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();
        ApplyRole(_settings.Role != "Worker");
        CheckInstall();
        await ScanHardware();
    }

    private void CoordinatorRole_Click(object sender, RoutedEventArgs e) => ApplyRole(true);
    private void WorkerRole_Click(object sender, RoutedEventArgs e) => ApplyRole(false);

    private void ApplyRole(bool coordinator)
    {
        _isCoordinator = coordinator;
        CoordinatorRole.Style = coordinator ? (Style)FindResource("RoleSelectedButton") : (Style)FindResource("SecondaryButton");
        WorkerRole.Style = !coordinator ? (Style)FindResource("RoleSelectedButton") : (Style)FindResource("SecondaryButton");
        PageTitle.Text = coordinator ? "Configurar coordenador" : "Configurar worker";
        PageSubtitle.Text = coordinator
            ? "Este PC organiza as GPUs e expõe a API somente localmente."
            : "Este PC empresta sua GPU apenas ao coordenador autorizado.";
        PrimaryLabel.Text = coordinator ? "Workers (IP:porta, separados por vírgula)" : "IP dedicado deste worker";
        SecondaryLabel.Text = coordinator ? "Modelo GGUF" : "IP autorizado do coordenador";
        ThirdLabel.Text = coordinator ? "Porta da API" : "Dispositivo";
        FourthLabel.Text = coordinator ? "Contexto" : "Cache local";
        BrowseButton.Visibility = coordinator ? Visibility.Visible : Visibility.Collapsed;
        DownloadModelButton.Visibility = coordinator ? Visibility.Visible : Visibility.Collapsed;
        FirewallButton.Visibility = coordinator ? Visibility.Collapsed : Visibility.Visible;
        StartButton.Content = coordinator ? "Iniciar coordenador" : "Iniciar worker";

        if (coordinator)
        {
            PrimaryInput.Text = _settings.WorkerEndpoints;
            SecondaryInput.Text = _settings.ModelPath;
            ThirdInput.Text = _settings.ApiPort.ToString();
            FourthInput.Text = _settings.ContextSize.ToString();
        }
        else
        {
            PrimaryInput.Text = _settings.BindIp;
            SecondaryInput.Text = _settings.CoordinatorIp;
            ThirdInput.Text = "CUDA0";
            FourthInput.Text = _settings.EnableRpcCache ? "Ativado" : "Desativado";
        }
        RpcPortInput.Text = _settings.RpcPort.ToString();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await ScanHardware();

    private async Task ScanHardware()
    {
        HardwareSummary.Text = "Detectando hardware...";
        try
        {
            var h = await _hardware.ScanAsync();
            var text = new StringBuilder();
            text.Append($"{h.ComputerName} · {h.Windows} · {h.RamGb:0.0} GB RAM\n");
            text.Append(h.Gpus.Count == 0
                ? "NVIDIA: não detectada\n"
                : string.Join("\n", h.Gpus.Select(g => $"GPU {g.Index}: {g.Name} · {g.FreeMiB}/{g.TotalMiB} MiB livres · driver {g.Driver}")) + "\n");
            text.Append(h.Networks.Count == 0
                ? "Rede IPv4: não detectada"
                : string.Join(" · ", h.Networks.Select(n => $"{n.Name}: {n.Address} ({n.SpeedMbps} Mb/s)")));
            HardwareSummary.Text = text.ToString();
            if (!_isCoordinator && string.IsNullOrWhiteSpace(PrimaryInput.Text))
                PrimaryInput.Text = h.Networks.FirstOrDefault()?.Address ?? "";
        }
        catch (Exception ex) { HardwareSummary.Text = $"Falha no diagnóstico: {ex.Message}"; }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var initial = string.IsNullOrWhiteSpace(SecondaryInput.Text)
            ? Path.Combine(_settings.LlamaRoot, "models")
            : SecondaryInput.Text;
        var dialog = new DarkPathPickerWindow("Escolher modelo GGUF", initial, false, ".gguf") { Owner = this };
        if (dialog.ShowDialog() == true) SecondaryInput.Text = dialog.SelectedPath;
    }

    private void DownloadModel_Click(object sender, RoutedEventArgs e)
    {
        var modelsFolder = Path.Combine(_settings.LlamaRoot, "models");
        var dialog = new ModelDownloadWindow(modelsFolder) { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedModelPath))
        {
            SecondaryInput.Text = dialog.SelectedModelPath;
            Log($"Modelo selecionado: {dialog.SelectedModelPath}");
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_activeProcess is { HasExited: false })
                throw new InvalidOperationException("Pare o coordenador/worker antes de atualizar o llama.cpp.");
            SetBusy(true);
            Log("Instalando llama.cpp. Isto pode levar alguns minutos...");
            var tag = await _installer.InstallLatestCuda12Async(_settings.LlamaRoot, Log);
            Log($"Instalação concluída: {tag}");
            CheckInstall();
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false); }
    }

    private void CheckInstall()
    {
        var server = _installer.FindExecutable(_settings.LlamaRoot, "llama-server.exe");
        var rpc = _installer.FindExecutable(_settings.LlamaRoot, "ggml-rpc-server.exe", "rpc-server.exe");
        InstallStatus.Text = server is not null && rpc is not null ? "● llama.cpp instalado" : "○ llama.cpp não instalado";
        InstallStatus.Foreground = System.Windows.Media.Brushes.White;
    }

    private async void Firewall_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RpcPortInput.Text, out var port)) { ShowError("Porta RPC inválida."); return; }
        var answer = DarkDialogWindow.Confirm(this,
            "Confirmar firewall",
            $"Será criada uma regra privada para permitir TCP {port} em {PrimaryInput.Text}, somente a partir de {SecondaryInput.Text}. Continuar?");
        if (!answer) return;
        try
        {
            _firewall.AddWorkerRule(PrimaryInput.Text.Trim(), SecondaryInput.Text.Trim(), port);
            Log("Regra de firewall criada com escopo restrito.");
            await SaveSettings();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_activeProcess is { HasExited: false }) throw new InvalidOperationException("Já existe um processo em execução.");
            if (!int.TryParse(RpcPortInput.Text, out var rpcPort)) throw new InvalidOperationException("Porta RPC inválida.");

            string executable;
            List<string> args;
            if (_isCoordinator)
            {
                executable = _installer.FindExecutable(_settings.LlamaRoot, "llama-server.exe")
                             ?? throw new FileNotFoundException("Instale o llama.cpp primeiro.");
                if (!File.Exists(SecondaryInput.Text)) throw new FileNotFoundException("Selecione um modelo GGUF válido.");
                if (!int.TryParse(ThirdInput.Text, out var apiPort) || !int.TryParse(FourthInput.Text, out var context))
                    throw new InvalidOperationException("Porta da API ou contexto inválido.");
                var endpoints = PrimaryInput.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (endpoints.Length == 0) throw new InvalidOperationException("Informe pelo menos um worker.");
                await ValidateWorkers(endpoints);
                args = ["--model", SecondaryInput.Text, "--rpc", string.Join(',', endpoints), "--split-mode", "layer",
                    "--n-gpu-layers", "all", "--fit", "on", "--fit-target", "1536", "--ctx-size", context.ToString(),
                    "--parallel", "1", "--host", "127.0.0.1", "--port", apiPort.ToString()];
            }
            else
            {
                executable = _installer.FindExecutable(_settings.LlamaRoot, "ggml-rpc-server.exe", "rpc-server.exe")
                             ?? throw new FileNotFoundException("Instale o llama.cpp primeiro.");
                if (!System.Net.IPAddress.TryParse(PrimaryInput.Text, out _))
                    throw new InvalidOperationException("IP dedicado do worker inválido.");
                args = ["--host", PrimaryInput.Text.Trim(), "--port", rpcPort.ToString(), "--device", ThirdInput.Text.Trim()];
                if (FourthInput.Text.StartsWith("Ativ", StringComparison.OrdinalIgnoreCase)) args.Add("--cache");
            }

            ResetTelemetry();
            Log($"Iniciando {Path.GetFileName(executable)}...");
            IReadOnlyDictionary<string, string>? rpcDebug = _isCoordinator
                ? null
                : new Dictionary<string, string> { ["GGML_RPC_DEBUG"] = "1" };
            _activeProcess = _runner.StartLongRunning(executable, args, Log, rpcDebug);
            _activeProcess.EnableRaisingEvents = true;
            _activeProcess.Exited += (_, _) => Dispatcher.Invoke(() =>
            {
                Log("Processo encerrado.");
                StopTelemetry("○ Encerrado");
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            });
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            if (!_isCoordinator) StartGpuTelemetry();
            await SaveSettings();
        }
        catch (Exception ex)
        {
            StopTelemetry("● Erro");
            ShowError(ex);
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => StopActiveProcess();

    private void StopActiveProcess()
    {
        try
        {
            if (_activeProcess is { HasExited: false })
            {
                _activeProcess.Kill(true);
                _activeProcess.WaitForExit(5000);
            }
        }
        catch { }
        finally
        {
            StopTelemetry("○ Parado");
            _activeProcess?.Dispose();
            _activeProcess = null;
            if (IsLoaded)
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            }
        }
    }

    private async Task SaveSettings()
    {
        _settings.Role = _isCoordinator ? "Coordinator" : "Worker";
        _settings.RpcPort = int.TryParse(RpcPortInput.Text, out var rpc) ? rpc : 50052;
        if (_isCoordinator)
        {
            _settings.WorkerEndpoints = PrimaryInput.Text.Trim();
            _settings.ModelPath = SecondaryInput.Text.Trim();
            _settings.ApiPort = int.TryParse(ThirdInput.Text, out var api) ? api : 8080;
            _settings.ContextSize = int.TryParse(FourthInput.Text, out var ctx) ? ctx : 4096;
        }
        else
        {
            _settings.BindIp = PrimaryInput.Text.Trim();
            _settings.CoordinatorIp = SecondaryInput.Text.Trim();
            _settings.EnableRpcCache = FourthInput.Text.StartsWith("Ativ", StringComparison.OrdinalIgnoreCase);
        }
        await _settingsService.SaveAsync(_settings);
    }

    private async Task ValidateWorkers(IEnumerable<string> endpoints)
    {
        foreach (var endpoint in endpoints)
        {
            var parts = endpoint.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException($"Worker inválido: {endpoint}. Use IP:porta.");
            Log($"Testando worker {endpoint}...");
            using var client = new TcpClient();
            try
            {
                await client.ConnectAsync(parts[0], port).WaitAsync(TimeSpan.FromSeconds(3));
                Log($"Worker {endpoint} respondeu.");
            }
            catch
            {
                throw new InvalidOperationException($"Worker {endpoint} não respondeu. Confirme processo, IP e firewall.");
            }
        }
    }

    private void Log(string message) => Dispatcher.Invoke(() =>
    {
        UpdateRpcTelemetry(message);
        if (!message.Contains("[graph_compute]", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("[alloc_buffer]", StringComparison.OrdinalIgnoreCase))
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            LogBox.ScrollToEnd();
        }
    });

    private void ResetTelemetry()
    {
        _telemetryCancellation?.Cancel();
        _telemetryCancellation = new CancellationTokenSource();
        _remoteGraphCount = 0;
        _rpcAllocatedMiB = 0;
        RpcTelemetry.Text = _isCoordinator ? "◌ Iniciando servidor" : "● Ouvindo conexões";
        GpuTelemetry.Text = _isCoordinator ? "Workers RPC" : ThirdInput.Text.Trim();
        VramTelemetry.Text = "—";
        WorkTelemetry.Text = _isCoordinator ? "Carregando modelo" : "Aguardando coordenador";
    }

    private void StopTelemetry(string rpcStatus)
    {
        _telemetryCancellation?.Cancel();
        _telemetryCancellation = null;
        RpcTelemetry.Text = rpcStatus;
    }

    private void UpdateRpcTelemetry(string message)
    {
        if (message.Contains("listening on", StringComparison.OrdinalIgnoreCase))
            RpcTelemetry.Text = "● Ouvindo na rede";
        if (message.Contains("client connected", StringComparison.OrdinalIgnoreCase))
        {
            RpcTelemetry.Text = "● Coordenador conectado";
            WorkTelemetry.Text = "Conectado · aguardando cálculo";
        }

        var device = DevicePattern.Match(message);
        if (device.Success)
        {
            GpuTelemetry.Text = device.Groups[1].Value.Trim();
            VramTelemetry.Text = $"{device.Groups[3].Value}/{device.Groups[2].Value} MiB livres";
        }

        var allocation = AllocationPattern.Match(message);
        if (allocation.Success && double.TryParse(allocation.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
        {
            if (allocation.Groups[2].Value.Equals("GiB", StringComparison.OrdinalIgnoreCase)) size *= 1024;
            _rpcAllocatedMiB += size;
            WorkTelemetry.Text = $"{_rpcAllocatedMiB:0} MiB alocados via RPC";
        }

        if (message.Contains("[graph_compute]", StringComparison.OrdinalIgnoreCase))
        {
            _remoteGraphCount++;
            RpcTelemetry.Text = "● Coordenador conectado";
            WorkTelemetry.Text = $"● Calculando · {_remoteGraphCount} grafos";
        }
    }

    private void StartGpuTelemetry()
    {
        var cancellation = _telemetryCancellation ??= new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    var (_, output) = await _runner.RunAsync("nvidia-smi", [
                        "--query-gpu=index,name,memory.used,memory.total,utilization.gpu",
                        "--format=csv,noheader,nounits"
                    ], cancellationToken: cancellation.Token);
                    var selectedIndex = ThirdInput.Text.Trim().StartsWith("CUDA", StringComparison.OrdinalIgnoreCase)
                        ? ThirdInput.Text.Trim()[4..]
                        : "0";
                    var rows = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Split(',').Select(value => value.Trim()).ToArray())
                        .ToArray();
                    var fields = rows.FirstOrDefault(row => row.Length >= 5 && row[0] == selectedIndex) ?? rows.FirstOrDefault();
                    if (fields is { Length: >= 5 })
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            GpuTelemetry.Text = $"GPU {fields[0]} · {fields[1]}";
                            VramTelemetry.Text = $"{fields[2]}/{fields[3]} MiB · {fields[4]}%";
                        });
                    }
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    await Dispatcher.InvokeAsync(() => VramTelemetry.Text = "Telemetria indisponível");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(2), cancellation.Token); }
                catch (OperationCanceledException) { break; }
            }
        }, cancellation.Token);
    }

    private void SetBusy(bool busy)
    {
        StartButton.IsEnabled = !busy;
        CoordinatorRole.IsEnabled = !busy;
        WorkerRole.IsEnabled = !busy;
    }

    private void ShowError(Exception ex) => ShowError(ex.Message);
    private void ShowError(string message)
    {
        Log($"ERRO: {message}");
        DarkDialogWindow.ShowMessage(this, "Rafão Cluster", message);
    }
}
