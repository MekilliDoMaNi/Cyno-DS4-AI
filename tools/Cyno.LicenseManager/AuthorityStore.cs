using System.IO;
using System.Security.Cryptography;using System.Text;using Cyno.Licensing;using Cyno.Licensing.Windows;
namespace Cyno.LicenseManager;
public static class AuthorityStore
{
    private static byte[] Entropy=>Encoding.UTF8.GetBytes("CynoDS4AI.Issuer.v1");
    public static string PrivatePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cyno DS4.AI","LicenseAuthority","issuer.dpapi");
    public static ECDsa Load()
    {
        if(!File.Exists(PrivatePath))throw new InvalidOperationException("Chiave privata assente su questo profilo Windows. Ripristina il backup cifrato del proprietario.");byte[] plain=ProtectedData.Unprotect(File.ReadAllBytes(PrivatePath),Entropy,DataProtectionScope.CurrentUser);var key=ECDsa.Create();
        try{key.ImportPkcs8PrivateKey(plain,out int read);if(read!=plain.Length||LicenseProtocol.PublicKeyId(key.ExportSubjectPublicKeyInfoPem())!=LicenseProtocol.PublicKeyId(IssuerTrust.PublicKeyPem))throw new CryptographicException("Chiave privata non corrispondente alla build Cyno.");return key;}catch{key.Dispose();throw;}finally{CryptographicOperations.ZeroMemory(plain);}
    }
    public static string Issue(DeviceRequest request,string customer,DateTimeOffset? expires=null)
    {
        using var key=Load();var claims=LicenseProtocol.CreateClaims(request,customer,IssuerTrust.PublicKeyPem,DateTimeOffset.UtcNow,expires);string license=LicenseProtocol.Sign(claims,key);
        string log=Path.Combine(Path.GetDirectoryName(PrivatePath)!,"issuance.log");File.AppendAllText(log,System.Text.Json.JsonSerializer.Serialize(new{claims.LicenseId,claims.DeviceId,claims.Customer,claims.IssuedUtc,claims.ExpiresUtc})+"\n");return license;
    }
    public static string ExportBackup(string password){if(password.Length<12)throw new ArgumentException("Usa una password di almeno 12 caratteri.");using var key=Load();return key.ExportEncryptedPkcs8PrivateKeyPem(password,new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc,HashAlgorithmName.SHA256,200000));}
    public static void RestoreBackup(string encryptedPem,string password)
    {
        using var key=ECDsa.Create();key.ImportFromEncryptedPem(encryptedPem,password);if(LicenseProtocol.PublicKeyId(key.ExportSubjectPublicKeyInfoPem())!=LicenseProtocol.PublicKeyId(IssuerTrust.PublicKeyPem))throw new CryptographicException("Questo backup non corrisponde alla chiave della tua app.");byte[] bytes=key.ExportPkcs8PrivateKey();try{byte[] protectedBytes=ProtectedData.Protect(bytes,Entropy,DataProtectionScope.CurrentUser);Directory.CreateDirectory(Path.GetDirectoryName(PrivatePath)!);string temp=PrivatePath+".tmp";File.WriteAllBytes(temp,protectedBytes);File.Move(temp,PrivatePath,true);}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
}
