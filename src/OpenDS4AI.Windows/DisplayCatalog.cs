using System.Runtime.InteropServices;
namespace OpenDS4AI.Windows;
public sealed record DisplayInfo(int Index,int Left,int Top,int Width,int Height,bool Primary)
{ public string Label=>$"Monitor {Index+1} · {Width}×{Height}"+(Primary?" · principale":""); }
public static class DisplayCatalog
{
    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var list=new List<DisplayInfo>();
        MonitorEnum callback=(IntPtr h,IntPtr dc,ref Rect rect,IntPtr data)=>{var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(GetMonitorInfo(h,ref info))list.Add(new(0,info.Monitor.Left,info.Monitor.Top,info.Monitor.Right-info.Monitor.Left,info.Monitor.Bottom-info.Monitor.Top,(info.Flags&1)!=0));return true;};
        if(!EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero)||list.Count==0)throw new IOException("Monitor non disponibili.");
        return list.OrderByDescending(x=>x.Primary).ThenBy(x=>x.Left).Select((x,i)=>x with {Index=i}).ToArray();
    }
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{public int Size;public Rect Monitor,Work;public int Flags;}
    private delegate bool MonitorEnum(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr data);
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorEnum callback,IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
}
