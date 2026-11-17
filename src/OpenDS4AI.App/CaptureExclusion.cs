using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace OpenDS4AI.App;
public partial class MainWindow
{
    private void ExcludeOwnWindow(){var hwnd=new WindowInteropHelper(this).Handle;if(hwnd!=IntPtr.Zero)SetWindowDisplayAffinity(hwnd,HideCapture.IsChecked==true?0x11u:0u);}
    [DllImport("user32.dll")]private static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
}
