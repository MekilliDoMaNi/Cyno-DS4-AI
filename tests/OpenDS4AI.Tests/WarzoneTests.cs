using OpenDS4AI.Core;using Xunit;
namespace OpenDS4AI.Tests;
public class WarzoneTests
{
    [Fact]public void FiltersHudAndLowConfidence(){var e=new WarzoneEngine();var target=e.Select(new[]{new DetectionBox(150,2,20,30,0.99f),new DetectionBox(150,150,20,60,0.2f),new DetectionBox(170,160,20,60,0.9f)},300,150,0,new(Anchor:0.5f,Smoothing:0));Assert.Equal(new TargetPoint(170,160),target);}
    [Fact]public void TargetLossResetsImmediately(){var e=new WarzoneEngine();Assert.NotNull(e.Select(new[]{new DetectionBox(150,150,30,70,0.9f)},300,150,0,new()));Assert.Null(e.Select(Array.Empty<DetectionBox>(),300,150,0.05,new()));Assert.Equal(new TargetPoint(100,150),e.Select(new[]{new DetectionBox(100,150,30,70,0.9f)},300,150,0.1,new(Anchor:0.5f,Smoothing:0)));}
    [Fact]public void BodyAnchorUsesBoxHeight(){var e=new WarzoneEngine();Assert.Equal(new TargetPoint(150,130),e.Select(new[]{new DetectionBox(150,150,30,80,0.9f)},300,150,0,new(Anchor:0.25f,Smoothing:0)));}
    [Fact]public void SmallCentralityChangeDoesNotSwitchTrack(){var e=new WarzoneEngine();var o=new WarzoneOptions(Anchor:0.5f,Smoothing:0);e.Select(new[]{new DetectionBox(140,150,15,50,0.9f)},300,150,0,o);Assert.Equal(new TargetPoint(140,150),e.Select(new[]{new DetectionBox(140,150,15,50,0.9f),new DetectionBox(159,150,15,50,0.95f)},300,150,0.05,o));}
    [Fact]public void AnnotationsNormalizeReverseDragAndClip(){var b=TrainingBox.FromPixels(120,100,-20,20,100,100);Assert.Equal(new TrainingBox(0.5f,0.6f,1,0.8f),b);Assert.StartsWith("0 0.500000 0.600000",b.ToYolo());}
    [Fact]public void RejectsInvalidBoxesAndOptions(){Assert.Throws<ArgumentException>(()=>TrainingBox.FromPixels(1,1,1,1,100,100));Assert.Throws<ArgumentException>(()=>new WarzoneOptions(Confidence:float.NaN).Validate());}
}
