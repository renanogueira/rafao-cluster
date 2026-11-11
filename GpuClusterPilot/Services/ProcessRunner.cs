using System.Diagnostics;
using System.Text;

namespace RafaoCluster.Services;

public sealed class ProcessRunner
{
    public async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        Action<string>? onLine = null,
        CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        var output = new StringBuilder();
        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);

        void Append(string? line)
        {
            if (line is null) return;
            lock (output) output.AppendLine(line);
            onLine?.Invoke(line);
        }

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, output.ToString());
    }

    public Process StartLongRunning(string fileName, IEnumerable<string> arguments, Action<string> onLine)
    {
        var info = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
}
