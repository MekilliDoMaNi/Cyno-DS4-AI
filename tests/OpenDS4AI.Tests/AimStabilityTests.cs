using OpenDS4AI.Core;using Xunit;
namespace OpenDS4AI.Tests;
public class AimStabilityTests
{
    private static PadState Held=>new(0,0,0,0,255,0,Buttons.None);
    [Fact]public void StableAimNeverExceedsConfiguredRadialLimit(){var e=new AssistEngine();var c=new AssistSettings{AimEnabled=true,Strength=5,AimMaxOutput=0.25f};for(int n=0;n<500;n++){var s=e.Process(Held,c,new(190,190),200,n/1000.0,1000,0.05);Assert.InRange(MathF.Sqrt(s.RX*s.RX+s.RY*s.RY),0,0.25001f);}}
    [Fact]public void CentreAndTargetLossStopCorrectionImmediately(){var e=new AssistEngine();var c=new AssistSettings{AimEnabled=true};e.Process(Held,c,new(160,100),200,0);Assert.Equal(0,e.Process(Held,c,new(100,100),200,0.02).RX);Assert.Equal(0,e.Process(Held,c,null,200,0.03).RX);}
    [Fact]public void CrossingTargetCannotLeaveCorrectionPushingAway(){var e=new AssistEngine();var c=new AssistSettings{AimEnabled=true};for(int n=0;n<100;n++)e.Process(Held,c,new(150,100),200,n/1000.0,1000,0.1);var s=e.Process(Held,c,new(90,100),200,0.1,1100,0.05);Assert.True(s.RX<=0);}
    [Fact]public void InvertedCorrectionAndManualOverrideAreExplicit(){var e=new AssistEngine();var c=new AssistSettings{AimEnabled=true,InvertAimY=true};Assert.True(e.Process(Held,c,new(100,130),200,0).RY>0);var manual=Held with {RX=1};Assert.Equal(1,e.Process(manual,c,new(20,100),200,0.1).RX);}
    [Fact]public void LowConfidenceMayRetainButNotAcquire(){var options=new WarzoneOptions(Confidence:0.6f,Anchor:0.5f,Smoothing:0);var engine=new WarzoneEngine();var weak=new[]{new DetectionBox(150,150,30,80,0.45f)};Assert.Null(engine.Select(weak,300,150,0,options));Assert.NotNull(engine.Select(new[]{new DetectionBox(150,150,30,80,0.9f)},300,150,0.1,options));Assert.NotNull(engine.Select(weak,300,150,0.2,options));}
    [Fact]public void CloserNeighbourMustBeConfirmedBeforeSwitch(){var options=new WarzoneOptions(Anchor:0.5f,Smoothing:0);var e=new WarzoneEngine();var original=new DetectionBox(125,150,15,50,0.9f);e.Select(new[]{original},300,150,0,options);var both=new[]{original,new DetectionBox(149,150,15,50,0.95f)};Assert.Equal(new TargetPoint(125,150),e.Select(both,300,150,0.05,options));Assert.Equal(new TargetPoint(125,150),e.Select(both,300,150,0.1,options));Assert.Equal(new TargetPoint(149,150),e.Select(both,300,150,0.15,options));}
    [Fact]public void StaleObservationAndReleasedActivationGiveNoCorrection(){var e=new AssistEngine();var c=new AssistSettings{AimEnabled=true};Assert.Equal(0,e.Process(Held,c,new(150,100),200,0,1000,0.3).RX);Assert.Equal(0,e.Process(default,c,new(150,100),200,0.1,1100,0.05).RX);}
}
