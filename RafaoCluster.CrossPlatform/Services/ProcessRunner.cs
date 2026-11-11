using System.Diagnostics;
namespace RafaoCluster.CrossPlatform.Services;
public sealed class ProcessRunner
{
 public async Task<(int ExitCode,string Output)> RunAsync(string file,IEnumerable<string> args) { var p=Process.Start(Create(file,args))??throw new InvalidOperationException($"Não foi possível iniciar {file}."); var o=p.StandardOutput.ReadToEndAsync(); var e=p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync(); return(p.ExitCode,(await o)+(await e)); }
 public Process Start(string file,IEnumerable<string> args,Action<string> log) { var p=new Process{StartInfo=Create(file,args),EnableRaisingEvents=true}; p.OutputDataReceived+=(_,e)=>{if(e.Data is not null)log(e.Data);}; p.ErrorDataReceived+=(_,e)=>{if(e.Data is not null)log(e.Data);}; p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();return p; }
 private static ProcessStartInfo Create(string f,IEnumerable<string>a){var i=new ProcessStartInfo(f){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(var x in a)i.ArgumentList.Add(x);return i;}
}
