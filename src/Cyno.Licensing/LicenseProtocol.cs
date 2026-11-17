using System.Security.Cryptography;using System.Text;using System.Text.Json;
namespace Cyno.Licensing;
public sealed record DeviceRequest(int Schema,string Product,string RequestId,string DeviceId,string MachineHash,string DevicePublicKey,DateTimeOffset CreatedUtc,string KeyProtection);
public sealed record LicenseClaims(int Schema,string Product,string KeyId,string LicenseId,string DeviceId,string DevicePublicKey,string Customer,DateTimeOffset IssuedUtc,DateTimeOffset NotBeforeUtc,DateTimeOffset? ExpiresUtc);
public sealed record SignedEnvelope(string Payload,string Signature);
public sealed record LicenseResult(bool Valid,string Message,LicenseClaims? License=null);
public static class LicenseProtocol
{
    public const string Product="CynoDS4AI";public const int MaxFileBytes=32768;
    private static readonly JsonSerializerOptions Json=new(){MaxDepth=8};
    public static string PublicKeyId(string pem){using var key=ECDsa.Create();key.ImportFromPem(pem);return Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo()))[..24];}
    public static string DeviceId(string machineHash,string publicKey)
    {
        if(machineHash.Length!=64||!machineHash.All(Uri.IsHexDigit))throw new InvalidDataException("Identità Windows non valida.");
        byte[] pub=Convert.FromBase64String(publicKey);using var key=ECDsa.Create();key.ImportSubjectPublicKeyInfo(pub,out int read);if(read!=pub.Length||key.KeySize!=256)throw new InvalidDataException("Chiave dispositivo non valida.");return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Product+"|device-v1|"+machineHash.ToUpperInvariant()+"|"+publicKey)));
    }
    public static string Sign<T>(T payload,ECDsa key){byte[] data=JsonSerializer.SerializeToUtf8Bytes(payload,Json);return JsonSerializer.Serialize(new SignedEnvelope(Convert.ToBase64String(data),Convert.ToBase64String(key.SignData(data,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))),Json);}
    private static (byte[] Payload,byte[] Signature) Decode(string text)
    {
        if(Encoding.UTF8.GetByteCount(text)>MaxFileBytes)throw new InvalidDataException("File licenza troppo grande.");var envelope=JsonSerializer.Deserialize<SignedEnvelope>(text,Json)??throw new InvalidDataException("File vuoto.");byte[] payload=Convert.FromBase64String(envelope.Payload),signature=Convert.FromBase64String(envelope.Signature);if(payload.Length>16384||signature.Length!=64)throw new InvalidDataException("Formato firma non valido.");return(payload,signature);
    }
    private static bool ValidSignature(byte[] payload,byte[] signature,string publicKey,bool pem)
    {
        using var key=ECDsa.Create();if(pem)key.ImportFromPem(publicKey);else{byte[] bytes=Convert.FromBase64String(publicKey);key.ImportSubjectPublicKeyInfo(bytes,out int read);if(read!=bytes.Length)return false;}return key.KeySize==256&&key.VerifyData(payload,signature,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
    public static DeviceRequest ReadRequest(string text)
    {
        var (payload,signature)=Decode(text);var request=JsonSerializer.Deserialize<DeviceRequest>(payload,Json)??throw new InvalidDataException("Richiesta vuota.");
        if(request.Schema!=1||request.Product!=Product||!Guid.TryParseExact(request.RequestId,"N",out _)||request.DeviceId!=DeviceId(request.MachineHash,request.DevicePublicKey)||!ValidSignature(payload,signature,request.DevicePublicKey,false))throw new InvalidDataException("Richiesta di attivazione non valida o manomessa.");return request;
    }
    public static LicenseResult Verify(string text,string issuerPublicKey,string expectedDeviceId,string expectedDevicePublicKey,DateTimeOffset now)
    {
        try
        {
            var(payload,signature)=Decode(text);if(!ValidSignature(payload,signature,issuerPublicKey,true))return new(false,"Firma licenza non valida.");
            var claim=JsonSerializer.Deserialize<LicenseClaims>(payload,Json)??throw new InvalidDataException("Licenza vuota.");
            if(claim.Schema!=1||claim.Product!=Product||claim.KeyId!=PublicKeyId(issuerPublicKey)||!Guid.TryParseExact(claim.LicenseId,"N",out _)||string.IsNullOrWhiteSpace(claim.Customer)||claim.Customer.Length>200)return new(false,"Licenza non compatibile con questa app.");
            if(!string.Equals(claim.DeviceId,expectedDeviceId,StringComparison.Ordinal)||!string.Equals(claim.DevicePublicKey,expectedDevicePublicKey,StringComparison.Ordinal))return new(false,"Questa licenza appartiene a un altro PC o profilo Windows.");
            if(claim.IssuedUtc>now.AddMinutes(5)||claim.NotBeforeUtc>now.AddMinutes(1))return new(false,"Licenza non ancora valida. Verifica data e ora del PC.");
            if(claim.ExpiresUtc is {} expiry&&(expiry<=claim.NotBeforeUtc||now>=expiry))return new(false,"Licenza scaduta.");
            return new(true,"PC autorizzato.",claim);
        }
        catch(Exception ex) when(ex is JsonException or FormatException or CryptographicException or InvalidDataException or ArgumentException or NullReferenceException){return new(false,"File licenza non valido o danneggiato.");}
    }
    public static LicenseClaims CreateClaims(DeviceRequest request,string customer,string issuerPublicKey,DateTimeOffset now,DateTimeOffset? expiry=null)
    {
        if(string.IsNullOrWhiteSpace(customer)||customer.Length>200||expiry is {} end&&end<=now)throw new ArgumentException("Nome o scadenza non validi.");return new(1,Product,PublicKeyId(issuerPublicKey),Guid.NewGuid().ToString("N"),request.DeviceId,request.DevicePublicKey,customer.Trim(),now,now.AddMinutes(-1),expiry);
    }
}
