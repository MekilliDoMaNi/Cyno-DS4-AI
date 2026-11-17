using System.IO;
namespace OpenDS4AI.App;
public partial class MainWindow
{
    private void MigrateLegacySettings()
    {
        try
        {
            string previous=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenDS4AI"),current=Path.GetDirectoryName(profilePath)!;
            Directory.CreateDirectory(current);
            foreach(string name in new[]{"profile.json","assistance.json"}){string source=Path.Combine(previous,name),destination=Path.Combine(current,name);if(File.Exists(source)&&!File.Exists(destination))File.Copy(source,destination);}
            string profiles=Path.Combine(previous,"Profiles"),target=Path.Combine(current,"Profiles");if(Directory.Exists(profiles)){Directory.CreateDirectory(target);foreach(var file in Directory.GetFiles(profiles,"*.json")){string destination=Path.Combine(target,Path.GetFileName(file));if(!File.Exists(destination))File.Copy(file,destination);}}
        }
        catch(Exception ex){Log("Migrazione profili: "+ex.Message);}
    }
}
