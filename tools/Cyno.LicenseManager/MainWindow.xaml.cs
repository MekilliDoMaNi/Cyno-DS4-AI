using System.IO;using System.Windows;using Microsoft.Win32;using Cyno.Licensing;using Cyno.Licensing.Windows;
namespace Cyno.LicenseManager;
public partial class MainWindow:Window
{
    private DeviceRequest? request;
    public MainWindow(){InitializeComponent();Expiry.SelectedDate=DateTime.Today.AddYears(1);RefreshAuthority();}
    private void RefreshAuthority(){try{using var key=AuthorityStore.Load();Generate.IsEnabled=true;AuthorityStatus.Text="Firma disponibile sul tuo account Windows. Autorità: "+LicenseProtocol.PublicKeyId(IssuerTrust.PublicKeyPem);}catch(Exception ex){Generate.IsEnabled=false;AuthorityStatus.Text=ex.Message;}}
    private void OpenRequest_Click(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Filter="Richiesta attivazione Cyno|*.cynorequest"};if(d.ShowDialog()!=true)return;try{if(new FileInfo(d.FileName).Length>LicenseProtocol.MaxFileBytes)throw new InvalidDataException("Richiesta troppo grande.");request=LicenseProtocol.ReadRequest(File.ReadAllText(d.FileName));RequestInfo.Text="PC: "+request.DeviceId+"\nChiave: "+request.KeyProtection;Result.Text="Richiesta verificata. Imposta il nome e genera la licenza.";}catch(Exception ex){request=null;RequestInfo.Text="Richiesta non valida.";Result.Text=ex.Message;}}
    private void Generate_Click(object sender,RoutedEventArgs e)
    {
        try{if(request==null)throw new InvalidOperationException("Apri prima una richiesta di attivazione.");DateTimeOffset? expires=null;if(Permanent.IsChecked!=true){if(Expiry.SelectedDate is not {} date)throw new ArgumentException("Seleziona la scadenza.");expires=new DateTimeOffset(DateTime.SpecifyKind(date.Date.AddDays(1),DateTimeKind.Local)).ToUniversalTime();}string license=AuthorityStore.Issue(request,Customer.Text,expires);var d=new SaveFileDialog{Filter="Licenza Cyno|*.cynolicense",FileName="Licenza-Cyno-"+request.DeviceId[..8]+".cynolicense"};if(d.ShowDialog()==true){File.WriteAllText(d.FileName,license);Result.Text="Licenza firmata salvata. Invia soltanto questo file al destinatario.";}}catch(Exception ex){Result.Text="Licenza non generata: "+ex.Message;}
    }
    private void Backup_Click(object sender,RoutedEventArgs e){try{string pem=AuthorityStore.ExportBackup(BackupPassword.Password);var d=new SaveFileDialog{Filter="Backup chiave privata cifrata|*.pem",FileName="Cyno-autorita-BACKUP-PRIVATO.pem"};if(d.ShowDialog()==true){File.WriteAllText(d.FileName,pem);Result.Text="Backup cifrato salvato. Non inviarlo ai clienti.";}BackupPassword.Clear();}catch(Exception ex){Result.Text=ex.Message;}}
    private void Restore_Click(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Filter="Backup chiave privata cifrata|*.pem"};if(d.ShowDialog()!=true)return;try{AuthorityStore.RestoreBackup(File.ReadAllText(d.FileName),BackupPassword.Password);BackupPassword.Clear();RefreshAuthority();Result.Text="Chiave ripristinata su questo account Windows.";}catch(Exception ex){Result.Text="Ripristino fallito: "+ex.Message;}}
}
