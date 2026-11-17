using System.IO;using System.Text.Json;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Shapes;using OpenDS4AI.Core;
namespace OpenDS4AI.App;
public partial class MainWindow
{
    private byte[]? trainingPixels;private int trainingSize;private Point? annotationStart;private Rectangle? draftBox;private readonly List<TrainingBox> trainingBoxes=new();
    private string DatasetRoot=>System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cyno DS4.AI","WarzoneDataset");
    private void CaptureTraining_Click(object sender,RoutedEventArgs e)
    {
        lock(gate){trainingPixels=frame?.ToArray();trainingSize=frameSize;}if(trainingPixels==null){DatasetStatus.Text="Abilita la visione per acquisire un frame.";return;}
        var bitmap=BitmapSource.Create(trainingSize,trainingSize,96,96,PixelFormats.Bgr32,null,trainingPixels,trainingSize*4);bitmap.Freeze();TrainingPreview.Source=bitmap;ClearTraining_Click(sender,e);DatasetStatus.Text="Frame congelato. Annota ogni persona prima di salvare.";
    }
    private void ClearTraining_Click(object sender,RoutedEventArgs e){trainingBoxes.Clear();TrainingCanvas.Children.Clear();annotationStart=null;draftBox=null;TrainingCanvas.ReleaseMouseCapture();DatasetStatus.Text="Annotazioni azzerate.";}
    private void TrainingDown(object sender,MouseButtonEventArgs e){if(trainingPixels==null)return;annotationStart=e.GetPosition(TrainingCanvas);draftBox=new Rectangle{Stroke=Brushes.DeepPink,StrokeThickness=2};TrainingCanvas.Children.Add(draftBox);TrainingCanvas.CaptureMouse();e.Handled=true;}
    private void TrainingMove(object sender,MouseEventArgs e){if(annotationStart is not {} start||draftBox==null)return;var p=e.GetPosition(TrainingCanvas);double left=Math.Clamp(Math.Min(start.X,p.X),0,400),top=Math.Clamp(Math.Min(start.Y,p.Y),0,400),right=Math.Clamp(Math.Max(start.X,p.X),0,400),bottom=Math.Clamp(Math.Max(start.Y,p.Y),0,400);Canvas.SetLeft(draftBox,left);Canvas.SetTop(draftBox,top);draftBox.Width=right-left;draftBox.Height=bottom-top;}
    private void TrainingUp(object sender,MouseButtonEventArgs e)
    {
        if(annotationStart is not {} start)return;var p=e.GetPosition(TrainingCanvas);try{trainingBoxes.Add(TrainingBox.FromPixels((float)start.X,(float)start.Y,(float)p.X,(float)p.Y,400,400));DatasetStatus.Text=$"{trainingBoxes.Count} persone annotate.";}catch(ArgumentException){if(draftBox!=null)TrainingCanvas.Children.Remove(draftBox);}finally{annotationStart=null;draftBox=null;TrainingCanvas.ReleaseMouseCapture();}e.Handled=true;
    }
    private void SaveTraining_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            if(trainingPixels==null)throw new InvalidOperationException("Congela prima un frame.");
            bool negative=NegativeSample.IsChecked==true;if(trainingBoxes.Count==0&&!negative)throw new InvalidOperationException("Annota le persone o indica un frame negativo.");if(negative&&trainingBoxes.Count>0)throw new InvalidOperationException("Un frame negativo non deve avere rettangoli.");
            string split=ValidationSample.IsChecked==true?"val":"train",name=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")[..6];
            string imageDir=System.IO.Path.Combine(DatasetRoot,"images",split),labelDir=System.IO.Path.Combine(DatasetRoot,"labels",split);Directory.CreateDirectory(imageDir);Directory.CreateDirectory(labelDir);
            string imagePath=System.IO.Path.Combine(imageDir,name+".png"),labelPath=System.IO.Path.Combine(labelDir,name+".txt");
            var bitmap=BitmapSource.Create(trainingSize,trainingSize,96,96,PixelFormats.Bgr32,null,trainingPixels,trainingSize*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(imagePath+".tmp"))encoder.Save(stream);
            File.WriteAllLines(labelPath,trainingBoxes.Select(b=>b.ToYolo()));File.Move(imagePath+".tmp",imagePath);
            string yaml="path: "+JsonSerializer.Serialize(DatasetRoot.Replace('\\','/'))+"\ntrain: images/train\nval: images/val\nnames:\n  0: person\n";File.WriteAllText(System.IO.Path.Combine(DatasetRoot,"dataset.yaml"),yaml);
            File.WriteAllText(System.IO.Path.Combine(labelDir,name+".metadata.json"),JsonSerializer.Serialize(new{Game="Warzone",Source="manual local annotation",Split=split,Negative=negative,Width=trainingSize,Height=trainingSize,TimestampUtc=DateTime.UtcNow,Boxes=trainingBoxes}));
            DatasetStatus.Text=$"Campione {split} salvato con {trainingBoxes.Count} annotazioni.\n{DatasetRoot}";
        }
        catch(Exception ex){DatasetStatus.Text="Campione non salvato: "+ex.Message;}
    }
}
