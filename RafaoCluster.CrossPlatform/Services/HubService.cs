using RafaoCluster.CrossPlatform.Models; using System.Net.Http.Headers; using System.Text.Json;
namespace RafaoCluster.CrossPlatform.Services;
public sealed class HubService
{
 public static IReadOnlyList<GgufCatalogModel> FreeCatalog { get; } =
 [
  new("Gemma 3 1B Instruct","ggml-org/gemma-3-1b-it-GGUF","0,8 GB","teste rápido","Gemma"),
  new("Qwen3 4B","Qwen/Qwen3-4B-GGUF","2,5 GB","PC modesto","Apache 2.0"),
  new("Qwen2.5 7B Instruct","Qwen/Qwen2.5-7B-Instruct-GGUF","4,7 GB","chat geral","Apache 2.0"),
  new("DeepSeek R1 Distill Qwen 7B","unsloth/DeepSeek-R1-Distill-Qwen-7B-GGUF","4,7 GB","raciocínio","Apache 2.0"),
  new("Qwen3 8B","Qwen/Qwen3-8B-GGUF","5,0 GB","chat e raciocínio","Apache 2.0"),
  new("Qwen3 14B","Qwen/Qwen3-14B-GGUF","9 GB","primeiro teste distribuído","Apache 2.0"),
  new("Mistral Small 3.1 24B","bartowski/mistralai_Mistral-Small-3.1-24B-Instruct-2503-GGUF","15 GB","cluster intermediário","Apache 2.0"),
  new("Qwen3 32B","Qwen/Qwen3-32B-GGUF","20 GB","cluster maior","Apache 2.0")
 ];
 private readonly HttpClient _http=new(){Timeout=Timeout.InfiniteTimeSpan};
 public HubService()=>_http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RafaoCluster","0.4"));
 public async Task<List<HubFile>> ListAsync(string repo){using var s=await _http.GetStreamAsync($"https://huggingface.co/api/models/{repo}");using var j=await JsonDocument.ParseAsync(s);return j.RootElement.GetProperty("siblings").EnumerateArray().Select(x=>x.GetProperty("rfilename").GetString()!).Where(x=>x.EndsWith(".gguf",StringComparison.OrdinalIgnoreCase)).Select(x=>new HubFile(x)).OrderBy(x=>x.Name.Contains("Q4_K_M",StringComparison.OrdinalIgnoreCase)?0:1).ToList();}
 public async Task<string> DownloadAsync(string repo,HubFile file,string folder,IProgress<double> progress){Directory.CreateDirectory(folder);var dest=Path.Combine(folder,Path.GetFileName(file.Name));using var response=await _http.GetAsync($"https://huggingface.co/{repo}/resolve/main/{file.Name}?download=true",HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();var total=response.Content.Headers.ContentLength;await using var i=await response.Content.ReadAsStreamAsync();await using var o=File.Create(dest+".partial");var b=new byte[1024*1024];long done=0;int n;while((n=await i.ReadAsync(b))>0){await o.WriteAsync(b.AsMemory(0,n));done+=n;if(total>0)progress.Report(done*100d/total.Value);}o.Close();File.Move(dest+".partial",dest,true);return dest;}
}
