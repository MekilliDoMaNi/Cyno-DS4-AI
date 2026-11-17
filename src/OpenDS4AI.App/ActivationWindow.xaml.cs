using System.IO;using System.Windows;using System.Windows.Threading;using Microsoft.Win32;using Cyno.Licensing.Windows;
namespace OpenDS4AI.App;
public partial class ActivationWindow:Window
{
    public event Action? ActivatedLicense;private bool ready;private readonly DispatcherTimer poll=new(){Interval=TimeSpan.FromSeconds(2)};
    public ActivationWindow(string reason){InitializeComponent();ActivationStatus.Text=reason;try{using var device=new DeviceIdentity();DeviceCode.Text=device.Id;DeviceProtection.Text="Protezione dispositivo: "+device.Protection;}catch(Exception ex){ActivationStatus.Text=ex.Message;}poll.Tick+=(_,_)=>{var check=LicenseInstallation.Check();if(check.Valid)Complete();};Closed+=(_,_)=>poll.Stop();poll.Start();}
    private void Complete(){if(ready)return;ready=true;poll.Stop();ActivatedLicense?.Invoke();}
    private void ExportRequest_Click(object sender,RoutedEventArgs e){try{using var device=new DeviceIdentity();var dialog=new SaveFileDialog{Filter="Richiesta Cyno|*.cynorequest",FileName="Richiesta-Cyno-"+device.Id[..8]+".cynorequest"};if(dialog.ShowDialog()==true){File.WriteAllText(dialog.FileName,device.CreateRequest());ActivationStatus.Text="Richiesta esportata. Inviala al proprietario per ricevere la licenza.";}}catch(Exception ex){ActivationStatus.Text="Esportazione non riuscita: "+ex.Message;}}
    private void ImportLicense_Click(object sender,RoutedEventArgs e){var dialog=new OpenFileDialog{Filter="Licenza Cyno|*.cynolicense"};if(dialog.ShowDialog()!=true)return;try{var check=LicenseInstallation.Import(dialog.FileName);ActivationStatus.Text=check.Message;if(check.Valid)Complete();}catch(Exception ex){ActivationStatus.Text="Importazione non riuscita: "+ex.Message;}}
}
