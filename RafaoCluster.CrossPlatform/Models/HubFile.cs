namespace RafaoCluster.CrossPlatform.Models;
public sealed record HubFile(string Name) { public string DisplayName => Path.GetFileName(Name); public override string ToString() => DisplayName; }
