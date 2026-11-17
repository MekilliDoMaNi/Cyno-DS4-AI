using System.Text.Json;
using OpenDS4AI.Core;
using Xunit;
namespace OpenDS4AI.Tests;
public class ShoulderTests
{
    [Theory]
    [InlineData(1,Buttons.LB,255,0)]
    [InlineData(1,Buttons.RB,0,255)]
    [InlineData(2,Buttons.RB,255,0)]
    [InlineData(2,Buttons.LB,0,255)]
    public void ShoulderRoutesToCorrectTrigger(int layout,Buttons button,int lt,int rt)
    {
        var result=new Profile{ShoulderLayout=layout}.ApplyOutputLayout(new(0,0,0,0,0,0,button|Buttons.A));
        Assert.Equal((byte)lt,result.LT);Assert.Equal((byte)rt,result.RT);Assert.Equal(Buttons.A,result.Buttons);
    }
    [Theory][InlineData(1,Buttons.LB)][InlineData(2,Buttons.RB)]
    public void TriggersReturnToShouldersWithoutLosingSimultaneousInputs(int layout,Buttons aimShoulder)
    {
        var result=new Profile{ShoulderLayout=layout}.ApplyOutputLayout(new(0,0,0,0,255,255,Buttons.LB|Buttons.RB));
        Assert.Equal((byte)255,result.LT);Assert.Equal((byte)255,result.RT);
        Assert.Equal(Buttons.LB|Buttons.RB,result.Buttons);Assert.True(result.Buttons.HasFlag(aimShoulder));
    }
    [Fact]public void L1ActivationUsesPhysicalButtonEvenWithOutputSwap()
    {
        var c=new AssistSettings{AimEnabled=true,ActivateOnL1=true};var e=new AssistEngine();
        PadState physical=new(0,0,0,0,0,0,Buttons.LB);
        var output=new Profile{ShoulderLayout=2}.ApplyOutputLayout(physical);
        Assert.True(e.Process(output,c,new(150,100),200,0,0,0,physical).RX>0);
        Assert.Equal(0,e.Process(output,c,new(150,100),200,0.1,0,0,default(PadState)).RX);
    }
    [Fact]public void OldProfilesKeepL2AndOriginalLayout()
    {
        var c=JsonSerializer.Deserialize<AssistSettings>("{\"AimEnabled\":true}")!;
        Assert.False(c.ActivateOnL1);Assert.True(c.ActivateOnLeftTrigger);
        var p=JsonSerializer.Deserialize<Profile>("{\"Name\":\"Old\"}")!;
        PadState input=new(0,0,0,0,127,200,Buttons.LB);Assert.Equal(input,p.ApplyOutputLayout(input));
    }
    [Fact]public void NewSelectionsRoundTripAndInvalidLayoutIsRejected()
    {
        var p=new Profile{ShoulderLayout=2};Assert.Equal(2,JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(p))!.ShoulderLayout);
        var c=new AssistSettings{ActivateOnL1=true};Assert.True(JsonSerializer.Deserialize<AssistSettings>(JsonSerializer.Serialize(c))!.ActivateOnL1);
        Assert.Throws<ArgumentException>(()=>(p with {ShoulderLayout=3}).Validate());
    }
}
