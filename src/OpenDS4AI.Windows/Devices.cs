using HidSharp;
using OpenDS4AI.Core;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
namespace OpenDS4AI.Windows;
public sealed class DualSenseUsb : IDisposable
{
    private HidStream? stream;
    private byte[] buffer=new byte[64];
    public string DeviceName {get;private set;}="DualSense";
    public string Connection {get;private set;}="USB";
    public void Open()
    {
        var d=DeviceList.Local.GetHidDevices(0x054c).Where(d=>d.ProductID is 0x0ce6 or 0x0df2).OrderBy(d=>d.GetMaxInputReportLength()).FirstOrDefault(d=>d.GetMaxInputReportLength() is 64 or 78) ?? throw new IOException("Nessun DualSense o DualSense Edge USB accessibile. Usa un cavo dati USB; se utilizzi HidHide, autorizza OpenDS4AI.");
        stream=d.Open();stream.ReadTimeout=250;buffer=new byte[d.GetMaxInputReportLength()];Connection=buffer.Length==78?"Bluetooth":"USB";DeviceName=d.ProductID==0x0df2?"DualSense Edge":"DualSense";if(buffer.Length==78){try {var feature=new byte[Math.Max(d.GetMaxFeatureReportLength(),64)];feature[0]=5;stream.GetFeature(feature);}catch(IOException){}catch(NotSupportedException){}}
    }
    public PadState Read() { var s=stream??throw new InvalidOperationException("Controller non aperto."); int n=s.Read(buffer); return DualSenseParser.Parse(buffer.AsSpan(0,n)); }
    public void Dispose() { stream?.Dispose(); stream=null; }
}
public sealed class XboxVirtualController : IVirtualController
{
    private readonly ViGEmClient client;
    private readonly IXbox360Controller target;
    public string Name=>"Xbox 360 virtuale (ViGEm)";
    public XboxVirtualController() { client=new(); try { target=client.CreateXbox360Controller(); target.AutoSubmitReport=false; target.Connect(); } catch { client.Dispose(); throw; } }
    public void Submit(PadState s)
    {
        target.SetAxisValue(Xbox360Axis.LeftThumbX,Axis(s.LX)); target.SetAxisValue(Xbox360Axis.LeftThumbY,Axis(s.LY)); target.SetAxisValue(Xbox360Axis.RightThumbX,Axis(s.RX)); target.SetAxisValue(Xbox360Axis.RightThumbY,Axis(s.RY));
        target.SetSliderValue(Xbox360Slider.LeftTrigger,s.LT); target.SetSliderValue(Xbox360Slider.RightTrigger,s.RT);
        Set(Buttons.A,Xbox360Button.A); Set(Buttons.B,Xbox360Button.B); Set(Buttons.X,Xbox360Button.X); Set(Buttons.Y,Xbox360Button.Y);
        Set(Buttons.Up,Xbox360Button.Up); Set(Buttons.Down,Xbox360Button.Down); Set(Buttons.Left,Xbox360Button.Left); Set(Buttons.Right,Xbox360Button.Right);
        Set(Buttons.LB,Xbox360Button.LeftShoulder); Set(Buttons.RB,Xbox360Button.RightShoulder); Set(Buttons.Back,Xbox360Button.Back); Set(Buttons.Start,Xbox360Button.Start);
        Set(Buttons.LS,Xbox360Button.LeftThumb); Set(Buttons.RS,Xbox360Button.RightThumb); Set(Buttons.Guide,Xbox360Button.Guide); target.SubmitReport();
        void Set(Buttons flag,Xbox360Button button)=>target.SetButtonState(button,(s.Buttons&flag)!=0);
    }
    private static short Axis(float v)=>(short)Math.Clamp(v*(v<0?32768:32767),short.MinValue,short.MaxValue);
    public void Dispose() { try { target.Disconnect(); } finally { client.Dispose(); } }
}