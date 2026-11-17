namespace OpenDS4AI.Core;
public readonly record struct DetectionBox(float X,float Y,float Width,float Height,float Confidence)
{
    public TargetPoint Anchor(float fraction)=>new(X,Y+Height*(fraction-0.5f));
}
public sealed record WarzoneOptions(float Confidence=0.45f,float Anchor=0.35f,float Smoothing=0.3f,float MaxJumpFraction=0.35f,bool KeepTarget=false,bool RequireVisibleTop=false)
{
    public void Validate(){if(!float.IsFinite(Confidence)||Confidence<0.05f||Confidence>0.99f||!float.IsFinite(Anchor)||Anchor<0||Anchor>1||!float.IsFinite(Smoothing)||Smoothing<0||Smoothing>0.95f||!float.IsFinite(MaxJumpFraction)||MaxJumpFraction<=0||MaxJumpFraction>1)throw new ArgumentException("Parametri motore Warzone non validi.");}
}
/// <summary>Independent candidate filtering and temporal association. No game-specific trained weights are implied.</summary>
public sealed class WarzoneEngine
{
    private TargetPoint? previous,challenger;private DetectionBox? trackedBox;private int challengerFrames;private double stamp=double.NaN;
    public void Reset(){previous=challenger=null;trackedBox=null;challengerFrames=0;stamp=double.NaN;}
    public TargetPoint? Select(IReadOnlyList<DetectionBox> candidates,int size,int radius,double seconds,WarzoneOptions options)
    {
        options.Validate();if(size<=0||radius<=0||!double.IsFinite(seconds)||seconds<0)throw new ArgumentException("Frame o tempo non valido.");
        if(!double.IsNaN(stamp)&&(seconds<stamp||seconds-stamp>0.25))Reset();
        float center=size/2f;
        var valid=candidates.Where(b=>float.IsFinite(b.X)&&float.IsFinite(b.Y)&&float.IsFinite(b.Width)&&float.IsFinite(b.Height)&&float.IsFinite(b.Confidence)&&b.Confidence>=(previous.HasValue?options.Confidence*0.65f:options.Confidence)&&b.Height>=8&&b.Width>=3&&b.Width/b.Height>=0.12f&&b.Width/b.Height<=1.3f&&b.Y>=size*0.04f&&b.Y<=size*0.94f&&b.X>=0&&b.X<size&&b.Width<=size*1.5f&&b.Height<=size*1.8f).OrderByDescending(b=>b.Confidence).ToList();
        var kept=new List<DetectionBox>();foreach(var box in valid)if(!kept.Any(k=>Overlap(k,box)>0.5f))kept.Add(box);
        kept=kept.Where(b=>!options.RequireVisibleTop||b.Y-b.Height/2>=2).ToList();
        if(options.KeepTarget)
        {
            var eligible=kept.Where(b=>{var p=b.Anchor(options.Anchor);return p.Y>=0&&p.Y<size&&Distance(p,new(center,center))<=radius*radius;}).ToArray();
            if(trackedBox is {} old)
            {
                var matching=eligible.Where(b=>b.Height/old.Height is >=0.5f and <=2f && b.Width/old.Width is >=0.4f and <=2.5f &&
                    (Overlap(old,b)>0.1f||Distance(new(old.X,old.Y),new(b.X,b.Y))<=MathF.Pow(Math.Max(12,Math.Min(old.Height,b.Height)*0.65f),2))).ToArray();
                if(matching.Length==0){Reset();return null;}
                var chosenBox=matching.MinBy(b=>Distance(new(old.X,old.Y),new(b.X,b.Y))/Math.Max(1,old.Height*old.Height)+0.5f*(1-Overlap(old,b)));
                var point=chosenBox.Anchor(options.Anchor);float dt=(float)Math.Clamp(seconds-stamp,0,0.25);float alpha=options.Smoothing==0?1:1-MathF.Pow(options.Smoothing,dt*60);
                var filtered=previous is {} last?new TargetPoint(last.X+(point.X-last.X)*alpha,last.Y+(point.Y-last.Y)*alpha):point;
                // Keep the filtered point inside the currently observed target box.
                filtered=new(Math.Clamp(filtered.X,chosenBox.X-chosenBox.Width/2,chosenBox.X+chosenBox.Width/2),Math.Clamp(filtered.Y,chosenBox.Y-chosenBox.Height/2,chosenBox.Y+chosenBox.Height/2));
                trackedBox=chosenBox;previous=filtered;stamp=seconds;return filtered;
            }
            var acquire=eligible.Where(b=>b.Confidence>=options.Confidence).ToArray();if(acquire.Length==0){Reset();return null;}
            var first=acquire.MinBy(b=>Distance(b.Anchor(options.Anchor),new(center,center)));trackedBox=first;previous=first.Anchor(options.Anchor);stamp=seconds;return previous;
        }
        var points=kept.Select(b=>b.Anchor(options.Anchor)).Where(p=>p.Y>=0&&p.Y<size&&Distance(p,new(center,center))<=radius*radius).ToArray();
        if(points.Length==0){Reset();return null;}
        if(previous is null){var acquire=kept.Where(b=>b.Confidence>=options.Confidence).Select(b=>b.Anchor(options.Anchor)).Where(p=>p.Y>=0&&p.Y<size&&Distance(p,new(center,center))<=radius*radius).ToArray();if(acquire.Length==0){Reset();return null;}points=acquire;}
        var nearest=points.MinBy(p=>Distance(p,new(center,center)));
        var chosen=nearest;
        if(previous is {} prior)
        {
            var associated=points.MinBy(p=>Distance(p,prior));float maxJump=size*options.MaxJumpFraction;
            if(Distance(associated,prior)<=maxJump*maxJump){bool switchCandidate=nearest!=associated&&Distance(nearest,new(center,center))<Distance(associated,new(center,center))*0.6f;if(switchCandidate){if(challenger is {} c&&Distance(c,nearest)<size*size*0.01f)challengerFrames++;else{challenger=nearest;challengerFrames=1;}chosen=challengerFrames>=3?nearest:associated;if(challengerFrames>=3){previous=null;challenger=null;challengerFrames=0;}}else{chosen=associated;challenger=null;challengerFrames=0;}}
            float dt=(float)Math.Clamp(seconds-stamp,0,0.25);float alpha=options.Smoothing==0?1:1-MathF.Pow(options.Smoothing,dt*60);
            chosen=new(prior.X+(chosen.X-prior.X)*alpha,prior.Y+(chosen.Y-prior.Y)*alpha);
        }
        previous=chosen;stamp=seconds;return chosen;
    }
    private static float Distance(TargetPoint a,TargetPoint b)=>(a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y);
    private static float Overlap(DetectionBox a,DetectionBox b){float w=Math.Max(0,Math.Min(a.X+a.Width/2,b.X+b.Width/2)-Math.Max(a.X-a.Width/2,b.X-b.Width/2));float h=Math.Max(0,Math.Min(a.Y+a.Height/2,b.Y+b.Height/2)-Math.Max(a.Y-a.Height/2,b.Y-b.Height/2));float intersection=w*h;return intersection/Math.Max(1,a.Width*a.Height+b.Width*b.Height-intersection);}
}
public readonly record struct TrainingBox(float CenterX,float CenterY,float Width,float Height)
{
    public static TrainingBox FromPixels(float x1,float y1,float x2,float y2,int width,int height)
    {
        if(width<=0||height<=0||!float.IsFinite(x1)||!float.IsFinite(x2)||!float.IsFinite(y1)||!float.IsFinite(y2))throw new ArgumentException("Annotazione non valida.");
        float left=Math.Clamp(Math.Min(x1,x2),0,width),right=Math.Clamp(Math.Max(x1,x2),0,width),top=Math.Clamp(Math.Min(y1,y2),0,height),bottom=Math.Clamp(Math.Max(y1,y2),0,height);
        if(right-left<2||bottom-top<2)throw new ArgumentException("Rettangolo troppo piccolo.");return new((left+right)/2/width,(top+bottom)/2/height,(right-left)/width,(bottom-top)/height);
    }
    public string ToYolo()=>FormattableString.Invariant($"0 {CenterX:F6} {CenterY:F6} {Width:F6} {Height:F6}");
}
