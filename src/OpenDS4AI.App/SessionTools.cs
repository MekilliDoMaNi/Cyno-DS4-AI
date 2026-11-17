using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using OpenDS4AI.Core;
using OpenDS4AI.Windows;
namespace OpenDS4AI.App;
public partial class MainWindow
{
    private Dictionary<Buttons,Buttons> mapping=new();
    private volatile int selectedDisplay;
    private IReadOnlyList<DisplayInfo> displays=Array.Empty<DisplayInfo>();
    private FovWindow? fovWindow;
    private HwndSource? hotkeySource;
    private long rateStamp,lastReportCount,visionElapsedTicks;
    private string LibraryPath=>Path.Combine(Path.GetDirectoryName(profilePath)!,"Profiles");
    private sealed record ButtonChoice(Buttons Value,string Label);
    private static string ButtonLabel(Buttons b)=>b switch {Buttons.A=>"A / Croce",Buttons.B=>"B / Cerchio",Buttons.X=>"X / Quadrato",Buttons.Y=>"Y / Triangolo",Buttons.LB=>"LB / L1",Buttons.RB=>"RB / R1",Buttons.LS=>"LS / L3",Buttons.RS=>"RS / R3",Buttons.Back=>"Back / Crea",Buttons.Start=>"Start / Options",Buttons.Guide=>"Guide / PS",Buttons.None=>"None / Disabilita",_=>b.ToString()};
    private void InitializeSessionTools()
    {
        ShoulderChoice.SelectedIndex=0;ShoulderChoice.SelectionChanged+=(_,_)=>{if(!updatingSettings)activeProfile=CurrentProfile();};
        MapSource.DisplayMemberPath=MapTarget.DisplayMemberPath="Label";
        MapSource.ItemsSource=Enum.GetValues<Buttons>().Where(b=>b!=Buttons.None).Select(b=>new ButtonChoice(b,ButtonLabel(b))).ToArray();
        MapTarget.ItemsSource=Enum.GetValues<Buttons>().Select(b=>new ButtonChoice(b,ButtonLabel(b))).ToArray();MapSource.SelectedIndex=0;MapTarget.SelectedIndex=0;
        ProfileName.TextChanged+=(_,_)=>{if(!updatingSettings)activeProfile=CurrentProfile();};
        try{displays=DisplayCatalog.GetDisplays();DisplayChoice.ItemsSource=displays;DisplayChoice.SelectedIndex=0;}catch(Exception ex){Log("Display: "+ex.Message);}
        DisplayChoice.SelectionChanged+=(_,_)=>{selectedDisplay=Math.Max(0,DisplayChoice.SelectedIndex);lock(gate){detected=null;frameStamp=0;}UpdateOverlay();};
        ShowFov.Checked+=(_,_)=>UpdateOverlay();ShowFov.Unchecked+=(_,_)=>UpdateOverlay();Fov.ValueChanged+=(_,_)=>UpdateOverlay();
        HideCapture.Checked+=(_,_)=>ExcludeOwnWindow();HideCapture.Unchecked+=(_,_)=>ExcludeOwnWindow();RefreshLibrary();RefreshMappingList();
    }
    private void SetProfile(Profile profile){profile.Validate();ProfileName.Text=profile.Name;DeadZone.Value=profile.DeadZone;Invert.IsChecked=profile.InvertY;ShoulderChoice.SelectedIndex=profile.ShoulderLayout;mapping=new(profile.Remap);RefreshMappingList();activeProfile=CurrentProfile();}
    private void AddMapping_Click(object sender,RoutedEventArgs e){if(MapSource.SelectedItem is not ButtonChoice source||MapTarget.SelectedItem is not ButtonChoice target)return;if(source.Value==target.Value)mapping.Remove(source.Value);else mapping[source.Value]=target.Value;activeProfile=CurrentProfile();RefreshMappingList();Status.Text="Rimappatura applicata; salva il profilo per conservarla.";}
    private void ClearMapping_Click(object sender,RoutedEventArgs e){mapping.Clear();activeProfile=CurrentProfile();RefreshMappingList();Status.Text="Rimappatura ripristinata.";}
    private void RefreshMappingList()=>MappingList.ItemsSource=mapping.Count==0?new[]{"Mappa originale · nessuna modifica"}:mapping.Select(p=>$"{ButtonLabel(p.Key)}  →  {ButtonLabel(p.Value)}").ToArray();
    private void RefreshLibrary(){Directory.CreateDirectory(LibraryPath);ProfileLibrary.ItemsSource=Directory.GetFiles(LibraryPath,"*.json").Select(Path.GetFileNameWithoutExtension).Order().ToArray();if(ProfileLibrary.Items.Count>0)ProfileLibrary.SelectedIndex=0;}
    private static void WriteSettings(string path,SavedSettings settings){string temp=path+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(temp,JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,path,true);}
    private void SaveNamedCurrent(){var s=CaptureSettings();string safe=string.Concat(s.Profile.Name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));if(safe.Length>80)safe=safe[..80];WriteSettings(Path.Combine(LibraryPath,safe+".json"),s);RefreshLibrary();ProfileLibrary.SelectedItem=safe;}
    private void LoadNamed_Click(object sender,RoutedEventArgs e){if(ProfileLibrary.SelectedItem is not string name)return;try{ApplySaved(JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(Path.Combine(LibraryPath,name+".json")))??throw new InvalidDataException("Profilo vuoto"));Status.Text="Profilo caricato: "+name;}catch(Exception ex){Status.Text="Profilo non caricato: "+ex.Message;}}
    private void NewProfile_Click(object sender,RoutedEventArgs e){ProfileName.Text="Nuovo profilo";Status.Text="Copia le impostazioni attuali con un nuovo nome, poi salva.";}
    private void RefreshTelemetry(PadState s)
    {
        Canvas.SetLeft(LeftStickDot,124+s.LX*18);Canvas.SetTop(LeftStickDot,133-s.LY*18);Canvas.SetLeft(RightStickDot,240+s.RX*18);Canvas.SetTop(RightStickDot,133-s.RY*18);LeftTrigger.Value=s.LT;RightTrigger.Value=s.RT;
        var idle=new SolidColorBrush(Color.FromRgb(35,50,72));var pressed=new SolidColorBrush(Color.FromRgb(40,110,118));FaceA.Background=(s.Buttons&Buttons.A)!=0?pressed:idle;FaceB.Background=(s.Buttons&Buttons.B)!=0?pressed:idle;FaceX.Background=(s.Buttons&Buttons.X)!=0?pressed:idle;FaceY.Background=(s.Buttons&Buttons.Y)!=0?pressed:idle;
        long now=Environment.TickCount64;if(rateStamp==0){rateStamp=now;lastReportCount=reports;}if(now-rateStamp>=500){long count=Interlocked.Read(ref reports);RateBadge.Text=demo?"DEMO":$"{(count-lastReportCount)*1000.0/(now-rateStamp):F0} Hz";lastReportCount=count;rateStamp=now;}
        long ticks=Interlocked.Read(ref visionElapsedTicks);LatencyBadge.Text=assistSettings.AimEnabled?$"{ticks*1000.0/System.Diagnostics.Stopwatch.Frequency:F0} ms":"OFF";
    }
    private void UpdateOverlay(){if(ShowFov.IsChecked!=true||cancellation==null||displays.Count==0){CloseOverlay();return;}fovWindow??=new FovWindow();fovWindow.Configure(displays[Math.Clamp(selectedDisplay,0,displays.Count-1)],(int)Fov.Value);if(!fovWindow.IsVisible)fovWindow.Show();}
    private void CloseOverlay(){fovWindow?.Close();fovWindow=null;}
    private void RegisterHotkeys(){hotkeySource=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);hotkeySource?.AddHook(HotkeyHook);for(int i=8;i<=10;i++)if(!RegisterHotKey(new WindowInteropHelper(this).Handle,i,0x4000,(uint)(0x6f+i)))Log($"Hotkey F{i} non disponibile.");}
    private void UnregisterHotkeys(){for(int i=8;i<=10;i++)UnregisterHotKey(new WindowInteropHelper(this).Handle,i);hotkeySource?.RemoveHook(HotkeyHook);}
    private IntPtr HotkeyHook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){if(message!=0x312)return IntPtr.Zero;switch(w.ToInt32()){case 8:if(cancellation!=null){wantConnection=false;_=StopAsync();}else Start_Click(this,new());break;case 9:AimEnabled.IsChecked=AimEnabled.IsChecked!=true;break;case 10:ShowFov.IsChecked=ShowFov.IsChecked!=true;break;default:return IntPtr.Zero;}handled=true;return IntPtr.Zero;}
    private void Diagnostics_Click(object sender,RoutedEventArgs e){try{int xinput=0;for(uint i=0;i<4;i++)if(XInputGetState(i,out _)==0)xinput++;Diagnostics.Text=$"Sessione: {(cancellation!=null?"attiva":"arrestata")}\nGamepad XInput: {xinput}\nModello: {(File.Exists(modelPath)?"presente":"non trovato")}\nMonitor: {displays.Count}\nRuntime: {Environment.Version}\n{visionMessage}";}catch(Exception ex){Diagnostics.Text=ex.Message;}}
    [DllImport("user32.dll")]private static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("xinput1_4.dll")]private static extern int XInputGetState(uint index,out XState state);
    [StructLayout(LayoutKind.Sequential)]private struct XState{public uint Packet;public ushort Buttons;public byte LT,RT;public short LX,LY,RX,RY;}
}
public sealed class FovWindow:Window
{
    public FovWindow(){WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;ShowActivated=false;IsHitTestVisible=false;Focusable=false;Content=new System.Windows.Shapes.Ellipse{Stroke=Brushes.Cyan,StrokeThickness=1,StrokeDashArray=new DoubleCollection{6,4},Margin=new Thickness(2),Opacity=0.75};SourceInitialized+=(_,_)=>{var hwnd=new WindowInteropHelper(this).Handle;SetWindowLongPtr(hwnd,-20,new IntPtr(GetWindowLongPtr(hwnd,-20).ToInt64()|0x20|0x80|0x08000000));SetWindowDisplayAffinity(hwnd,0x11);};}
    public void Configure(DisplayInfo display,int radius){var dpi=VisualTreeHelper.GetDpi(this);Width=radius*2/dpi.DpiScaleX;Height=radius*2/dpi.DpiScaleY;Left=(display.Left+display.Width/2.0-radius)/dpi.DpiScaleX;Top=(display.Top+display.Height/2.0-radius)/dpi.DpiScaleY;}
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll")]private static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint flags);
}
