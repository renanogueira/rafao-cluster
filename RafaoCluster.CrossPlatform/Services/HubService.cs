using RafaoCluster.CrossPlatform.Models; using System.Net.Http.Headers; using System.Text.Json;
namespace RafaoCluster.CrossPlatform.Services;
public sealed class HubService
{
 private readonly HttpClient _http=new(){Timeout=Timeout.InfiniteTimeSpan};
 public HubService()=>_http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RafaoCluster","0.4"));
 public async Task<List<HubFile>> ListAsync(string repo){using var s=await _http.GetStreamAsync($"https://huggingface.co/api/models/{repo}");using var j=await JsonDocument.ParseAsync(s);return j.RootElement.GetProperty("siblings").EnumerateArray().Select(x=>x.GetProperty("rfilename").GetString()!).Where(x=>x.EndsWith(".gguf",StringComparison.OrdinalIgnoreCase)).Select(x=>new HubFile(x)).OrderBy(x=>x.Name.Contains("Q4_K_M",StringComparison.OrdinalIgnoreCase)?0:1).ToList();}
 public async Task<string> DownloadAsync(string repo,HubFile file,string folder,IProgress<double> progress){Directory.CreateDirectory(folder);var dest=Path.Combine(folder,Path.GetFileName(file.Name));using var response=await _http.GetAsync($"https://huggingface.co/{repo}/resolve/main/{file.Name}?download=true",HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();var total=response.Content.Headers.ContentLength;await using var i=await response.Content.ReadAsStreamAsync();await using var o=File.Create(dest+".partial");var b=new byte[1024*1024];long done=0;int n;while((n=await i.ReadAsync(b))>0){await o.WriteAsync(b.AsMemory(0,n));done+=n;if(total>0)progress.Report(done*100d/total.Value);}o.Close();File.Move(dest+".partial",dest,true);return dest;}
}
