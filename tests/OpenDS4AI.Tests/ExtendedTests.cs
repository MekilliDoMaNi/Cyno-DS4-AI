using OpenDS4AI.Core;using Xunit;
namespace OpenDS4AI.Tests;
public class ExtendedTests
{
    [Fact]public void RemappingIsSimultaneousAndPreservesOtherButtons(){var p=new Profile(){Remap=new(){{Buttons.A,Buttons.B},{Buttons.B,Buttons.A}}};var s=p.Apply(new(0,0,0,0,0,0,Buttons.A|Buttons.X));Assert.Equal(Buttons.B|Buttons.X,s.Buttons);Assert.Equal(Buttons.A|Buttons.B,p.Apply(s with {Buttons=Buttons.A|Buttons.B}).Buttons);}
    [Fact]public void DisabledMappingSuppressesOnlySource(){var p=new Profile(){Remap=new(){{Buttons.A,Buttons.None}}};Assert.Equal(Buttons.X,p.Apply(new(0,0,0,0,0,0,Buttons.A|Buttons.X)).Buttons);}
    [Fact]public void RejectsCompositeMapping(){Assert.Throws<ArgumentException>(()=>new Profile(){Remap=new(){{Buttons.A|Buttons.B,Buttons.X}}}.Validate());}
    [Fact]public void PingHonorsDelayCooldownAndRelease(){var e=new AssistEngine();var c=new AssistSettings{AutoPing=true,PingDelayMs=250,PingCooldownMs=1000};var s=new PadState(0,0,0,0,255,0,0);Assert.Equal(Buttons.None,e.Process(s,c,null,100,0).Buttons);Assert.Equal(Buttons.RS,e.Process(s,c,null,100,0.26).Buttons);Assert.Equal(Buttons.None,e.Process(s,c,null,100,0.5).Buttons);Assert.Equal(Buttons.RS,e.Process(s,c,null,100,1.3).Buttons);Assert.Equal(Buttons.None,e.Process(default,c,null,100,1.31).Buttons);}
    [Fact]public void SlideIsBoundedAndRequiresNewPress(){var e=new AssistEngine();var c=new AssistSettings{CancelSlide=true,SlideMs=80};var s=new PadState(0,0,0,0,0,0,Buttons.B);Assert.True(e.Process(s,c,null,100,0).Buttons.HasFlag(Buttons.LS));Assert.False(e.Process(s,c,null,100,0.1).Buttons.HasFlag(Buttons.LS));e.Process(default,c,null,100,0.11);Assert.True(e.Process(s,c,null,100,0.12).Buttons.HasFlag(Buttons.LS));}
    [Fact]public void SwapDoesNotEraseManualTriangle(){var e=new AssistEngine();var c=new AssistSettings{SpamSwap=true,SwapPeriodMs=100};var s=new PadState(0,0,0,0,0,0,Buttons.B|Buttons.Y);e.Process(s,c,null,100,0);Assert.True(e.Process(s,c,null,100,0.075).Buttons.HasFlag(Buttons.Y));}
    [Fact]public void BluetoothKnownNeutralReport(){var r=Convert.FromHexString("310080808080000000080000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000065d1e334");Assert.Equal(default,DualSenseParser.Parse(r));r[3]^=1;Assert.Throws<ArgumentException>(()=>DualSenseParser.Parse(r));}
    [Fact]public void BluetoothRejectsInvalidCrc(){var r=new byte[78];r[0]=0x31;Assert.Throws<ArgumentException>(()=>DualSenseParser.Parse(r));}
}
