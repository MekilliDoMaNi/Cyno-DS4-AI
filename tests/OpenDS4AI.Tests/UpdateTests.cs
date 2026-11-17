using System.Security.Cryptography;
using System.Text.Json;
using OpenDS4AI.Core;
using Xunit;
namespace OpenDS4AI.Tests;
public class UpdateTests
{
    private static UpdateRelease Release=>new("0.6.4","https://github.com/MekilliDoMaNi/Cyno-DS4-AI/releases/download/v0.6.4/Cyno-DS4.AI.exe",new string('A',64),10);
    private static string Sign(UpdateRelease r,ECDsa k){var p=JsonSerializer.SerializeToUtf8Bytes(r);return JsonSerializer.Serialize(new SignedUpdate(Convert.ToBase64String(p),Convert.ToBase64String(k.SignData(p,HashAlgorithmName.SHA256))));}
    [Fact]public void SignedManifestAcceptsOnlyTrustedKey(){using var k=ECDsa.Create();using var other=ECDsa.Create();var json=Sign(Release,k);Assert.Equal(Release,UpdateProtocol.Verify(json,k.ExportSubjectPublicKeyInfoPem()));Assert.Throws<CryptographicException>(()=>UpdateProtocol.Verify(json,other.ExportSubjectPublicKeyInfoPem()));}
    [Fact]public void ModifiedPayloadIsRejected(){using var k=ECDsa.Create();var signed=JsonSerializer.Deserialize<SignedUpdate>(Sign(Release,k))!;var bad=signed with {Payload=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Release with {Version="99.0.0"}))};Assert.Throws<CryptographicException>(()=>UpdateProtocol.Verify(JsonSerializer.Serialize(bad),k.ExportSubjectPublicKeyInfoPem()));}
    [Theory][InlineData("http://github.com/a/b")][InlineData("file:///C:/evil.exe")][InlineData("https://user:password@example.com/x")]
    public void UnsafeUrlsAreRejected(string url)=>Assert.Throws<InvalidDataException>(()=>UpdateProtocol.Https(url));
    [Fact]public void NoDowngradeOrSameVersion(){Assert.True(UpdateProtocol.IsNewer(Release,new Version(0,6,3,0)));Assert.False(UpdateProtocol.IsNewer(Release,new Version(0,6,5)));Assert.False(UpdateProtocol.IsNewer(Release,new Version(0,6,4)));}
    [Fact]public void InvalidFieldsAreRejected(){Assert.Throws<InvalidDataException>(()=>UpdateProtocol.Validate(Release with {Size=UpdateProtocol.MaxSize+1}));Assert.Throws<InvalidDataException>(()=>UpdateProtocol.Validate(Release with {Sha256="abc"}));Assert.Throws<InvalidDataException>(()=>UpdateProtocol.Validate(Release with {Version="latest"}));}
    [Fact]public void GithubRepositoryBecomesLatestAsset(){Assert.Equal("https://github.com/MekilliDoMaNi/Cyno-DS4-AI/releases/latest/download/update.json",UpdateProtocol.GitHubFeed("https://github.com/MekilliDoMaNi/Cyno-DS4-AI"));Assert.Throws<InvalidDataException>(()=>UpdateProtocol.GitHubFeed("https://github.com/MekilliDoMaNi?tab=repositories"));Assert.Throws<InvalidDataException>(()=>UpdateProtocol.GitHubFeed("https://evil.example/a/b"));}
    [Fact]public void HashSizeAndExecutableHeaderAreChecked()
    {
        string path=Path.GetTempFileName();try{byte[] bytes=[0x4d,0x5a,1,2];File.WriteAllBytes(path,bytes);var r=Release with {Size=4,Sha256=Convert.ToHexString(SHA256.HashData(bytes))};UpdateProtocol.VerifyFile(path,r);Assert.Throws<CryptographicException>(()=>UpdateProtocol.VerifyFile(path,r with {Sha256=new string('A',64)}));Assert.Throws<InvalidDataException>(()=>UpdateProtocol.VerifyFile(path,r with {Size=5}));File.WriteAllBytes(path,[0,0,1,2]);Assert.Throws<CryptographicException>(()=>UpdateProtocol.VerifyFile(path,r));}finally{File.Delete(path);}
    }
}