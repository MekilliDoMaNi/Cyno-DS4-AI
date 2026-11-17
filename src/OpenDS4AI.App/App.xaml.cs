using System.Threading;using System.Windows;using System.Windows.Threading;using Cyno.Licensing.Windows;
namespace OpenDS4AI.App;
public partial class App:Application
{
    private Mutex? instance;private bool ownsInstance,switching;private Window? activeWindow;private DispatcherTimer? licenseTimer;
    protected override void OnStartup(StartupEventArgs e)
    {
        if(e.Args.Length==2 && e.Args[0]=="--apply-update"){try{System.Threading.Tasks.Task.Run(()=>AppUpdates.Apply(e.Args[1])).GetAwaiter().GetResult();}catch(Exception ex){MessageBox.Show("Aggiornamento non completato: "+ex.Message,"Cyno aggiornamenti");}Shutdown();return;}
        instance=new Mutex(true,"Local\\CynoDS4AI.Neon.UserSession",out ownsInstance);
        if(!ownsInstance){MessageBox.Show("Cyno DS4.AI è già aperto.","Cyno DS4.AI");Shutdown();return;}
        ShutdownMode=ShutdownMode.OnExplicitShutdown;base.OnStartup(e);
        var check=LicenseInstallation.Check();if(check.Valid)ShowLicensedMain();else ShowActivation(check.Message);
    }
    private void ReplaceWindow(Window next)
    {
        var old=activeWindow;activeWindow=next;MainWindow=next;next.Closed+=(_,_)=>{if(ReferenceEquals(activeWindow,next))Shutdown();};next.Show();old?.Close();
    }
    private void ShowActivation(string message){licenseTimer?.Stop();var window=new ActivationWindow(message);window.ActivatedLicense+=ShowLicensedMain;ReplaceWindow(window);}
    private void ShowLicensedMain()
    {
        var check=LicenseInstallation.Check();if(!check.Valid){ShowActivation(check.Message);return;}
        var window=new OpenDS4AI.App.MainWindow();ReplaceWindow(window);licenseTimer?.Stop();licenseTimer=new(){Interval=TimeSpan.FromSeconds(15)};
        licenseTimer.Tick+=async(_,_)=>{if(switching)return;var result=LicenseInstallation.Check();if(result.Valid)return;switching=true;licenseTimer?.Stop();await window.DeactivateForLicenseAsync();ShowActivation(result.Message);switching=false;};licenseTimer.Start();
    }
    protected override void OnExit(ExitEventArgs e){licenseTimer?.Stop();if(ownsInstance)instance?.ReleaseMutex();instance?.Dispose();base.OnExit(e);}
}
