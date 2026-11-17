using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using OpenDS4AI.Core;
try
{
    var keyPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cyno DS4.AI","UpdateAuthority","update-signing.dpapi");
    using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
    if(File.Exists(keyPath))
    {
        var bytes=ProtectedData.Unprotect(File.ReadAllBytes(keyPath),null,DataProtectionScope.CurrentUser);
        try{key.ImportPkcs8PrivateKey(bytes,out _);}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    else
    {
        if(args.Length!=2||args[0]!="init")throw new InvalidOperationException("Chiave di firma mancante. Non generare una nuova chiave per aggiornare un'app già distribuita.");
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);var bytes=key.ExportPkcs8PrivateKey();
        try{File.WriteAllBytes(keyPath,ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser));}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    if(args.Length==2&&args[0]=="init")
    {
        File.WriteAllText(args[1],key.ExportSubjectPublicKeyInfoPem());Console.WriteLine("Chiave pubblica esportata. Firma privata protetta nel solo account proprietario.");return 0;
    }
    if(args.Length!=4){Console.WriteLine("Uso: Cyno.UpdatePublisher.exe <exe> <versione> <repository-https> <cartella-output>");return 1;}
    string exe=Path.GetFullPath(args[0]),version=args[1],folder=Path.GetFullPath(args[3]);
    string feed=UpdateProtocol.GitHubFeed(args[2]);
    if(!Version.TryParse(version,out var requested)||requested.ToString(3)!=Version.Parse(FileVersionInfo.GetVersionInfo(exe).ProductVersion!.Split('+')[0]).ToString(3))throw new InvalidDataException("Versione indicata diversa dall'eseguibile.");
    string url=feed.Replace("/latest/download/update.json",$"/download/v{version}/Cyno-DS4.AI.exe");
    using var file=File.OpenRead(exe);var release=new UpdateRelease(version,url,Convert.ToHexString(SHA256.HashData(file)),file.Length,"Aggiornamento Cyno DS4.AI "+version);
    UpdateProtocol.Validate(release);var payload=JsonSerializer.SerializeToUtf8Bytes(release);
    var envelope=new SignedUpdate(Convert.ToBase64String(payload),Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256)));
    Directory.CreateDirectory(folder);file.Dispose();File.Copy(exe,Path.Combine(folder,"Cyno-DS4.AI.exe"),true);
    File.WriteAllText(Path.Combine(folder,"update.json"),JsonSerializer.Serialize(envelope,new JsonSerializerOptions{WriteIndented=true}));
    File.WriteAllText(Path.Combine(folder,"updates.json"),JsonSerializer.Serialize(new{FeedUrl=feed,AutoCheck=true},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine("Creati Cyno-DS4.AI.exe e update.json: allegali alla Release pubblica v"+version+". updates.json configura il primo pacchetto clienti.");return 0;
}
catch(Exception ex){Console.Error.WriteLine("Pubblicazione non preparata: "+ex.Message);return 1;}