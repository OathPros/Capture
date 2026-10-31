using System.Text.Json;
using System.Buffers.Binary;
using System.Text.Json.Serialization;
namespace Capture.Core;
public sealed class Guide
{
    public string SchemaVersion { get; set; } = "1.2.0";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled guide";
    public string Summary { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Step> Steps { get; set; } = [];
    public List<Asset> Assets { get; set; } = [];
    public List<string> PendingAssetDeletes { get; set; } = [];
}
public sealed class Step
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Order { get; set; }
    public string Title { get; set; } = "Select the highlighted area";
    public string Instruction { get; set; } = "";
    public List<string> TitleFields { get; set; } = [];
    public string Notes { get; set; } = "";
    public string? AssetId { get; set; }
    public List<Annotation> Annotations { get; set; } = [];
    public bool NeedsReview { get; set; } = true;
    public CropRegion? Crop { get; set; }
    public List<ImageCut> Cuts { get; set; } = [];
}
public sealed record CropRegion(double X, double Y, double Width, double Height);
public sealed record Annotation(string Id, string Kind, double X, double Y, double Width, double Height)
{
    public string Stroke { get; init; } = "#e31837";
    public string Fill { get; init; } = "transparent";
    public string TextColor { get; init; } = "#202833";
    public double StrokeWidth { get; init; } = 3;
    public double FontSize { get; init; } = 24;
    public bool Shadow { get; init; }
    public string Text { get; init; } = "";
    public bool FlipX { get; init; }
    public bool FlipY { get; init; }
    public int BlurRadius { get; init; } = 12;
}
public sealed record Asset(string Id, string RelativePath, string MimeType, int Width, int Height, string Sha256);
public static class GuideJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string Serialize(Guide guide) { Validate(guide); guide.SchemaVersion = "1.2.0"; return JsonSerializer.Serialize(guide, Options); }
    public static Guide Parse(string json) { var g = JsonSerializer.Deserialize<Guide>(json, Options) ?? throw new InvalidDataException("Empty guide."); Validate(g); return g; }
    public static void Validate(Guide g)
    {
        if (g.SchemaVersion is not ("1.0.0" or "1.1.0" or "1.2.0")) throw new InvalidDataException("Unsupported guide schema. Use a compatible Capture version.");
        if (g.Id == Guid.Empty || g.Title is null || g.Title.Length > 1000 || g.Summary is null || g.Summary.Length > 100000 || g.Steps is null || g.Assets is null || g.Steps.Count > 10000 || g.Assets.Count > 10000 || g.PendingAssetDeletes is null || g.PendingAssetDeletes.Count > 10000) throw new InvalidDataException("Invalid guide.");
        if (g.Steps.Select(s => s.Id).Distinct().Count() != g.Steps.Count || g.Assets.Select(a => a.Id).Distinct().Count() != g.Assets.Count || g.Assets.Select(a => a.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != g.Assets.Count) throw new InvalidDataException("Duplicate IDs.");
        foreach (var a in g.Assets)
        {
            SafePath("/project", a.RelativePath);
            if (!a.RelativePath.StartsWith("assets/", StringComparison.Ordinal) || a.MimeType != "image/png" || a.Width <= 0 || a.Height <= 0 || a.Width > 32768 || a.Height > 32768 || a.Sha256.Length != 64 || !a.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid image asset.");
        }
        foreach(var path in g.PendingAssetDeletes)
        {
            SafePath("/project",path);
            if(!path.StartsWith("assets/",StringComparison.Ordinal) || g.Assets.Any(a=>a.RelativePath.Equals(path,StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Invalid privacy cleanup path.");
        }
        for (int i = 0; i < g.Steps.Count; i++)
        {
            var s = g.Steps[i];
            if (s.Id == Guid.Empty || s.Title is null || s.Instruction is null || s.Notes is null || s.Title.Length > 1000 || s.Instruction.Length > 100000 || s.Notes.Length > 100000 || s.Annotations is null || s.Annotations.Count > 1000 || s.TitleFields is null || s.TitleFields.Count>32 || s.TitleFields.Any(f=>string.IsNullOrWhiteSpace(f)||f.Length>120) || s.Cuts is null || s.Cuts.Count>32) throw new InvalidDataException("Invalid step.");
            if (s.AssetId is not null && !g.Assets.Any(a => a.Id == s.AssetId)) throw new InvalidDataException("Missing image reference.");
            if(s.Crop is not null && !ValidRegion(s.Crop.X,s.Crop.Y,s.Crop.Width,s.Crop.Height)) throw new InvalidDataException("Invalid crop.");
            if(s.Annotations.Select(a=>a.Id).Distinct().Count()!=s.Annotations.Count) throw new InvalidDataException("Duplicate annotation IDs.");
            foreach (var a in s.Annotations)
            {
                if (a.Kind is not ("highlight" or "rectangle" or "ellipse" or "line" or "arrow" or "text" or "callout" or "blur" or "redact") || !ValidRegion(a.X,a.Y,a.Width,a.Height)) throw new InvalidDataException("Invalid annotation.");
                if(string.IsNullOrWhiteSpace(a.Id) || a.Id.Length>100 || !Color(a.Stroke) || !Color(a.TextColor) || (a.Fill!="transparent" && !Color(a.Fill)) || !double.IsFinite(a.StrokeWidth) || a.StrokeWidth<1 || a.StrokeWidth>40 || !double.IsFinite(a.FontSize) || a.FontSize<8 || a.FontSize>256 || a.Text is null || a.Text.Length>4000 || a.BlurRadius<2 || a.BlurRadius>64) throw new InvalidDataException("Invalid annotation properties.");
            }
            foreach(var cut in s.Cuts)
                if(cut.Axis is not ("horizontal" or "vertical") || !double.IsFinite(cut.Start+cut.End) || cut.Start<0 || cut.End>1 || cut.Start>=cut.End)throw new InvalidDataException("Invalid cut strip.");
            if(s.Cuts.Count>0)
            {
                var image=g.Assets.SingleOrDefault(a=>a.Id==s.AssetId)??throw new InvalidDataException("Cut requires a screenshot.");
                var layout=ImageLayout.Create(image.Width,image.Height,s.Crop,s.Cuts);
                if(layout.Width<1||layout.Height<1)throw new InvalidDataException("A cut must leave some of the screenshot.");
            }
            if(StepText.IsDuplicate(s.Title,s.Instruction))
            {if(g.SchemaVersion is "1.0.0" or "1.1.0" && s.TitleFields.Count==0)s.TitleFields=StepText.LegacyFields(s.Title);s.Instruction="";}
            s.Order = i + 1;
        }
    }
    public static bool ValidRegion(double x,double y,double width,double height) => double.IsFinite(x+y+width+height) && x>=0 && y>=0 && width>0 && height>0 && x+width<=1.000001 && y+height<=1.000001;
    private static bool Color(string value) => value is not null && value.Length==7 && value[0]=='#' && value.Skip(1).All(Uri.IsHexDigit);
    public static void EnsurePrivacyApplied(Guide guide)
    {
        if(guide.PendingAssetDeletes.Count>0) throw new InvalidDataException("Finish privacy cleanup before exporting.");
        if(guide.Steps.Any(s=>s.Annotations.Any(a=>a.Kind is "blur" or "redact"))) throw new InvalidDataException("Apply blur/redaction permanently in the screenshot editor before exporting.");
    }
    public static void ValidatePng(ReadOnlySpan<byte> bytes, Asset asset)
    {
        ReadOnlySpan<byte> signature = [137,80,78,71,13,10,26,10];
        if(bytes.Length < 33 || bytes.Length > 64*1024*1024 || !bytes[..8].SequenceEqual(signature) || !bytes.Slice(12,4).SequenceEqual("IHDR"u8)) throw new InvalidDataException("Invalid PNG image.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16,4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20,4));
        if(width != asset.Width || height != asset.Height || (long)width*height > 3840L*2160) throw new InvalidDataException("Image dimensions exceed preview limits or do not match metadata.");
    }
    public static string SafePath(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(p => p is ".." or "." or "") || Path.IsPathRooted(path)) throw new InvalidDataException("Unsafe asset path.");
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(fullRoot, StringComparison.Ordinal)) throw new InvalidDataException("Asset outside project.");
        return full;
    }
}
public static class Coordinates
{
    public static Annotation Marker(double px, double py, double left, double top, double width, double height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var w = Math.Min(32 / width, 1); var h = Math.Min(32 / height, 1);
        return new(Guid.NewGuid().ToString(), "highlight", Math.Clamp((px-left)/width-w/2,0,1-w), Math.Clamp((py-top)/height-h/2,0,1-h), w, h);
    }
}
public enum SessionState { Idle, Recording, Paused, Finalizing }
public sealed class Session
{
    public SessionState State { get; private set; }
    public void Start() { if (State != SessionState.Idle) throw new InvalidOperationException("A session is already active."); State = SessionState.Recording; }
    public void Pause() { if (State != SessionState.Recording) throw new InvalidOperationException(); State = SessionState.Paused; }
    public void Resume() { if (State != SessionState.Paused) throw new InvalidOperationException(); State = SessionState.Recording; }
    public void Finish() { if (State is not (SessionState.Recording or SessionState.Paused)) throw new InvalidOperationException(); State = SessionState.Finalizing; }
    public void Reset() => State = SessionState.Idle;
}
