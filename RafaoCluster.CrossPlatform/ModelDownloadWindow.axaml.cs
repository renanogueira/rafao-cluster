using Avalonia.Controls; using Avalonia.Interactivity; using RafaoCluster.CrossPlatform.Models; using RafaoCluster.CrossPlatform.Services;
namespace RafaoCluster.CrossPlatform;
public partial class ModelDownloadWindow:Window
{
 private readonly HubService _hub=new(); private readonly string _folder; public string? SelectedPath{get;private set;}
 public ModelDownloadWindow():this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RafaoCluster","models")){}
 public ModelDownloadWindow(string folder){InitializeComponent();_folder=folder;FolderText.Text="Destino: "+folder;}
 private async void Search_Click(object? s,RoutedEventArgs e){try{StatusText.Text="Consultando...";var f=await _hub.ListAsync(RepoInput.Text??"");FileInput.ItemsSource=f;FileInput.SelectedIndex=f.Count>0?0:-1;DownloadAction.IsEnabled=f.Count>0;StatusText.Text=$"{f.Count} arquivo(s) encontrado(s).";}catch(Exception x){StatusText.Text=x.Message;}}
 private async void Download_Click(object? s,RoutedEventArgs e){if(FileInput.SelectedItem is not HubFile f)return;try{DownloadAction.IsEnabled=false;var p=new Progress<double>(v=>{Progress.Value=v;StatusText.Text=$"Baixando {v:0}%";});SelectedPath=await _hub.DownloadAsync(RepoInput.Text??"",f,_folder,p);Close(true);}catch(Exception x){StatusText.Text=x.Message;DownloadAction.IsEnabled=true;}}
 private void Cancel_Click(object? s,RoutedEventArgs e)=>Close(false);
}
