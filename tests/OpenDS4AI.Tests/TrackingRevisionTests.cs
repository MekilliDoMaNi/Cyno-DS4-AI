using OpenDS4AI.Core;
using Xunit;
namespace OpenDS4AI.Tests;
public class TrackingRevisionTests
{
    [Fact]public void WiderContextPreservesPhysicalAimErrorAndFov()
    {
        Assert.Equal(600,VisionGeometry.CaptureSize(150,true,2560,1440));
        Assert.Equal(new TargetPoint(180,120),VisionGeometry.ToControlSpace(new(330,270),600,300));
        Assert.Equal(new TargetPoint(150,150),VisionGeometry.ToControlSpace(new(300,300),600,300));
        Assert.Equal(900,VisionGeometry.CaptureSize(400,true,1600,900));
        Assert.Throws<ArgumentException>(()=>VisionGeometry.CaptureSize(400,true,640,480));
    }
    [Fact]public void ModerateManualInputDoesNotEraseAssistanceAtHighReportRate()
    {
        float Run(int hz){var e=new AssistEngine();var c=new AssistSettings{AimEnabled=true,Strength=1,AimMaxOutput=0.3f};PadState held=new(0,0,0.6f,0,255,0,Buttons.None);for(int n=0;n<hz;n++)e.Process(held,c,new(180,100),200,n/(double)hz,1000,0.05);return e.LastAimX;}
        float slow=Run(120),fast=Run(1000);Assert.InRange(fast,0.22f,0.24f);Assert.InRange(Math.Abs(slow-fast),0,0.001f);
    }
    [Fact]public void KeepingTargetDoesNotSwitchToCloserNeighbour()
    {
        var e=new WarzoneEngine();var options=new WarzoneOptions(Anchor:0.5f,Smoothing:0,KeepTarget:true);var old=new DetectionBox(115,150,20,70,0.9f);e.Select([old],300,150,0,options);
        for(int n=1;n<=10;n++)Assert.Equal(new TargetPoint(115,150),e.Select([old,new(150,150,20,70,0.95f)],300,150,n*0.02,options));
    }
    [Fact]public void UnrelatedReplacementBreaksTrackWithoutInterpolatingBetweenPeople()
    {
        var e=new WarzoneEngine();var o=new WarzoneOptions(Anchor:0.5f,Smoothing:0,KeepTarget:true);e.Select([new(80,150,20,60,0.9f)],300,150,0,o);
        Assert.Null(e.Select([new(220,150,20,60,0.9f)],300,150,0.05,o));
        Assert.Equal(new TargetPoint(220,150),e.Select([new(220,150,20,60,0.9f)],300,150,0.1,o));
    }
    [Fact]public void HeadEstimateRequiresVisibleTopAndUsesRelativeHeight()
    {
        var e=new WarzoneEngine();var o=new WarzoneOptions(Anchor:0.12f,Smoothing:0,KeepTarget:true,RequireVisibleTop:true);
        Assert.Null(e.Select([new(150,25,30,80,0.9f)],300,150,0,o));
        Assert.Equal(new TargetPoint(150,119.6f),e.Select([new(150,150,30,80,0.9f)],300,150,0.1,o));
    }
    [Fact]public void LatencyCompensationBrakesApproachingErrorWithoutReversing()
    {
        float Run(bool compensate){var e=new AssistEngine();var c=new AssistSettings{AimEnabled=true,CompensateVisionLatency=compensate,AimDamping=0,Strength=1};PadState held=new(0,0,0,0,255,0,Buttons.None);e.Process(held,c,new(150,100),200,0,1000,0.05);e.Process(held,c,new(110,100),200,0.1,1100,0.1);return e.LastAimX;}
        Assert.True(Run(false)>0);Assert.Equal(0,Run(true));
    }
    [Fact]public void LatencyPredictionStillStopsForLostOrOldFrames()
    {
        var e=new AssistEngine();var c=new AssistSettings{AimEnabled=true,CompensateVisionLatency=true};PadState held=new(0,0,0,0,255,0,Buttons.None);e.Process(held,c,new(170,100),200,0,1000,0.05);Assert.Equal(0,e.Process(held,c,null,200,0.1,1100,0.1).RX);Assert.Equal(0,e.Process(held,c,new(170,100),200,0.2,1200,0.3).RX);
    }
}
