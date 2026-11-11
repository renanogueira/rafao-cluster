using System.Diagnostics;

namespace RafaoCluster.Services;

public sealed class FirewallService
{
    public void AddWorkerRule(string bindIp, string coordinatorIp, int port)
    {
        if (!System.Net.IPAddress.TryParse(bindIp, out _) || !System.Net.IPAddress.TryParse(coordinatorIp, out _))
            throw new InvalidOperationException("Informe IPs válidos antes de configurar o firewall.");

        var command = $"New-NetFirewallRule -DisplayName 'RafaoCluster RPC' " +
                      $"-Direction Inbound -Action Allow -Protocol TCP -LocalPort {port} " +
                      $"-LocalAddress '{bindIp}' -RemoteAddress '{coordinatorIp}' -Profile Private";
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\""
        };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Não foi possível abrir a confirmação administrativa.");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("A configuração do firewall foi cancelada ou falhou.");
    }
}
