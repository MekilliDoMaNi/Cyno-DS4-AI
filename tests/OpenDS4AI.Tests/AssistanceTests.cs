using OpenDS4AI.Core;
using Xunit;
namespace OpenDS4AI.Tests;
public class AssistanceTests
{
    [Fact] public void ChoosesClosestMarkerAndRejectsOutsideFov()
    {
        byte[] image = new byte[20 * 20 * 4];
        void Pixel(int x, int y) { int i=(y*20+x)*4;image[i]=205;image[i+2]=152; }
        Pixel(12,10);Pixel(19,19);
        Assert.Equal(new TargetPoint(12,10),ColorTargetDetector.Detect(image,20,20,new(){FovRadius=4,ColorTolerance=0}));
        Assert.Null(ColorTargetDetector.Detect(new byte[1600],20,20,new(){FovRadius=4,ColorTolerance=0}));
    }
    [Fact] public void AimRequiresActivationAndClearsImmediatelyOnTargetLoss()
    {
        var engine=new AssistEngine();var cfg=new AssistSettings{AimEnabled=true,Smooth=0,StableAim=false};
        Assert.Equal(0,engine.Process(default,cfg,new(75,50),100,0).RX);
        var pressed=new PadState(0,0,0,0,255,0,Buttons.None);
        Assert.Equal(0.5f,engine.Process(pressed,cfg,new(75,50),100,0.1).RX);
        Assert.Equal(0,engine.Process(pressed,cfg,null,100,0.2).RX);
    }
    [Fact] public void MacrosPreserveOtherButtonsAndResetOnRelease()
    {
        var engine=new AssistEngine();var cfg=new AssistSettings{RapidFire=true,RapidPeriodMs=100,BunnyHop=true,BunnyPeriodMs=100};
        var input=new PadState(0,0,0,0,0,255,Buttons.A|Buttons.B);
        Assert.Equal(input,engine.Process(input,cfg,null,100,0));
        var released=engine.Process(input,cfg,null,100,0.075);Assert.Equal(0,released.RT);Assert.Equal(Buttons.B,released.Buttons);
        engine.Process(default,cfg,null,100,0.08);
        Assert.Equal(input,engine.Process(input,cfg,null,100,0.09));
    }
    [Fact] public void RecoilOnlyChangesRightStickWhenFiringAndClamps()
    {
        var engine=new AssistEngine();var cfg=new AssistSettings{AntiRecoil=true,RecoilVertical=0.5f,RecoilHorizontal=0.5f};
        var s=new PadState(1,1,0.9f,-0.9f,0,255,Buttons.X);
        var result=engine.Process(s,cfg,null,100,0);Assert.Equal(1,result.RX);Assert.Equal(-1,result.RY);Assert.Equal(s.LX,result.LX);Assert.Equal(s.Buttons,result.Buttons);
    }
    [Fact] public void RejectsBadConfigurationAndBuffers()
    {
        Assert.Throws<ArgumentException>(()=>new AssistSettings{MarkerHex="bad"}.Validate());
        Assert.Throws<ArgumentException>(()=>new AssistSettings{Smooth=float.NaN}.Validate());
        Assert.Throws<ArgumentException>(()=>ColorTargetDetector.Detect(new byte[3],10,10,new()));
    }
}
