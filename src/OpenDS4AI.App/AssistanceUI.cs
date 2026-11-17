using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenDS4AI.Core;
using OpenDS4AI.Windows;
namespace OpenDS4AI.App;
public partial class MainWindow
{
    private volatile AssistSettings assistSettings=new();
    private volatile bool useOnnx;
    private volatile bool keepTarget,estimateHead,activationHeld;private volatile bool useWarzone=true;private volatile float anchorValue=0.35f;private long engineGeneration;
    private volatile string modelPath=Path.Combine(AppContext.BaseDirectory,"models","yolov8n.onnx");
    private Task? visionWorker;
    private TargetPoint? detected;
    private int frameSize=300,frameRadius=150;private bool frameWideContext;
    private byte[]? frame;
    private long frameStamp;
    private string visionMessage="Rilevamento disattivato";
    private long previewStamp;
    private bool updatingSettings;
    private string settingsError="";
    private string SettingsPath=>Path.Combine(Path.GetDirectoryName(profilePath)!,"assistance.json");
    private sealed record SavedSettings(Profile Profile,AssistSettings Assistance,bool Onnx,string ModelPath,float Confidence=0.45f,bool AutoReconnect=true,bool Overlay=false,int Display=0,bool ExcludeCapture=true,bool Warzone=true,float Anchor=0.35f,bool KeepTarget=false,bool HeadEstimate=false);
    private void InitializeAssistance()
    {
        foreach(var s in new[]{Tolerance,Fov,Strength,Smooth,AimOffset,Confidence,RecoilY,RecoilX,RapidMs,BunnyMs,PingDelay,PingCooldown,SwapMs,SlideDuration,AimLimit,AimDamp,AimDeadband})s.ValueChanged+=(_,_)=>UpdateAssistance();
        foreach(var c in new[]{AimEnabled,OnnxMode,ActivationLT,Recoil,Rapid,Bunny,Ping,Swap,Slide,StableControl,ReverseAimX,ReverseAimY,LatencyControl,WideVisionControl}) {c.Checked+=(_,_)=>UpdateAssistance();c.Unchecked+=(_,_)=>UpdateAssistance();}
        KeepTargetControl.Checked+=(_,_)=>UpdateEngineMode();KeepTargetControl.Unchecked+=(_,_)=>UpdateEngineMode();HeadEstimateControl.Checked+=(_,_)=>UpdateEngineMode();HeadEstimateControl.Unchecked+=(_,_)=>UpdateEngineMode();
        ActivationChoice.SelectedIndex=0;ActivationChoice.SelectionChanged+=(_,_)=>UpdateAssistance();
        Marker.TextChanged+=(_,_)=>UpdateAssistance();
        GameEngineChoice.ItemsSource=new[]{"Warzone / indipendente","Generico / centro FOV"};GameEngineChoice.SelectedIndex=0;GameEngineChoice.SelectionChanged+=(_,_)=>UpdateEngineMode();WarzoneAnchor.ValueChanged+=(_,_)=>UpdateEngineMode();try{modelPath=ModelVault.EnsureBundledModel();}catch(Exception ex){Log("Model vault: "+ex.Message);}ModelLabel.Text=modelPath;AimEnabled.IsChecked=true;OnnxMode.IsChecked=File.Exists(modelPath);
        try{if(File.Exists(SettingsPath))ApplySaved(JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(SettingsPath))??throw new InvalidDataException("Profilo vuoto"));}catch(Exception ex){Log("Impostazioni assistenza: "+ex.Message);}
        UpdateAssistance();
    }
    private void UpdateAssistance()
    {
        if(updatingSettings)return;
        try
        {
            var settings=new AssistSettings{AimEnabled=AimEnabled.IsChecked==true,MarkerHex=Marker.Text.Trim().TrimStart('#'),ColorTolerance=(int)Tolerance.Value,FovRadius=(int)Fov.Value,Strength=(float)Strength.Value,Smooth=(float)Smooth.Value,AimOffsetY=(float)AimOffset.Value,ActivateOnLeftTrigger=ActivationLT.IsChecked==true,ActivateOnL1=ActivationChoice.SelectedIndex==1,AntiRecoil=Recoil.IsChecked==true,RecoilVertical=(float)RecoilY.Value,RecoilHorizontal=(float)RecoilX.Value,RapidFire=Rapid.IsChecked==true,RapidPeriodMs=(int)RapidMs.Value,BunnyHop=Bunny.IsChecked==true,BunnyPeriodMs=(int)BunnyMs.Value,AutoPing=Ping.IsChecked==true,PingDelayMs=(int)PingDelay.Value,PingCooldownMs=(int)PingCooldown.Value,SpamSwap=Swap.IsChecked==true,SwapPeriodMs=(int)SwapMs.Value,CancelSlide=Slide.IsChecked==true,SlideMs=(int)SlideDuration.Value,StableAim=StableControl.IsChecked==true,AimMaxOutput=(float)AimLimit.Value,AimDamping=(float)AimDamp.Value,AimDeadbandPixels=(float)AimDeadband.Value,InvertAimX=ReverseAimX.IsChecked==true,InvertAimY=ReverseAimY.IsChecked==true,CompensateVisionLatency=LatencyControl.IsChecked==true,WideContext=WideVisionControl.IsChecked==true};
            settings.Validate();if(settings!=assistSettings || useOnnx!=(OnnxMode.IsChecked==true)){Interlocked.Increment(ref engineGeneration);lock(gate){detected=null;frameStamp=0;}}assistSettings=settings;useOnnx=OnnxMode.IsChecked==true;settingsError="";
        }catch(ArgumentException ex){settingsError=ex.Message;assistSettings=assistSettings with {AimEnabled=false};VisionStatus.Text=ex.Message;}
    }
    private void HeadPreset_Click(object sender,RoutedEventArgs e)
    {
        updatingSettings=true;
        try
        {
            GameEngineChoice.SelectedIndex=0;KeepTargetControl.IsChecked=true;HeadEstimateControl.IsChecked=true;
            WideVisionControl.IsChecked=true;LatencyControl.IsChecked=true;StableControl.IsChecked=true;
            AimEnabled.IsChecked=true;OnnxMode.IsChecked=true;Fov.Value=150;Strength.Value=1.4;Smooth.Value=0.08;
            AimLimit.Value=0.23;AimDamp.Value=1;AimDeadband.Value=2;
            ProfileName.Text="Warzone - zona testa stimata 0.6.4";
        }
        finally{updatingSettings=false;UpdateEngineMode();UpdateAssistance();activeProfile=CurrentProfile();}
        try{SaveAssistance();SaveNamedCurrent();Status.Text="Preset salvato. Zona testa stimata dal corpo; riconoscimento testa dedicato non presente.";}
        catch(Exception ex){Status.Text="Preset applicato, salvataggio non riuscito: "+ex.Message;}
    }
    private SavedSettings CaptureSettings()=>new(CurrentProfile(),assistSettings,useOnnx,modelPath,(float)Confidence.Value,Reconnect.IsChecked==true,ShowFov.IsChecked==true,selectedDisplay,HideCapture.IsChecked==true,useWarzone,anchorValue,keepTarget,estimateHead);
    private void SaveAssistance(){UpdateAssistance();if(settingsError.Length>0)throw new ArgumentException(settingsError);var s=CaptureSettings();s.Profile.Validate();Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);string temp=SettingsPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,SettingsPath,true);}
    private void ApplySaved(SavedSettings saved)
    {
        if(saved.Profile==null||saved.Assistance==null)throw new InvalidDataException("Profilo incompleto.");saved.Profile.Validate();saved.Assistance.Validate();if(!float.IsFinite(saved.Confidence)||saved.Confidence<0.1||saved.Confidence>0.95)throw new InvalidDataException("Soglia AI non valida.");updatingSettings=true;
        try
        {
            KeepTargetControl.IsChecked=saved.KeepTarget;HeadEstimateControl.IsChecked=saved.HeadEstimate;GameEngineChoice.SelectedIndex=saved.Warzone?0:1;WarzoneAnchor.Value=Math.Clamp(saved.Anchor,0.15f,0.8f);UpdateEngineMode();Confidence.Value=saved.Confidence;SetProfile(saved.Profile);Reconnect.IsChecked=saved.AutoReconnect;ShowFov.IsChecked=saved.Overlay;HideCapture.IsChecked=saved.ExcludeCapture;DisplayChoice.SelectedIndex=Math.Clamp(saved.Display,0,Math.Max(0,displays.Count-1));var s=saved.Assistance;StableControl.IsChecked=s.StableAim;AimLimit.Value=s.AimMaxOutput;AimDamp.Value=s.AimDamping;AimDeadband.Value=s.AimDeadbandPixels;ReverseAimX.IsChecked=s.InvertAimX;ReverseAimY.IsChecked=s.InvertAimY;LatencyControl.IsChecked=s.CompensateVisionLatency;WideVisionControl.IsChecked=s.WideContext;
            AimEnabled.IsChecked=s.AimEnabled;Marker.Text=s.MarkerHex;Tolerance.Value=s.ColorTolerance;Fov.Value=s.FovRadius;Strength.Value=s.Strength;Smooth.Value=s.Smooth;AimOffset.Value=s.AimOffsetY;ActivationLT.IsChecked=s.ActivateOnLeftTrigger;ActivationChoice.SelectedIndex=s.ActivateOnL1?1:0;Recoil.IsChecked=s.AntiRecoil;RecoilY.Value=s.RecoilVertical;RecoilX.Value=s.RecoilHorizontal;Rapid.IsChecked=s.RapidFire;RapidMs.Value=s.RapidPeriodMs;Bunny.IsChecked=s.BunnyHop;BunnyMs.Value=s.BunnyPeriodMs;Ping.IsChecked=s.AutoPing;PingDelay.Value=s.PingDelayMs;PingCooldown.Value=s.PingCooldownMs;Swap.IsChecked=s.SpamSwap;SwapMs.Value=s.SwapPeriodMs;Slide.IsChecked=s.CancelSlide;SlideDuration.Value=s.SlideMs;OnnxMode.IsChecked=saved.Onnx;modelPath=string.IsNullOrWhiteSpace(saved.ModelPath)||saved.ModelPath.EndsWith("yolov8n.onnx",StringComparison.OrdinalIgnoreCase)?ModelVault.EnsureBundledModel():saved.ModelPath;ModelLabel.Text=modelPath;
        }
        finally{updatingSettings=false;UpdateAssistance();activeProfile=CurrentProfile();}
    }
    private void Model_Click(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Filter="Modelli compatibili|*.onnx;*.cynomodel"};if(d.ShowDialog()!=true)return;try{modelPath=ModelVault.Import(d.FileName);ModelLabel.Text=modelPath;OnnxMode.IsChecked=true;UpdateEngineMode();Status.Text="Modello importato e protetto per questo account Windows.";}catch(Exception ex){Status.Text="Importazione modello fallita: "+ex.Message;}}
    private void Export_Click(object sender,RoutedEventArgs e){var d=new SaveFileDialog{Filter="Profilo Cyno DS4.AI|*.json",FileName="Cyno-DS4.AI-profile.json"};if(d.ShowDialog()==true){try{File.WriteAllText(d.FileName,JsonSerializer.Serialize(CaptureSettings(),new JsonSerializerOptions{WriteIndented=true}));Status.Text="Profilo esportato";}catch(Exception ex){Status.Text=ex.Message;}}}
    private void Import_Click(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Filter="Profilo Cyno DS4.AI|*.json"};if(d.ShowDialog()==true){try{ApplySaved(JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(d.FileName))??throw new InvalidDataException("Profilo vuoto"));Status.Text="Profilo importato";}catch(Exception ex){Status.Text="Importazione fallita: "+ex.Message;}}}
    private void ImportOriginal_Click(object sender,RoutedEventArgs e)
    {
        var d=new OpenFileDialog{Filter="Impostazioni DS4AI|*.json",InitialDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"config")};
        if(d.ShowDialog()!=true)return;
        try
        {
            using var json=JsonDocument.Parse(File.ReadAllText(d.FileName));var r=json.RootElement;
            float Num(string name,float fallback)=>r.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.Number?v.GetSingle():fallback;
            bool Flag(string name,bool fallback=false)=>r.TryGetProperty(name,out var v)&&v.ValueKind is JsonValueKind.True or JsonValueKind.False?v.GetBoolean():fallback;
            string Text(string name,string fallback)=>r.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??fallback:fallback;
            var original=assistSettings with {AimEnabled=true,MarkerHex=Text("MarkerHex","9800CD"),FovRadius=(int)Math.Clamp(Num("FovRadius",150),50,400),Strength=Math.Clamp(Num("AimStrength",1),0,5),Smooth=Math.Clamp(Num("AimSmooth",0.25f),0,0.95f),ActivateOnLeftTrigger=Text("AimActivator","L2")!="toujours",ActivateOnL1=Text("AimActivator","L2")=="L1",AntiRecoil=Flag("AntiRecoil"),RecoilVertical=Math.Clamp(Num("RecoilPush",0.14f),0,0.6f),RapidFire=Flag("Rapidfire"),RapidPeriodMs=(int)Math.Clamp(Num("RapidDelayMs",40)*2,40,400),AutoPing=Flag("AutoPing"),PingDelayMs=(int)Math.Clamp(Num("PingDelayMs",250),0,1000),PingCooldownMs=(int)Math.Clamp(Num("PingCooldownMs",1500),100,5000),SpamSwap=Flag("SpamSwap"),SwapPeriodMs=(int)Math.Clamp(Num("SpamDelayMs",50)*2,40,1000),CancelSlide=Flag("CancelSlide"),SlideMs=(int)Math.Clamp(Num("CancelSlideMs",80),20,500),BunnyHop=Flag("BunnyHop"),BunnyPeriodMs=(int)Math.Clamp(Num("BunnyMs",80)*2,80,600)};
            ApplySaved(new(CurrentProfile() with {InvertY=Flag("InvertY")},original,Flag("YoloEnabled")||Flag("YoloOnly"),modelPath));
            Status.Text="Importati FOV, forza, smoothing, colore, attivazione e macro compatibili. Le altre opzioni restano escluse.";
        }catch(Exception ex){Status.Text="Importazione originale fallita: "+ex.Message;}
    }
    private void VisionLoop(CancellationToken token)
    {
        ScreenCapture? capture=null;OnnxTargetDetector? detector=null;string loadedPath="";int captureDisplay=-1;var warzone=new WarzoneEngine();var trackClock=Stopwatch.StartNew();long lastGeneration=-1;
        try
        {
            while(!token.IsCancellationRequested)
            {
                var settings=assistSettings;long generation=Interlocked.Read(ref engineGeneration);if(generation!=lastGeneration){warzone.Reset();lastGeneration=generation;}
                if(!activationHeld)warzone.Reset();
                if(!settings.AimEnabled){lock(gate){detected=null;frame=null;visionMessage="Rilevamento disattivato";}if(token.WaitHandle.WaitOne(100))break;continue;}
                try
                {
                    var monitors=displays.Count>0?displays:DisplayCatalog.GetDisplays();var monitorInfo=monitors[Math.Clamp(selectedDisplay,0,monitors.Count-1)];int size=VisionGeometry.CaptureSize(settings.FovRadius,settings.WideContext,monitorInfo.Width,monitorInfo.Height);
                    int monitor=selectedDisplay;if(capture==null||capture.Size!=size||captureDisplay!=monitor){capture?.Dispose();capture=null;capture=new(size,monitor);captureDisplay=monitor;}
                    long capturedAt=Environment.TickCount64;var sw=Stopwatch.StartNew();var pixels=capture.Grab();TargetPoint? target;
                    if(useOnnx)
                    {
                        string path=modelPath;
                        if(detector==null||path!=loadedPath){detector?.Dispose();detector=null;detector=new(path);loadedPath=path;}
                        target=useWarzone?warzone.Select(detector.DetectCandidates(pixels,size,size,Math.Max(0.05f,confidenceValue*0.65f)),size,settings.FovRadius,trackClock.Elapsed.TotalSeconds,new(confidenceValue,estimateHead?0.12f:anchorValue,KeepTarget:keepTarget&&activationHeld,RequireVisibleTop:estimateHead)):detector.Detect(pixels,size,size,settings,confidenceValue);
                    }
                    else target=ColorTargetDetector.Detect(pixels,size,size,settings);
                    if(generation!=Interlocked.Read(ref engineGeneration))target=null;Interlocked.Exchange(ref visionElapsedTicks,sw.ElapsedTicks);lock(gate){detected=target;frame=pixels;frameSize=size;frameRadius=settings.FovRadius;frameWideContext=settings.WideContext;frameStamp=capturedAt;visionMessage=$"{(useOnnx?(useWarzone?"Warzone / modello generico":"AI generica"):"Marcatore colore")} • {sw.Elapsed.TotalMilliseconds:F0} ms • {(target.HasValue?$"bersaglio {target.Value.X:F0}, {target.Value.Y:F0}":"nessun bersaglio")}";}
                }
                catch(Exception ex){lock(gate){detected=null;frame=null;visionMessage="Rilevamento non disponibile: "+ex.Message;}if(token.WaitHandle.WaitOne(500))break;}
                if(token.WaitHandle.WaitOne(useOnnx?1:16))break;
            }
        }
        finally{capture?.Dispose();detector?.Dispose();lock(gate){detected=null;frame=null;}}
    }
    private void UpdateEngineMode(){keepTarget=KeepTargetControl.IsChecked==true;estimateHead=HeadEstimateControl.IsChecked==true;useWarzone=GameEngineChoice.SelectedIndex==0;anchorValue=(float)WarzoneAnchor.Value;Interlocked.Increment(ref engineGeneration);lock(gate){detected=null;frameStamp=0;}}
    private volatile float confidenceValue=0.45f;
    private void RefreshVision()
    {
        confidenceValue=(float)Confidence.Value;lock(gate){double age=(Environment.TickCount64-frameStamp);AimControlState.Text=age>250?"Frame obsoleto: correzione sospesa":$"Correzione X {aimCorrectionX:F3}  Y {aimCorrectionY:F3} · età frame {age:F0} ms";}
        byte[]? pixels;int size;string message;
        lock(gate){pixels=frame;size=frameSize;message=visionMessage;}
        if(settingsError.Length>0)message="Impostazioni non valide: "+settingsError;VisionStatus.Text=message;VisionSummary.Text=message;
        if(Environment.TickCount64-previewStamp<150)return;previewStamp=Environment.TickCount64;
        PreviewOverlay.Children.Clear();if(pixels==null){Preview.Source=null;return;}
        var bitmap=BitmapSource.Create(size,size,96,96,PixelFormats.Bgr32,null,pixels,size*4);bitmap.Freeze();Preview.Source=bitmap;
        double diameter=assistSettings.FovRadius*2.0*330/size;var fovRing=new System.Windows.Shapes.Ellipse{Width=diameter,Height=diameter,Stroke=Brushes.Cyan,StrokeThickness=1};Canvas.SetLeft(fovRing,(330-diameter)/2);Canvas.SetTop(fovRing,(330-diameter)/2);PreviewOverlay.Children.Add(fovRing);
        TargetPoint? point;lock(gate)point=detected;if(point is {} t){var dot=new System.Windows.Shapes.Ellipse{Width=10,Height=10,Stroke=Brushes.Lime,StrokeThickness=2};Canvas.SetLeft(dot,t.X*330/size-5);Canvas.SetTop(dot,t.Y*330/size-5);PreviewOverlay.Children.Add(dot);}
    }
}
