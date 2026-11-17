using OpenDS4AI.Core;
using Xunit;
namespace OpenDS4AI.Tests;
public class CoreTests
{
    private static byte[] Report(){var r=new byte[64];r[0]=1;r[1]=r[2]=r[3]=r[4]=128;r[8]=8;return r;}
    [Fact] public void NeutralIsZero(){Assert.Equal(default, DualSenseParser.Parse(Report()));}
    [Fact] public void MapsButtonsHatAndTriggers(){var r=Report();r[8]=0x21;r[5]=255;var s=DualSenseParser.Parse(r);Assert.Equal(Buttons.A|Buttons.Up|Buttons.Right,s.Buttons);Assert.Equal(255,s.LT);}
    [Fact] public void AxisEndpoints(){var r=Report();r[1]=0;r[2]=255;var s=DualSenseParser.Parse(r);Assert.Equal(-1,s.LX);Assert.Equal(-1,s.LY);}
    [Theory][InlineData(0)][InlineData(63)]public void RejectsShortReport(int length)=>Assert.Throws<ArgumentException>(()=>DualSenseParser.Parse(new byte[length]));
    [Fact] public void RejectsBluetooth(){var r=Report();r[0]=0x31;Assert.Throws<ArgumentException>(()=>DualSenseParser.Parse(r));}
    [Fact] public void DeadZoneAndInversion(){var p=new Profile("Test",0.1f,true);var s=p.Apply(new(0.05f,1,0,0,0,0,0));Assert.Equal(0,s.LX);Assert.Equal(-1,s.LY);}
    [Theory][InlineData(-0.1f)][InlineData(1f)][InlineData(float.NaN)]public void RejectsInvalidProfile(float d)=>Assert.Throws<ArgumentException>(()=>new Profile("Test",d).Validate());
    [Fact] public void ProfileRoundTrip(){var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());var path=Path.Combine(dir,"profile.json");try{var p=new Profile("Test",0.2f,true){Remap=new(){{Buttons.A,Buttons.B}}};ProfileStore.Save(path,p);var loaded=ProfileStore.Load(path);Assert.Equal(p.Name,loaded.Name);Assert.Equal(p.DeadZone,loaded.DeadZone);Assert.Equal(p.InvertY,loaded.InvertY);Assert.Equal(Buttons.B,loaded.Remap[Buttons.A]);}finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}}
    [Fact] public void CorruptProfileIsNotOverwritten(){var path=Path.GetTempFileName();try{File.WriteAllText(path,"broken");Assert.Throws<System.Text.Json.JsonException>(()=>ProfileStore.Load(path));Assert.Equal("broken",File.ReadAllText(path));}finally{File.Delete(path);}}
    [Fact] public void PreviewCapturesState(){using var v=new PreviewController();var s=new PadState(1,0,0,0,0,0,Buttons.A);v.Submit(s);Assert.Equal(s,v.Last);}
}