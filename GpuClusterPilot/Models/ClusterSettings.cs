namespace RafaoCluster.Models;

public sealed class ClusterSettings
{
    public string Role { get; set; } = "Coordinator";
    public string BindIp { get; set; } = "";
    public string CoordinatorIp { get; set; } = "";
    public int RpcPort { get; set; } = 50052;
    public int ApiPort { get; set; } = 8080;
    public string WorkerEndpoints { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public int ContextSize { get; set; } = 4096;
    public bool EnableRpcCache { get; set; } = true;
    public string LlamaRoot { get; set; } = @"C:\llama";
}
