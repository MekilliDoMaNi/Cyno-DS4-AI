using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using OpenDS4AI.Core;
namespace OpenDS4AI.App;
internal sealed record UpdatePreferences(string FeedUrl="",bool AutoCheck=true);
internal sealed record UpdatePlan(string Manifest,string PayloadPath,string TargetPath,int ParentId,long ParentStarted);
internal static class AppUpdates
{
    internal static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cyno DS4.AI","Updates");
    internal static Version Current=>typeof(App).Assembly.GetName().Version!;
    internal static string PublicKey{get{using var s=typeof(App).Assembly.GetManifestResourceStream("CynoDS4AI.UpdatePublicKey")!;using var r=new StreamReader(s);return r.ReadToEnd();}}
    internal static string PreferencesPath=>Path.Combine(Root,"preferences.json");
    internal static UpdatePreferences Load(){string p=File.Exists(PreferencesPath)?PreferencesPath:Path.Combine(AppContext.BaseDirectory,"updates.json");return File.Exists(p)?JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllText(p))??new():new();}
    internal static void Save(UpdatePreferences p){UpdateProtocol.Https(p.FeedUrl);Directory.CreateDirectory(Root);File.WriteAllText(PreferencesPath,JsonSerializer.Serialize(p));}
    internal static HttpClient Client(){var c=new HttpClient{Timeout=TimeSpan.FromMinutes(10)};c.DefaultRequestHeaders.UserAgent.ParseAdd("CynoDS4AI/"+Current.ToString(3));return c;}
    internal static async Task Download(HttpClient client,string url,string output,long limit,long? expected=null)
    {
        using var response=await client.GetAsync(UpdateProtocol.Https(url),HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();UpdateProtocol.Https(response.RequestMessage!.RequestUri!.AbsoluteUri);
        if(response.Content.Headers.ContentLength is long len&&(len>limit||(expected.HasValue&&len!=expected)))throw new InvalidDataException("Dimensione download non valida.");
        await using var input=await response.Content.ReadAsStreamAsync();await using var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
        byte[] buffer=new byte[81920];long total=0;int read;using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        while((read=await input.ReadAsync(buffer,timeout.Token))!=0){total+=read;if(total>limit)throw new InvalidDataException("Download oltre il limite.");await file.WriteAsync(buffer.AsMemory(0,read),timeout.Token);}
        if(expected.HasValue&&total!=expected)throw new InvalidDataException("Download incompleto.");
    }
    internal static async Task Apply(string planPath)
    {
        planPath=Path.GetFullPath(planPath);if(!planPath.StartsWith(Path.GetFullPath(Root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Piano non valido.");
        var p=JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(planPath))??throw new InvalidDataException("Piano vuoto.");
        string folder=Path.GetDirectoryName(planPath)!,payload=Path.GetFullPath(p.PayloadPath),target=Path.GetFullPath(p.TargetPath);
        if(Path.GetDirectoryName(payload)!=folder||Path.GetDirectoryName(Environment.ProcessPath!)!=folder||Path.GetFileName(target)!="Cyno DS4.AI.exe"||!File.Exists(target))throw new InvalidDataException("Percorso non valido.");
        var release=UpdateProtocol.Verify(p.Manifest,PublicKey);UpdateProtocol.VerifyFile(payload,release);
        var old=Version.Parse(FileVersionInfo.GetVersionInfo(target).ProductVersion!.Split('+')[0]);if(!UpdateProtocol.IsNewer(release,old))throw new InvalidDataException("Versione precedente o già installata.");
        Process? parent=null;try{parent=Process.GetProcessById(p.ParentId);}catch(ArgumentException){}
        if(parent!=null){using(parent){if(parent.StartTime.ToUniversalTime().Ticks!=p.ParentStarted||!string.Equals(parent.MainModule!.FileName,target,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Processo origine non valido.");using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));await parent.WaitForExitAsync(timeout.Token);}}
        UpdateProtocol.VerifyFile(payload,release);string temp=target+".updating",backup=target+".previous";File.Copy(payload,temp,true);bool replaced=false;
        try{File.Replace(temp,target,backup,true);replaced=true;Process.Start(new ProcessStartInfo(target){WorkingDirectory=Path.GetDirectoryName(target)!,UseShellExecute=true});File.WriteAllText(Path.Combine(Root,"last-result.txt"),"Installata versione "+release.Version);}
        catch{if(replaced&&File.Exists(backup)){File.Copy(backup,target,true);Process.Start(new ProcessStartInfo(target){WorkingDirectory=Path.GetDirectoryName(target)!,UseShellExecute=true});}throw;}
    }
}
public partial class MainWindow
{
    private bool updateBusy;
    private readonly DispatcherTimer updateTimer=new(){Interval=TimeSpan.FromHours(6)};
    private void InitializeUpdates()
    {
        try{var p=AppUpdates.Load();UpdateFeed.Text=p.FeedUrl;AutomaticUpdates.IsChecked=p.AutoCheck;}catch(Exception ex){UpdateStatus.Text=ex.Message;}
        updateTimer.Tick+=async(_,_)=>{if(AutomaticUpdates.IsChecked==true)await CheckUpdate(false);};Loaded+=async(_,_)=>{updateTimer.Start();if(AutomaticUpdates.IsChecked==true)await CheckUpdate(false);};Closed+=(_,_)=>updateTimer.Stop();
    }
    private void SaveUpdates_Click(object sender,RoutedEventArgs e)
    {
        try{string url=UpdateFeed.Text.Trim();if(!url.EndsWith(".json",StringComparison.OrdinalIgnoreCase))url=UpdateProtocol.GitHubFeed(url);AppUpdates.Save(new(url,AutomaticUpdates.IsChecked==true));UpdateFeed.Text=url;UpdateStatus.Text="Indirizzo salvato.";}catch(Exception ex){UpdateStatus.Text=ex.Message;}
    }
    private async void CheckUpdates_Click(object sender,RoutedEventArgs e)=>await CheckUpdate(true);
    private async Task CheckUpdate(bool manual)
    {
        if(updateBusy||closing)return;if(string.IsNullOrWhiteSpace(UpdateFeed.Text)){UpdateStatus.Text="Inserisci il repository GitHub e salva.";return;}
        updateBusy=true;CheckUpdateButton.IsEnabled=false;
        try
        {
            string url=UpdateFeed.Text.Trim();if(!url.EndsWith(".json",StringComparison.OrdinalIgnoreCase))url=UpdateProtocol.GitHubFeed(url);UpdateProtocol.Https(url);
            string folder=Path.Combine(AppUpdates.Root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);using var client=AppUpdates.Client();UpdateStatus.Text="Controllo aggiornamenti…";
            string mf=Path.Combine(folder,"update.json");await AppUpdates.Download(client,url,mf,65536);string manifest=await File.ReadAllTextAsync(mf);var release=UpdateProtocol.Verify(manifest,AppUpdates.PublicKey);
            if(!UpdateProtocol.IsNewer(release,AppUpdates.Current)){UpdateStatus.Text="Versione già aggiornata: "+AppUpdates.Current.ToString(3);return;}
            UpdateStatus.Text="Disponibile versione "+release.Version;
            if(closing||MessageBox.Show(this,$"Disponibile Cyno {release.Version}.\n{release.Notes}\n\nScaricare e installare? L'app verrà riavviata e il controller scollegato durante l'aggiornamento.","Aggiornamento Cyno",MessageBoxButton.YesNo,MessageBoxImage.Information)!=MessageBoxResult.Yes)return;
            UpdateStatus.Text="Download aggiornamento…";string payload=Path.Combine(folder,"new.exe");await AppUpdates.Download(client,release.Url,payload,UpdateProtocol.MaxSize,release.Size);UpdateProtocol.VerifyFile(payload,release);if(closing)return;
            string current=Environment.ProcessPath??throw new IOException("Percorso app non disponibile.");if(Path.GetFileName(current)!="Cyno DS4.AI.exe")throw new IOException("Avvia l'eseguibile distribuito per aggiornare.");
            string helper=Path.Combine(folder,"CynoUpdater.exe");File.Copy(current,helper);using var process=Process.GetCurrentProcess();string plan=Path.Combine(folder,"plan.json");
            await File.WriteAllTextAsync(plan,JsonSerializer.Serialize(new UpdatePlan(manifest,payload,current,Environment.ProcessId,process.StartTime.ToUniversalTime().Ticks)));
            SaveAssistance();var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder};start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(plan);
            if(Process.Start(start)==null)throw new IOException("Avvio aggiornamento non riuscito.");UpdateStatus.Text="Installazione e riavvio…";Close();
        }
        catch(Exception ex){UpdateStatus.Text="Aggiornamento non installato: "+ex.Message;if(manual)Log("UPDATE: "+ex.Message);}
        finally{updateBusy=false;CheckUpdateButton.IsEnabled=true;}
    }
}