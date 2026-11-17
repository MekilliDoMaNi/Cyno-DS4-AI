using System.IO;
using System.Windows;
using System.Windows.Threading;
using OpenDS4AI.Core;
using OpenDS4AI.Windows;
namespace OpenDS4AI.App;
public partial class MainWindow : Window
{
    private readonly string profilePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cyno DS4.AI","profile.json");
    private readonly string logPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cyno DS4.AI","session.log");
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(33)};
    private readonly DispatcherTimer reconnectTimer=new(){Interval=TimeSpan.FromSeconds(3)};
    private CancellationTokenSource? cancellation;
    private Task? worker,stopTask;
    private IVirtualController? output;
    private PadState latest;
    private readonly object gate=new();
    private volatile Profile activeProfile=new();
    private bool closing,demo,stopping,wantConnection;
    private long reports;private float aimCorrectionX,aimCorrectionY;
    public MainWindow()
    {
        InitializeComponent();MigrateLegacySettings();InitializeSessionTools();InitializeAssistance();InitializeUpdates();
        if(!File.Exists(SettingsPath)) {try{SetProfile(ProfileStore.Load(profilePath));}catch(Exception ex){Status.Text="Profilo legacy non caricato: "+ex.Message;}}
        activeProfile=CurrentProfile();
        timer.Tick+=Tick;Closing+=OnClosing;SourceInitialized+=(_,_)=> {ExcludeOwnWindow();RegisterHotkeys();};
        DeadZone.ValueChanged+=(_,_)=>activeProfile=CurrentProfile();
        Invert.Checked+=(_,_)=>activeProfile=CurrentProfile();Invert.Unchecked+=(_,_)=>activeProfile=CurrentProfile();
        reconnectTimer.Tick+=(_,_)=> {if(wantConnection && Reconnect.IsChecked==true && cancellation==null && !stopping)Start_Click(this,new());};
        Loaded+=(_,_)=> {Demo.IsChecked=false;Virtual.IsChecked=true;reconnectTimer.Start();Start_Click(this,new());};
    }
    private void Log(string message){try{Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);File.AppendAllText(logPath,$"{DateTimeOffset.Now:O} {message}\n");}catch(IOException){}catch(UnauthorizedAccessException){}}
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        try {if(string.IsNullOrWhiteSpace(ProfileName.Text))throw new ArgumentException("Inserisci un nome per il profilo.");SaveAssistance();SaveNamedCurrent();Status.Text="Profilo salvato: "+CurrentProfile().Name;}catch(Exception ex){Status.Text="Salvataggio fallito: "+ex.Message;}
    }
    private Profile CurrentProfile()=>new(string.IsNullOrWhiteSpace(ProfileName.Text)?"Default":ProfileName.Text.Trim(),(float)DeadZone.Value,Invert.IsChecked==true){Remap=new(mapping),ShoulderLayout=Math.Max(0,ShoulderChoice.SelectedIndex)};
    private void Start_Click(object sender,RoutedEventArgs e)
    {
        wantConnection=true;if(cancellation!=null||stopping)return;
        DualSenseUsb? device=null;IVirtualController? target=null;
        try
        {
            ResetMonitor();reports=0;activeProfile=CurrentProfile();activeProfile.Validate();demo=Demo.IsChecked==true;
            if(!demo){device=new();device.Open();}
            target=Virtual.IsChecked==true?new XboxVirtualController():new PreviewController();output=target;cancellation=new();var session=cancellation;
            StartButton.IsEnabled=false;StopButton.IsEnabled=true;Demo.IsEnabled=false;Virtual.IsEnabled=false;
            DeviceLabel.Text=demo?"Simulazione":$"{device!.DeviceName} / {device.Connection}";
            Status.Text=DeviceLabel.Text+" → "+target.Name;LinkBadge.Text=demo?"SIMULAZIONE":"CONNESSO";LinkBadge.Foreground=System.Windows.Media.Brushes.Turquoise;Log("START "+Status.Text);
            if(device!=null){var owned=device;var backend=target;worker=Task.Run(()=>ReadLoop(owned,backend,session));}
            visionWorker=Task.Run(()=>VisionLoop(session.Token));timer.Start();UpdateOverlay();
        }
        catch(Exception ex)
        {
            device?.Dispose();try{target?.Dispose();}catch(Exception cleanup){Log(cleanup.Message);}output=null;cancellation?.Dispose();cancellation=null;ResetMonitor();Status.Text="Avvio fallito: "+ex.Message;Log(Status.Text);
        }
    }
    private void ReadLoop(DualSenseUsb device,IVirtualController backend,CancellationTokenSource session)
    {
        using(device)
        {
            var engine=new AssistEngine();var clock=System.Diagnostics.Stopwatch.StartNew();long lastInput=Environment.TickCount64;
            try
            {
                while(!session.IsCancellationRequested)
                {
                    PadState s;PadState physical=default;var profile=activeProfile;bool received=true;
                    try{physical=device.Read();s=profile.ApplyOutputLayout(profile.Apply(physical));lastInput=Environment.TickCount64;}
                    catch(TimeoutException){s=default;received=false;if(Environment.TickCount64-lastInput>5000)throw new IOException("Nessun report dal controller da 5 secondi.");}
                    if(session.IsCancellationRequested)break;
                    TargetPoint? target;int size;long observed;double age;lock(gate){observed=frameStamp;age=Math.Max(0,(Environment.TickCount64-observed)/1000.0);target=age<0.25?detected:null;size=frameSize;}
                    var settings=assistSettings;if(useWarzone&&useOnnx)settings=settings with {AimOffsetY=0};if(size!=settings.FovRadius*2)target=null;
                    if(received)s=engine.Process(s,settings,target,size,clock.Elapsed.TotalSeconds,observed,age,physical);else engine.Reset();
                    backend.Submit(s);lock(gate){latest=s;aimCorrectionX=engine.LastAimX;aimCorrectionY=engine.LastAimY;}
                    if(received && Interlocked.Increment(ref reports)==1)Log("FIRST_INPUT: controller reale inoltrato a "+backend.Name);
                }
            }
            catch(Exception ex)
            {
                if(!session.IsCancellationRequested)Dispatcher.BeginInvoke(new Action(async()=>{if(cancellation!=session)return;await StopAsync();Status.Text="Connessione interrotta: "+ex.Message+(wantConnection && Reconnect.IsChecked==true?" • riconnessione in corso":"");Log(Status.Text);}));
            }
        }
    }
    private void Tick(object? sender,EventArgs e)
    {
        try
        {
            RefreshVision();PadState s;
            if(demo){float t=(float)Environment.TickCount64/1000;s=activeProfile.Apply(new(MathF.Sin(t),MathF.Cos(t),0,0,0,0,Buttons.None));output?.Submit(s);}else{lock(gate)s=latest;}
            Monitor.Text=$"LX {s.LX,6:F2}  LY {s.LY,6:F2}\nRX {s.RX,6:F2}  RY {s.RY,6:F2}\nLT {s.LT,3}     RT {s.RT,3}\n\n{s.Buttons}\n\n{Interlocked.Read(ref reports):N0} report inoltrati";RefreshTelemetry(s);
        }
        catch(Exception ex){timer.Stop();_=HandleFailure(ex);}
    }
    internal async Task DeactivateForLicenseAsync(){wantConnection=false;await StopAsync();}
    private async Task HandleFailure(Exception ex){wantConnection=false;await StopAsync();Status.Text="Errore uscita: "+ex.Message;Log(Status.Text);}
    private async void Stop_Click(object sender,RoutedEventArgs e){wantConnection=false;await StopAsync();}
    private void ResetMonitor(){lock(gate)latest=default;Monitor.Text="Nessun input attivo";LinkBadge.Text="OFFLINE";LinkBadge.Foreground=System.Windows.Media.Brushes.HotPink;RateBadge.Text="— Hz";LatencyBadge.Text="— ms";LeftTrigger.Value=RightTrigger.Value=0;rateStamp=lastReportCount=0;System.Windows.Controls.Canvas.SetLeft(LeftStickDot,124);System.Windows.Controls.Canvas.SetTop(LeftStickDot,133);System.Windows.Controls.Canvas.SetLeft(RightStickDot,240);System.Windows.Controls.Canvas.SetTop(RightStickDot,133);}
    private Task StopAsync(){if(stopTask is {IsCompleted:false})return stopTask;stopTask=StopCoreAsync();return stopTask;}
    private async Task StopCoreAsync()
    {
        stopping=true;StopButton.IsEnabled=false;timer.Stop();cancellation?.Cancel();CloseOverlay();
        try
        {
            try{await Task.WhenAll(worker??Task.CompletedTask,visionWorker??Task.CompletedTask);}catch(Exception ex){Log("Worker: "+ex.Message);}
            try{output?.Submit(default);}catch(Exception ex){Log("Neutralizzazione: "+ex.Message);}try{output?.Dispose();}catch(Exception ex){Log("Rilascio: "+ex.Message);}
        }
        finally
        {
            output=null;worker=visionWorker=null;cancellation?.Dispose();cancellation=null;lock(gate){frame=null;detected=null;visionMessage="Sessione arrestata";}
            Preview.Source=null;PreviewOverlay.Children.Clear();VisionStatus.Text=VisionSummary.Text="Sessione arrestata";ResetMonitor();Status.Text="Arrestato • nessun input attivo";Log("STOP");StartButton.IsEnabled=true;Demo.IsEnabled=true;Virtual.IsEnabled=true;stopping=false;
        }
    }
    private async void OnClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(closing)return;e.Cancel=true;wantConnection=false;reconnectTimer.Stop();IsEnabled=false;await StopAsync();UnregisterHotkeys();closing=true;Close();}
    private void Minimize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void Maximize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
}
