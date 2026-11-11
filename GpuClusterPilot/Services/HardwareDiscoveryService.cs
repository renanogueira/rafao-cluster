using Microsoft.Win32;
using RafaoCluster.Models;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace RafaoCluster.Services;

public sealed class HardwareDiscoveryService(ProcessRunner runner)
{
    public async Task<HardwareSnapshot> ScanAsync()
    {
        var snapshot = new HardwareSnapshot();

        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            var name = key?.GetValue("ProductName")?.ToString();
            var build = key?.GetValue("CurrentBuildNumber")?.ToString();
            if (!string.IsNullOrWhiteSpace(name)) snapshot.Windows = $"{name} (build {build})";
        }

        try
        {
            using var cpuKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            snapshot.Cpu = cpuKey?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Não identificado";
            var memory = new MemoryStatusEx();
            if (GlobalMemoryStatusEx(memory))
                snapshot.RamGb = Math.Round(memory.TotalPhysical / 1024d / 1024d / 1024d, 1);
        }
        catch { }

        try
        {
            var (_, output) = await runner.RunAsync("nvidia-smi", [
                "--query-gpu=index,name,memory.total,memory.free,driver_version",
                "--format=csv,noheader,nounits"
            ]);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split(',').Select(x => x.Trim()).ToArray();
                if (p.Length >= 5 && int.TryParse(p[0], out var index) &&
                    int.TryParse(p[2], out var total) && int.TryParse(p[3], out var free))
                    snapshot.Gpus.Add(new GpuInfo(index, p[1], total, free, p[4]));
            }
        }
        catch { }

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                 n.NetworkInterfaceType is not NetworkInterfaceType.Loopback))
        {
            foreach (var ip in nic.GetIPProperties().UnicastAddresses
                         .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork &&
                                     !a.Address.ToString().StartsWith("169.254.")))
            {
                snapshot.Networks.Add(new NetworkInfo(nic.Name, ip.Address.ToString(), nic.Speed / 1_000_000));
            }
        }
        return snapshot;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
}
