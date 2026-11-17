namespace OpenDS4AI.Core;
public static class VisionGeometry
{
    public static int CaptureSize(int radius,bool wide,int monitorWidth,int monitorHeight)
    {
        if(radius<16||radius>800||monitorWidth<radius*2||monitorHeight<radius*2)throw new ArgumentException("FOV maggiore del monitor selezionato.");
        return Math.Min(radius*(wide?4:2),Math.Min(1600,Math.Min(monitorWidth,monitorHeight)));
    }
    public static TargetPoint ToControlSpace(TargetPoint point,int captureSize,int controlSize)
    {
        if(captureSize<controlSize||controlSize<=0)throw new ArgumentException("Geometria visione non valida.");
        float offset=(captureSize-controlSize)/2f;return new(point.X-offset,point.Y-offset);
    }
}
