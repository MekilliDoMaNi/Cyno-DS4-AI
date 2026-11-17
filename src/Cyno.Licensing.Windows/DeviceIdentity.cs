using System.Security.Cryptography;using System.Text;using Microsoft.Win32;using Cyno.Licensing;
namespace Cyno.Licensing.Windows;
public static class IssuerTrust
{
    public static string PublicKeyPem{get;}=Load();
    private static string Load(){using var stream=typeof(IssuerTrust).Assembly.GetManifestResourceStream("CynoDS4AI.Licensing.IssuerPublicKey")??throw new InvalidOperationException("Chiave pubblica non configurata.");using var reader=new StreamReader(stream);return reader.ReadToEnd();}
}
public sealed class DeviceIdentity:IDisposable
{
    private const string KeyName="CynoDS4AI.DeviceIdentity.v1";private readonly CngKey key;private readonly ECDsaCng signer;
    public string PublicKey{get;}public string MachineHash{get;}public string Id{get;}public string Protection{get;}
    public DeviceIdentity()
    {
        key=OpenDeviceKey();signer=new ECDsaCng(key);PublicKey=Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo());
        using var registry=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);using var crypt=registry.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");string machine=crypt?.GetValue("MachineGuid") as string??throw new InvalidOperationException("Identità Windows non disponibile.");if(!Guid.TryParse(machine,out var machineGuid))throw new InvalidOperationException("Identità Windows non valida.");
        MachineHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(machineGuid.ToString("D"))));Id=LicenseProtocol.DeviceId(MachineHash,PublicKey);Protection=key.Provider?.Provider=="Microsoft Platform Crypto Provider"?"TPM":"Windows CNG / profilo utente";
    }
    private static CngKey OpenDeviceKey()
    {
        foreach(var provider in new[]{new CngProvider("Microsoft Platform Crypto Provider"),CngProvider.MicrosoftSoftwareKeyStorageProvider})
        {
            try{if(CngKey.Exists(KeyName,provider))return CngKey.Open(KeyName,provider);return CngKey.Create(CngAlgorithm.ECDsaP256,KeyName,new CngKeyCreationParameters{Provider=provider,ExportPolicy=CngExportPolicies.None,KeyUsage=CngKeyUsages.Signing,UIPolicy=new CngUIPolicy(CngUIProtectionLevels.None)});}catch(CryptographicException){}
        }
        throw new CryptographicException("Impossibile creare la chiave non esportabile del dispositivo.");
    }
    public string CreateRequest()=>LicenseProtocol.Sign(new DeviceRequest(1,LicenseProtocol.Product,Guid.NewGuid().ToString("N"),Id,MachineHash,PublicKey,DateTimeOffset.UtcNow,Protection),signer);
    public bool ProvePossession(){byte[] challenge=RandomNumberGenerator.GetBytes(32),signature=signer.SignData(challenge,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);using var check=ECDsa.Create();check.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKey),out _);return check.VerifyData(challenge,signature,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);}
    public void Dispose(){signer.Dispose();key.Dispose();}
}
public static class LicenseInstallation
{
    public static string LicensePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cyno DS4.AI","Licensing","license.cynolicense");
    public static LicenseResult Check()
    {
        try{using var device=new DeviceIdentity();if(!File.Exists(LicensePath))return new(false,"Questo PC non è ancora autorizzato.");if(new FileInfo(LicensePath).Length>LicenseProtocol.MaxFileBytes)return new(false,"File licenza non valido.");var result=LicenseProtocol.Verify(File.ReadAllText(LicensePath),IssuerTrust.PublicKeyPem,device.Id,device.PublicKey,DateTimeOffset.UtcNow);return result.Valid&&!device.ProvePossession()?new(false,"Chiave del dispositivo non disponibile."):result;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException){return new(false,"Verifica PC non riuscita: "+ex.Message);}
    }
    public static LicenseResult Import(string path)
    {
        if(new FileInfo(path).Length>LicenseProtocol.MaxFileBytes)return new(false,"File troppo grande.");string text=File.ReadAllText(path);using var device=new DeviceIdentity();var result=LicenseProtocol.Verify(text,IssuerTrust.PublicKeyPem,device.Id,device.PublicKey,DateTimeOffset.UtcNow);if(!result.Valid||!device.ProvePossession())return result.Valid?new(false,"Chiave dispositivo non disponibile."):result;
        Directory.CreateDirectory(Path.GetDirectoryName(LicensePath)!);string temp=LicensePath+".tmp";File.WriteAllText(temp,text);File.Move(temp,LicensePath,true);return result;
    }
}
