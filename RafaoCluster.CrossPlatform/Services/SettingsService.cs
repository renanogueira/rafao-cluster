using RafaoCluster.CrossPlatform.Models; using System.Text.Json;
namespace RafaoCluster.CrossPlatform.Services;
public sealed class SettingsService(string root)
{
 private readonly string _file=Path.Combine(root,"settings.json");
 public async Task<ClusterSettings> LoadAsync(){try{if(File.Exists(_file))return JsonSerializer.Deserialize<ClusterSettings>(await File.ReadAllTextAsync(_file))??new();}catch{}return new();}
 public async Task SaveAsync(ClusterSettings v){Directory.CreateDirectory(Path.GetDirectoryName(_file)!);await File.WriteAllTextAsync(_file,JsonSerializer.Serialize(v,new JsonSerializerOptions{WriteIndented=true}));}
}
