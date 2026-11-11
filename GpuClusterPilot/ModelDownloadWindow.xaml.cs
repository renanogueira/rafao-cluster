using RafaoCluster.Models;
using RafaoCluster.Services;
using System.Windows;

namespace RafaoCluster;

public partial class ModelDownloadWindow : Window
{
    private readonly HuggingFaceService _hub = new();
    private CancellationTokenSource? _downloadCancellation;
    public string? SelectedModelPath { get; private set; }

    public ModelDownloadWindow(string initialFolder)
    {
        InitializeComponent();
        DestinationInput.Text = initialFolder;
        Closed += (_, _) => _downloadCancellation?.Cancel();
    }

    private string Repository => RepositoryInput.Text.Trim();

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Consultando o Hugging Face...");
            var files = await _hub.ListGgufFilesAsync(Repository, TokenInput.Password);
            FileInput.ItemsSource = files;
            FileInput.IsEnabled = files.Count > 0;
            if (files.Count > 0) FileInput.SelectedIndex = 0;
            DownloadButton.IsEnabled = files.Count > 0;
            StatusText.Text = files.Count == 0
                ? "Nenhum arquivo .gguf foi encontrado nesse repositório."
                : $"{files.Count} arquivo(s) encontrado(s). Q4_K_M costuma equilibrar qualidade e memória.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SearchButton.IsEnabled = true; }
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var initial = Directory.Exists(DestinationInput.Text) ? DestinationInput.Text : @"C:\";
        var dialog = new DarkPathPickerWindow("Escolha a pasta dos modelos", initial, true) { Owner = this };
        if (dialog.ShowDialog() == true) DestinationInput.Text = dialog.SelectedPath;
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (FileInput.SelectedItem is not HuggingFaceFile file)
        {
            ShowError("Selecione um arquivo GGUF.");
            return;
        }

        _downloadCancellation = new CancellationTokenSource();
        try
        {
            SetBusy(true, $"Preparando {file.DisplayName}...");
            DownloadProgress.IsIndeterminate = true;
            var progress = new Progress<(long Downloaded, long? Total)>(value =>
            {
                var downloadedGb = value.Downloaded / 1024d / 1024d / 1024d;
                if (value.Total is > 0)
                {
                    DownloadProgress.IsIndeterminate = false;
                    DownloadProgress.Value = value.Downloaded * 100d / value.Total.Value;
                    StatusText.Text = $"Baixando {downloadedGb:0.00} de {value.Total.Value / 1024d / 1024d / 1024d:0.00} GB ({DownloadProgress.Value:0}%)";
                }
                else StatusText.Text = $"Baixando {downloadedGb:0.00} GB...";
            });
            SelectedModelPath = await _hub.DownloadAsync(
                Repository, file, DestinationInput.Text.Trim(), TokenInput.Password, progress, _downloadCancellation.Token);
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = 100;
            StatusText.Text = $"Concluído: {SelectedModelPath}";
            DialogResult = true;
        }
        catch (OperationCanceledException) { StatusText.Text = "Download cancelado."; }
        catch (Exception ex) { ShowError(ex.Message); }
        finally
        {
            _downloadCancellation = null;
            SearchButton.IsEnabled = true;
            DownloadButton.IsEnabled = FileInput.SelectedItem is HuggingFaceFile;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadCancellation is not null)
        {
            _downloadCancellation.Cancel();
            _downloadCancellation = null;
            StatusText.Text = "Cancelando download...";
            return;
        }
        DialogResult = false;
    }

    private void SetBusy(bool busy, string status)
    {
        SearchButton.IsEnabled = !busy;
        DownloadButton.IsEnabled = !busy;
        StatusText.Text = status;
    }

    private void ShowError(string message)
    {
        StatusText.Text = message;
        DarkDialogWindow.ShowMessage(this, "Download de modelo", message);
    }
}
