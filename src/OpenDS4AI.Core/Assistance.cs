namespace OpenDS4AI.Core;

public sealed record AssistSettings
{
    public bool AimEnabled { get; init; }
    public string MarkerHex { get; init; } = "9800CD";
    public int ColorTolerance { get; init; } = 35;
    public int FovRadius { get; init; } = 150;
    public float Strength { get; init; } = 1;
    public float Smooth { get; init; } = 0.25f;
    public float AimOffsetY { get; init; }
    public bool ActivateOnLeftTrigger { get; init; } = true;
    public bool ActivateOnL1 { get; init; }
    public bool StableAim {get;init;}=true;
    public float AimMaxOutput {get;init;}=0.3f;
    public float AimDeadbandPixels {get;init;}=3;
    public float AimDamping {get;init;}=0.4f;
    public bool InvertAimX {get;init;}
    public bool InvertAimY {get;init;}
    public bool AntiRecoil { get; init; }
    public float RecoilVertical { get; init; } = 0.14f;
    public float RecoilHorizontal { get; init; }
    public bool RapidFire { get; init; }
    public int RapidPeriodMs { get; init; } = 80;
    public bool AutoPing {get;init;}
    public int PingDelayMs {get;init;}=250;
    public int PingCooldownMs {get;init;}=1500;
    public bool SpamSwap {get;init;}
    public int SwapPeriodMs {get;init;}=100;
    public bool CancelSlide {get;init;}
    public int SlideMs {get;init;}=80;
    public bool BunnyHop { get; init; }
    public int BunnyPeriodMs { get; init; } = 160;

    public void Validate()
    {
        var hex = MarkerHex?.TrimStart('#');
        if (hex is null || hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out _))
            throw new ArgumentException("Colore marker richiesto in formato RRGGBB.");
        if(!float.IsFinite(AimMaxOutput)||AimMaxOutput<0.05f||AimMaxOutput>1||!float.IsFinite(AimDeadbandPixels)||AimDeadbandPixels<0||AimDeadbandPixels>20||!float.IsFinite(AimDamping)||AimDamping<0||AimDamping>1)throw new ArgumentException("Parametri controllo stabile non validi.");
        if (ColorTolerance is < 0 or > 120 || FovRadius is < 1 or > 1000 || !float.IsFinite(Strength) || Strength is < 0 or > 5 ||
            !float.IsFinite(Smooth) || Smooth is < 0 or > 0.95f || !float.IsFinite(AimOffsetY) || AimOffsetY is < -0.5f or > 0.5f ||
            !float.IsFinite(RecoilVertical) || RecoilVertical is < -1 or > 1 || !float.IsFinite(RecoilHorizontal) || RecoilHorizontal is < -1 or > 1 ||
            PingDelayMs is < 0 or > 5000 || PingCooldownMs is < 100 or > 10000 || SwapPeriodMs is < 40 or > 2000 || SlideMs is < 20 or > 1000 || RapidPeriodMs is < 10 or > 5000 || BunnyPeriodMs is < 10 or > 5000)
            throw new ArgumentException("Impostazioni assistenza fuori intervallo.");
    }
}

// Coordinates are absolute pixels within the captured crop, not normalized offsets.
public readonly record struct TargetPoint(float X, float Y);

public static class ColorTargetDetector
{
    public static TargetPoint? Detect(byte[] bgra, int width, int height, AssistSettings settings)
    {
        ArgumentNullException.ThrowIfNull(bgra); settings.Validate();
        if (width <= 0 || height <= 0 || (long)width * height * 4 > bgra.Length)
            throw new ArgumentException("Buffer BGRA non valido.");
        uint color = Convert.ToUInt32(settings.MarkerHex.TrimStart('#'), 16);
        int red = (int)((color >> 16) & 255), green = (int)((color >> 8) & 255), blue = (int)(color & 255);
        float cx = width / 2f, cy = height / 2f;
        float bestDistance = settings.FovRadius * (float)settings.FovRadius;
        TargetPoint? best = null;
        // A deterministic nearest matching pixel avoids averaging separate targets together.
        for (int y = Math.Max(0, (int)cy - settings.FovRadius); y < Math.Min(height, (int)cy + settings.FovRadius + 1); y++)
        for (int x = Math.Max(0, (int)cx - settings.FovRadius); x < Math.Min(width, (int)cx + settings.FovRadius + 1); x++)
        {
            int i = (y * width + x) * 4;
            if (Math.Abs(bgra[i] - blue) > settings.ColorTolerance || Math.Abs(bgra[i + 1] - green) > settings.ColorTolerance || Math.Abs(bgra[i + 2] - red) > settings.ColorTolerance) continue;
            float distance = (x - cx) * (x - cx) + (y - cy) * (y - cy);
            if (distance <= bestDistance) { bestDistance = distance; best = new(x, y); }
        }
        return best;
    }
}

public sealed class AssistEngine
{
    private float correctionX,correctionY,lastErrorX,lastErrorY,velocityX,velocityY;
    private double previousTime=double.NaN,lastObservationTime=double.NaN;
    private long lastObservationId=long.MinValue;
    private double fireStarted=double.NaN,jumpStarted=double.NaN,pingStarted=double.NaN,lastPing=double.NegativeInfinity,swapStarted=double.NaN,slideUntil;
    private bool previousSlide;
    public float LastAimX{get;private set;}public float LastAimY{get;private set;}
    public void Reset(){ClearAim();previousTime=fireStarted=jumpStarted=pingStarted=swapStarted=double.NaN;lastPing=double.NegativeInfinity;slideUntil=0;previousSlide=false;}
    private void ClearAim(){correctionX=correctionY=velocityX=velocityY=lastErrorX=lastErrorY=LastAimX=LastAimY=0;lastObservationTime=double.NaN;lastObservationId=long.MinValue;}
    public PadState Process(PadState input,AssistSettings settings,TargetPoint? target,int captureSize,double elapsedSeconds,long observationId=0,double observationAgeSeconds=0,PadState? activationInput=null)
    {
        settings.Validate();if(captureSize<=0||!double.IsFinite(elapsedSeconds)||elapsedSeconds<0||!double.IsFinite(observationAgeSeconds)||observationAgeSeconds<0)throw new ArgumentException("Tempo o dimensione cattura non validi.");
        if(!double.IsNaN(previousTime)&&elapsedSeconds<previousTime)Reset();double delta=double.IsNaN(previousTime)?1.0/120:Math.Min(0.1,elapsedSeconds-previousTime);previousTime=elapsedSeconds;
        var activation=activationInput??input;
        bool active=settings.AimEnabled&&(!settings.ActivateOnLeftTrigger||(settings.ActivateOnL1?activation.Buttons.HasFlag(Buttons.LB):activation.LT>30))&&observationAgeSeconds<=0.25;
        if(active&&target is {} point&&float.IsFinite(point.X)&&float.IsFinite(point.Y))
        {
            float half=captureSize/2f,px=point.X-half,py=-(point.Y-half+settings.AimOffsetY*captureSize);
            if(settings.InvertAimX)px=-px;if(settings.InvertAimY)py=-py;
            float ex=px/half,ey=py/half;
            if(!settings.StableAim)
            {
                float alpha=settings.Smooth==0?1:(float)(1-Math.Pow(settings.Smooth,delta*120));correctionX+=(Math.Clamp(ex*settings.Strength,-1,1)-correctionX)*alpha;correctionY+=(Math.Clamp(ey*settings.Strength,-1,1)-correctionY)*alpha;
            }
            else
            {
                double observationTime=observationId==0?elapsedSeconds:observationId/1000.0;
                bool fresh=observationId==0?double.IsNaN(lastObservationTime)||observationTime>lastObservationTime:observationId!=lastObservationId;
                if(fresh)
                {
                    if(!double.IsNaN(lastObservationTime)){double dt=observationTime-lastObservationTime;if(dt>0.005&&dt<0.5){velocityX=0.6f*velocityX+0.4f*Math.Clamp((ex-lastErrorX)/(float)dt,-8,8);velocityY=0.6f*velocityY+0.4f*Math.Clamp((ey-lastErrorY)/(float)dt,-8,8);}else velocityX=velocityY=0;}
                    lastErrorX=ex;lastErrorY=ey;lastObservationTime=observationTime;lastObservationId=observationId;
                }
                float freshness=(float)Math.Clamp((0.25-observationAgeSeconds)/0.13,0,1),gain=settings.Strength*freshness,kd=settings.AimDamping*0.08f;
                float desiredX=Toward(ex,ex*gain+velocityX*kd,settings.AimMaxOutput),desiredY=Toward(ey,ey*gain+velocityY*kd,settings.AimMaxOutput);
                float norm=MathF.Sqrt(desiredX*desiredX+desiredY*desiredY);if(norm>settings.AimMaxOutput){desiredX*=settings.AimMaxOutput/norm;desiredY*=settings.AimMaxOutput/norm;}
                float alpha=(float)(1-Math.Exp(-delta/(0.012+settings.Smooth*0.08)));
                correctionX+=(desiredX-correctionX)*alpha;correctionY+=(desiredY-correctionY)*alpha;
                // Stop on the centre immediately and never add a correction away from a visible error.
                if(Math.Abs(px)<=settings.AimDeadbandPixels||correctionX*ex<0)correctionX=0;
                if(Math.Abs(py)<=settings.AimDeadbandPixels||correctionY*ey<0)correctionY=0;
                float manual=Math.Clamp((0.95f-MathF.Sqrt(input.RX*input.RX+input.RY*input.RY))/0.45f,0,1);correctionX*=manual;correctionY*=manual;
            }
        }
        else ClearAim();
        var result=input with {RX=Math.Clamp(input.RX+correctionX,-1,1),RY=Math.Clamp(input.RY+correctionY,-1,1)};LastAimX=result.RX-input.RX;LastAimY=result.RY-input.RY;
        bool firing=input.RT>30;if(firing&&settings.AntiRecoil)result=result with {RX=Math.Clamp(result.RX+settings.RecoilHorizontal,-1,1),RY=Math.Clamp(result.RY-settings.RecoilVertical,-1,1)};
        if(!firing||!settings.RapidFire)fireStarted=double.NaN;else{if(double.IsNaN(fireStarted))fireStarted=elapsedSeconds;if(IsReleased(elapsedSeconds-fireStarted,settings.RapidPeriodMs))result=result with {RT=0};}
        bool jumping=(input.Buttons&Buttons.A)!=0;if(!jumping||!settings.BunnyHop)jumpStarted=double.NaN;else{if(double.IsNaN(jumpStarted))jumpStarted=elapsedSeconds;if(IsReleased(elapsedSeconds-jumpStarted,settings.BunnyPeriodMs))result=result with {Buttons=result.Buttons&~Buttons.A};}
        if(settings.AutoPing&&input.LT>30){if(double.IsNaN(pingStarted))pingStarted=elapsedSeconds;if((elapsedSeconds-pingStarted)*1000>=settings.PingDelayMs&&(elapsedSeconds-lastPing)*1000>=settings.PingCooldownMs)lastPing=elapsedSeconds;if(elapsedSeconds-lastPing<0.06)result=result with {Buttons=result.Buttons|Buttons.RS};}else pingStarted=double.NaN;
        bool slide=(input.Buttons&Buttons.B)!=0;if(settings.SpamSwap&&slide){if(double.IsNaN(swapStarted))swapStarted=elapsedSeconds;if(!IsReleased(elapsedSeconds-swapStarted,settings.SwapPeriodMs))result=result with {Buttons=result.Buttons|Buttons.Y};}else swapStarted=double.NaN;
        if(settings.CancelSlide&&slide&&!previousSlide)slideUntil=elapsedSeconds+settings.SlideMs/1000.0;if(settings.CancelSlide&&elapsedSeconds<slideUntil)result=result with {Buttons=result.Buttons|Buttons.LS};previousSlide=slide;
        return result;
    }
    private static float Toward(float error,float value,float maximum)=>error>0?Math.Clamp(value,0,maximum):error<0?Math.Clamp(value,-maximum,0):0;
    private static bool IsReleased(double seconds,int periodMs)=>seconds*1000%periodMs>=periodMs/2.0;
}
