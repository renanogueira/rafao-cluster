using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RafaoCluster.CrossPlatform.Services;

public sealed class NetworkDiscoveryService
{
    public async Task<IReadOnlyList<string>> FindWorkersAsync(int port, CancellationToken cancellationToken = default)
    {
        var localAddresses = GetLocalAddresses();
        var candidates = localAddresses
            .SelectMany(GetScanAddresses)
            .Where(address => !localAddresses.Contains(address))
            .Distinct()
            .ToArray();
        var found = new ConcurrentBag<IPAddress>();

        await Parallel.ForEachAsync(candidates, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 64
        }, async (address, token) =>
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                await client.ConnectAsync(address, port, token).AsTask().WaitAsync(TimeSpan.FromMilliseconds(450), token);
                found.Add(address);
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException)
            {
                if (ex is OperationCanceledException && token.IsCancellationRequested) throw;
            }
        });

        return found
            .Distinct()
            .OrderBy(ToUInt32)
            .Select(address => $"{address}:{port}")
            .ToArray();
    }

    private static HashSet<IPAddress> GetLocalAddresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                          network.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
        .SelectMany(network => network.GetIPProperties().UnicastAddresses)
        .Where(info => info.Address.AddressFamily == AddressFamily.InterNetwork && IsPrivate(info.Address))
        .Select(info => info.Address)
        .ToHashSet();

    private static IEnumerable<IPAddress> GetScanAddresses(IPAddress localAddress)
    {
        var bytes = localAddress.GetAddressBytes();
        for (var host = 1; host < 255; host++)
            yield return new IPAddress([bytes[0], bytes[1], bytes[2], (byte)host]);
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
               bytes[0] == 192 && bytes[1] == 168;
    }

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }
}
