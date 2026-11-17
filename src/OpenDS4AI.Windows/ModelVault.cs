using System.Security.Cryptography;using System.Text;using System.Text.Json;
namespace OpenDS4AI.Windows;
public static class ModelVault
{
    private static readonly byte[] Entropy=Encoding.UTF8.GetBytes("CynoDS4AI.LocalModelVault.v1");
    private sealed record Envelope(int Version,string Name,string Sha256,byte[] WrappedKey,byte[] Nonce,byte[] Ciphertext,byte[] Tag);
    public static string ModelDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cyno DS4.AI","Models");
    public static string EnsureBundledModel()
    {
        string path=Path.Combine(ModelDirectory,"person-generic.cynomodel");if(File.Exists(path))return path;
        using var source=typeof(ModelVault).Assembly.GetManifestResourceStream("CynoDS4AI.Models.PersonGeneric")??throw new FileNotFoundException("Modello generico incluso non trovato.");using var memory=new MemoryStream();source.CopyTo(memory);byte[] bytes=memory.ToArray();try{Protect(bytes,path,"YOLOv8n pubblico / generico COCO");}finally{CryptographicOperations.ZeroMemory(bytes);}return path;
    }
    public static string Import(string source)
    {
        if(source.EndsWith(".cynomodel",StringComparison.OrdinalIgnoreCase)){byte[] check=Load(source);CryptographicOperations.ZeroMemory(check);return source;}
        byte[] bytes=File.ReadAllBytes(source);try{string hash=Convert.ToHexString(SHA256.HashData(bytes));string path=Path.Combine(ModelDirectory,"import-"+hash[..16]+".cynomodel");Protect(bytes,path,Path.GetFileName(source));return path;}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    public static void Protect(byte[] model,string path,string name)
    {
        if(model.Length<16||model.Length>512*1024*1024)throw new InvalidDataException("Dimensione modello non supportata.");
        byte[] key=RandomNumberGenerator.GetBytes(32),nonce=RandomNumberGenerator.GetBytes(12),tag=new byte[16],cipher=new byte[model.Length];
        try
        {
            using var aes=new AesGcm(key,16);aes.Encrypt(nonce,model,cipher,tag,Encoding.UTF8.GetBytes(name));
            var envelope=new Envelope(1,name,Convert.ToHexString(SHA256.HashData(model)),ProtectedData.Protect(key,Entropy,DataProtectionScope.CurrentUser),nonce,cipher,tag);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,JsonSerializer.Serialize(envelope));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        finally{CryptographicOperations.ZeroMemory(key);}
    }
    public static byte[] Load(string path)
    {
        if(new FileInfo(path).Length>720L*1024*1024)throw new InvalidDataException("Pacchetto modello troppo grande.");
        var e=JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path))??throw new InvalidDataException("Pacchetto modello vuoto.");
        if(e.Version!=1||string.IsNullOrWhiteSpace(e.Name)||e.WrappedKey==null||e.WrappedKey.Length>8192||e.Nonce?.Length!=12||e.Tag?.Length!=16||e.Ciphertext==null||e.Ciphertext.Length<16)throw new InvalidDataException("Formato modello protetto non valido.");
        byte[] key=ProtectedData.Unprotect(e.WrappedKey,Entropy,DataProtectionScope.CurrentUser),plain=new byte[e.Ciphertext.Length];
        try{using var aes=new AesGcm(key,16);aes.Decrypt(e.Nonce,e.Ciphertext,e.Tag,plain,Encoding.UTF8.GetBytes(e.Name));if(!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Convert.ToHexString(SHA256.HashData(plain))),Encoding.ASCII.GetBytes(e.Sha256)))throw new CryptographicException("Integrità modello non valida.");return plain;}
        catch{CryptographicOperations.ZeroMemory(plain);throw;}finally{CryptographicOperations.ZeroMemory(key);}
    }
}
