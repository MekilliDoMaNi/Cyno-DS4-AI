using System.ComponentModel;
using System.Runtime.InteropServices;
namespace OpenDS4AI.Windows;
public sealed class ScreenCapture : IDisposable
{
    private IntPtr screen, dc, bitmap, original, pixels;
    public int Size {get;}
    public DisplayInfo Display {get;}
    public ScreenCapture(int size,int monitorIndex=0)
    {
        if(size<32 || size>1600)throw new ArgumentOutOfRangeException(nameof(size));Size=size;var monitors=DisplayCatalog.GetDisplays();Display=monitors[Math.Clamp(monitorIndex,0,monitors.Count-1)];if(size>Display.Width||size>Display.Height)throw new ArgumentOutOfRangeException(nameof(size),"FOV maggiore del monitor selezionato.");
        screen=GetDC(IntPtr.Zero);dc=CreateCompatibleDC(screen);
        var info=new BitmapInfo{Size=40,Width=size,Height=-size,Planes=1,BitCount=32};
        bitmap=CreateDIBSection(screen,ref info,0,out pixels,IntPtr.Zero,0);
        if(screen==IntPtr.Zero||dc==IntPtr.Zero||bitmap==IntPtr.Zero){Dispose();throw new Win32Exception("Cattura schermo non disponibile.");}
        original=SelectObject(dc,bitmap);
    }
    public byte[] Grab()
    {
        int x=Display.Left+(Display.Width-Size)/2,y=Display.Top+(Display.Height-Size)/2;
        if(!BitBlt(dc,0,0,Size,Size,screen,x,y,0x00CC0020|0x40000000))throw new Win32Exception(Marshal.GetLastWin32Error());
        byte[] buffer=new byte[Size*Size*4];Marshal.Copy(pixels,buffer,0,buffer.Length);return buffer;
    }
    public void Dispose(){if(dc!=IntPtr.Zero && original!=IntPtr.Zero)SelectObject(dc,original);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(dc!=IntPtr.Zero)DeleteDC(dc);if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);bitmap=dc=screen=original=pixels=IntPtr.Zero;}
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo {public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,SizeImage;public int XPels,YPels;public uint ClrUsed,ClrImportant;}
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd,IntPtr hdc);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("gdi32.dll",SetLastError=true)] private static extern bool BitBlt(IntPtr dest,int x,int y,int w,int h,IntPtr source,int sx,int sy,uint rop);
}
