namespace RafaoCluster.Models;

public sealed record HuggingFaceFile(string Name)
{
    public string DisplayName => Path.GetFileName(Name);
    public override string ToString() => DisplayName;
}
