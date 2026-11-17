using System.Security.Cryptography;
using System.Text.Json;
namespace OpenDS4AI.Core;
public sealed record UpdateRelease(string Version,string Url,string Sha256,long Size,string Notes="");
public sealed record SignedUpdate(string Payload,string Signature);
public static class UpdateProtocol
{
    public const long MaxSize=600L*1024*1024;
    public static Uri Https(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("Indirizzo aggiornamenti HTTPS non valido.");
        return uri;
    }
    public static UpdateRelease Verify(string json,string publicKey)
    {
        if(json.Length>65536)throw new InvalidDataException("Manifest troppo grande.");
        var envelope=JsonSerializer.Deserialize<SignedUpdate>(json)??throw new InvalidDataException("Manifest vuoto.");
        byte[] payload=Convert.FromBase64String(envelope.Payload),signature=Convert.FromBase64String(envelope.Signature);
        using var key=ECDsa.Create();key.ImportFromPem(publicKey);
        if(!key.VerifyData(payload,signature,HashAlgorithmName.SHA256))throw new CryptographicException("Firma aggiornamento non valida.");
        var release=JsonSerializer.Deserialize<UpdateRelease>(payload)??throw new InvalidDataException("Versione mancante.");
        Validate(release);return release;
    }
    public static void Validate(UpdateRelease release)
    {
        if(!System.Version.TryParse(release.Version,out _)||release.Size<2||release.Size>MaxSize||release.Sha256 is null||release.Sha256.Length!=64||!release.Sha256.All(Uri.IsHexDigit)||release.Notes is null||release.Notes.Length>4000)throw new InvalidDataException("Dati aggiornamento non validi.");
        Https(release.Url);
    }
    public static bool IsNewer(UpdateRelease release,Version installed)=>System.Version.Parse(release.Version)>installed;
    public static void VerifyFile(string path,UpdateRelease release)
    {
        Validate(release);using var file=File.OpenRead(path);
        if(file.Length!=release.Size)throw new InvalidDataException("Dimensione aggiornamento non valida.");
        if(!Convert.ToHexString(SHA256.HashData(file)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new CryptographicException("File aggiornamento alterato o incompleto.");
        file.Position=0;if(file.ReadByte()!=0x4d||file.ReadByte()!=0x5a)throw new InvalidDataException("Eseguibile Windows non valido.");
    }
    public static string GitHubFeed(string repository)
    {
        var uri=Https(repository.Trim().TrimEnd('/'));
        if(!uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))throw new InvalidDataException("Inserisci il link del repository GitHub.");
        var segments=uri.AbsolutePath.Trim('/').Split('/');
        if(segments.Length!=2||segments.Any(s=>s.Length==0||!s.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_' or '.'))||segments.Any(s=>s is "." or ".."))throw new InvalidDataException("Repository GitHub non valido.");
        return $"https://github.com/{segments[0]}/{segments[1]}/releases/latest/download/update.json";
    }
}