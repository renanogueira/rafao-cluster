namespace RafaoCluster.Models;

public sealed class HardwareSnapshot
{
    public string ComputerName { get; init; } = Environment.MachineName;
    public string Windows { get; set; } = Environment.OSVersion.VersionString;
    public string Cpu { get; set; } = "Não identificado";
    public double RamGb { get; set; }
    public List<GpuInfo> Gpus { get; } = [];
    public List<NetworkInfo> Networks { get; } = [];
}

public sealed record GpuInfo(int Index, string Name, int TotalMiB, int FreeMiB, string Driver);
public sealed record NetworkInfo(string Name, string Address, long SpeedMbps);
