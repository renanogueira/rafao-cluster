using RafaoCluster.Models;
using System.Text.Json;

namespace RafaoCluster.Services;

public sealed class SettingsService
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RafaoCluster");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public async Task<ClusterSettings> LoadAsync()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<ClusterSettings>(await File.ReadAllTextAsync(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public async Task SaveAsync(ClusterSettings settings)
    {
        Directory.CreateDirectory(Folder);
        await File.WriteAllTextAsync(FilePath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
