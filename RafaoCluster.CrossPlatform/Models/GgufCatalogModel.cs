namespace RafaoCluster.CrossPlatform.Models;

public sealed record GgufCatalogModel(
    string Name,
    string Repository,
    string EstimatedQ4Size,
    string RecommendedFor,
    string License)
{
    public string DisplayName => $"{Name} · Q4 ≈ {EstimatedQ4Size} · {RecommendedFor} · {License}";
    public override string ToString() => DisplayName;
}
