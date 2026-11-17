using System.Text.Json;
namespace OpenDS4AI.Core;
[Flags] public enum Buttons { None=0, A=1, B=2, X=4, Y=8, Up=16, Right=32, Down=64, Left=128, LB=256, RB=512, Back=1024, Start=2048, LS=4096, RS=8192, Guide=16384 }
public readonly record struct PadState(float LX, float LY, float RX, float RY, byte LT, byte RT, Buttons Buttons);
public sealed record Profile(string Name = "Default", float DeadZone = 0.08f, bool InvertY = false)
{
    // 0: original; 1: L1 aim / R1 fire; 2: R1 aim / L1 fire.
    public int ShoulderLayout { get; init; }
    public PadState ApplyOutputLayout(PadState s)
    {
        if(ShoulderLayout==0)return s;
        if(ShoulderLayout is <1 or >2)throw new ArgumentException("Schema dorsali non valido.");
        bool crossed=ShoulderLayout==2;
        byte lt=(s.Buttons.HasFlag(crossed?Buttons.RB:Buttons.LB))?(byte)255:(byte)0;
        byte rt=(s.Buttons.HasFlag(crossed?Buttons.LB:Buttons.RB))?(byte)255:(byte)0;
        var buttons=s.Buttons&~(Buttons.LB|Buttons.RB);
        if(s.LT>30)buttons|=crossed?Buttons.RB:Buttons.LB;
        if(s.RT>30)buttons|=crossed?Buttons.LB:Buttons.RB;
        return s with {LT=lt,RT=rt,Buttons=buttons};
    }
    public Dictionary<Buttons, Buttons> Remap { get; init; } = new();
    public void Validate() { if(ShoulderLayout is <0 or >2)throw new ArgumentException("Schema dorsali non valido."); if(Remap==null)throw new ArgumentException("Rimappatura mancante.");foreach(var pair in Remap) { int key=(int)pair.Key; if(key<=0 || (key&(key-1))!=0 || !Enum.IsDefined(pair.Key) || !Enum.IsDefined(pair.Value))throw new ArgumentException("Rimappatura non valida."); } if (string.IsNullOrWhiteSpace(Name) || Name.Length>80 || !float.IsFinite(DeadZone) || DeadZone < 0 || DeadZone >= 1) throw new ArgumentException("Nome o dead zone non validi."); }
    public PadState Apply(PadState s) { Validate(); var mapped=s.Buttons; foreach(var pair in Remap) { if((s.Buttons & pair.Key)!=0)mapped &= ~pair.Key; } foreach(var pair in Remap) { if((s.Buttons & pair.Key)!=0)mapped |= pair.Value; } float Axis(float v) => MathF.Abs(v) <= DeadZone ? 0 : MathF.CopySign((MathF.Abs(v)-DeadZone)/(1-DeadZone), v); return s with { Buttons=mapped, LX=Axis(s.LX), LY=Axis(s.LY)*(InvertY?-1:1), RX=Axis(s.RX), RY=Axis(s.RY)*(InvertY?-1:1) }; }
}
public static class DualSenseParser
{
    public static uint BluetoothCrc(ReadOnlySpan<byte> data) { uint crc=0xffffffff; void Add(byte b) { crc ^= b; for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0); } Add(0xa1);foreach(byte b in data)Add(b);return ~crc; }
    public static PadState Parse(ReadOnlySpan<byte> r)
    {
        // Full Bluetooth input report: report ID and sequence precede the common payload.
        if(r.Length==78 && r[0]==0x31) { uint expected=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(r[74..]); if(BluetoothCrc(r[..74])!=expected)throw new ArgumentException("CRC Bluetooth non valido."); Span<byte> usb=stackalloc byte[64];usb[0]=1;r.Slice(2,63).CopyTo(usb[1..]);return Parse(usb); }
        if (r.Length < 64 || r[0] != 1) throw new ArgumentException("Richiesto report DualSense USB 0x01 da almeno 64 byte.");
        float Axis(byte v) => v >=128 ? (v-128)/127f : (v-128)/128f;
        var b = Buttons.None; int hat = r[8]&15;
        if (hat is 0 or 1 or 7) b|=Buttons.Up; if(hat is 1 or 2 or 3)b|=Buttons.Right;
        if(hat is 3 or 4 or 5)b|=Buttons.Down; if(hat is 5 or 6 or 7)b|=Buttons.Left;
        if((r[8]&16)!=0)b|=Buttons.X; if((r[8]&32)!=0)b|=Buttons.A; if((r[8]&64)!=0)b|=Buttons.B; if((r[8]&128)!=0)b|=Buttons.Y;
        if((r[9]&1)!=0)b|=Buttons.LB; if((r[9]&2)!=0)b|=Buttons.RB; if((r[9]&16)!=0)b|=Buttons.Back; if((r[9]&32)!=0)b|=Buttons.Start;
        if((r[9]&64)!=0)b|=Buttons.LS; if((r[9]&128)!=0)b|=Buttons.RS; if((r[10]&1)!=0)b|=Buttons.Guide;
        return new(Axis(r[1]),-Axis(r[2]),Axis(r[3]),-Axis(r[4]),r[5],r[6],b);
    }
}
public static class ProfileStore
{
    public static Profile Load(string path) { if(!File.Exists(path)) return new(); var p=JsonSerializer.Deserialize<Profile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Profilo vuoto."); p.Validate(); return p; }
    public static void Save(string path, Profile p) { p.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); string temp=path+".tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(p,new JsonSerializerOptions{WriteIndented=true})); File.Move(temp,path,true); }
}
public interface IVirtualController : IDisposable { string Name { get; } void Submit(PadState state); }
public sealed class PreviewController : IVirtualController { public string Name => "Anteprima (nessun dispositivo virtuale)"; public PadState Last {get; private set;} public void Submit(PadState state)=>Last=state; public void Dispose(){} }
