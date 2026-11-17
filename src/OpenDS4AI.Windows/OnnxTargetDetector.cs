using Microsoft.ML.OnnxRuntime;using Microsoft.ML.OnnxRuntime.Tensors;using OpenDS4AI.Core;using System.Security.Cryptography;
namespace OpenDS4AI.Windows;
public sealed class OnnxTargetDetector:IDisposable
{
    private readonly InferenceSession session;private readonly string inputName;private readonly int inputWidth,inputHeight;
    public string Description{get;}
    public OnnxTargetDetector(string path)
    {
        using var options=new SessionOptions{IntraOpNumThreads=2,InterOpNumThreads=1};
        if(path.EndsWith(".cynomodel",StringComparison.OrdinalIgnoreCase)){byte[] bytes=ModelVault.Load(path);try{session=new InferenceSession(bytes,options);}finally{CryptographicOperations.ZeroMemory(bytes);}}else session=new InferenceSession(path,options);
        try{if(session.InputMetadata.Count!=1)throw new NotSupportedException("Richiesto un input immagine.");var input=session.InputMetadata.First();var shape=input.Value.Dimensions;if(input.Value.ElementType!=typeof(float)||shape.Length!=4||shape[0]!=1||shape[1]!=3||shape[2]<1||shape[3]<1||shape[2]>2048||shape[3]>2048)throw new NotSupportedException("Richiesto input YOLOv8 float32 fisso NCHW.");inputName=input.Key;inputHeight=shape[2];inputWidth=shape[3];Description=$"YOLO CPU {inputWidth}×{inputHeight}";}catch{session.Dispose();throw;}
    }
    public IReadOnlyList<DetectionBox> DetectCandidates(byte[] bgra,int width,int height,float confidence=0.45f)
    {
        if(width<1||height<1||bgra.Length!=checked(width*height*4))throw new ArgumentException("Frame BGRA non valido.");if(!float.IsFinite(confidence)||confidence<0||confidence>1)throw new ArgumentOutOfRangeException(nameof(confidence));
        float scale=Math.Min(inputWidth/(float)width,inputHeight/(float)height);int sw=Math.Clamp((int)MathF.Round(width*scale),1,inputWidth),sh=Math.Clamp((int)MathF.Round(height*scale),1,inputHeight),left=(inputWidth-sw)/2,top=(inputHeight-sh)/2;
        var tensor=new DenseTensor<float>(new[]{1,3,inputHeight,inputWidth});tensor.Buffer.Span.Fill(114f/255);
        for(int y=0;y<sh;y++)for(int x=0;x<sw;x++){int sx=Math.Min(width-1,(int)(x/scale)),sy=Math.Min(height-1,(int)(y/scale)),p=(sy*width+sx)*4;tensor[0,0,y+top,x+left]=bgra[p+2]/255f;tensor[0,1,y+top,x+left]=bgra[p+1]/255f;tensor[0,2,y+top,x+left]=bgra[p]/255f;}
        using var results=session.Run(new[]{NamedOnnxValue.CreateFromTensor(inputName,tensor)});var output=results.First().AsTensor<float>();var d=output.Dimensions;if(d.Length!=3||d[0]!=1||d[1]<5||d[1]>256||d[2]<=d[1])throw new NotSupportedException("Richiesto output YOLOv8 [1,4+C,N].");
        var boxes=new List<DetectionBox>();for(int i=0;i<d[2];i++){float score=output[0,4,i];if(!float.IsFinite(score)||score<confidence)continue;bool person=true;for(int c=5;c<d[1];c++)if(output[0,c,i]>score){person=false;break;}if(!person)continue;float x=(output[0,0,i]-left)/scale,y=(output[0,1,i]-top)/scale,w=output[0,2,i]/scale,h=output[0,3,i]/scale;if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(w)||!float.IsFinite(h)||w<=0||h<=0||x<0||x>=width||y<0||y>=height)continue;boxes.Add(new(x,y,w,h,score));}return boxes;
    }
    public TargetPoint? Detect(byte[] bgra,int width,int height,AssistSettings settings,float confidence=0.45f){var points=DetectCandidates(bgra,width,height,confidence).Select(b=>new TargetPoint(b.X,b.Y)).Where(p=>(p.X-width/2f)*(p.X-width/2f)+(p.Y-height/2f)*(p.Y-height/2f)<=settings.FovRadius*settings.FovRadius).ToArray();return points.Length==0?null:points.MinBy(p=>(p.X-width/2f)*(p.X-width/2f)+(p.Y-height/2f)*(p.Y-height/2f));}
    public void Dispose()=>session.Dispose();
}
