using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
namespace Capture.Core;
public sealed class ProjectStore(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public string DirectoryFor(Guid id) => Path.Combine(Root, id.ToString());
    public async Task SaveAsync(Guide g)
    {
        var json = GuideJson.Serialize(g);
        var dir = DirectoryFor(g.Id); Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "guide.json");
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, json);
        File.Move(temp, path, true);
    }
    public async Task<Guide> LoadAsync(Guid id)
    {
        var path = Path.Combine(DirectoryFor(id), "guide.json");
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Guide is too large.");
        var g = GuideJson.Parse(await File.ReadAllTextAsync(path));
        if (g.Id != id) throw new InvalidDataException("Project ID mismatch.");
        return g;
    }
    public async Task<List<Guide>> ListAsync()
    {
        Directory.CreateDirectory(Root); var result = new List<Guide>();
        foreach (var dir in Directory.EnumerateDirectories(Root))
        {
            if (!Guid.TryParse(Path.GetFileName(dir), out var id)) continue;
            try { result.Add(await LoadAsync(id)); } catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or ArgumentException) { /* A corrupt project must not hide other drafts. */ }
        }
        return result.OrderByDescending(g => g.UpdatedAt).ToList();
    }
    public async Task ExportPortableAsync(Guide g, string target)
    {
        g = GuideJson.Parse(GuideJson.Serialize(g));
        var referenced = g.Steps.Where(s=>s.AssetId is not null).Select(s=>s.AssetId).ToHashSet();
        g.Assets = g.Assets.Where(a=>referenced.Contains(a.Id)).ToList();
        using var file = new FileStream(target, FileMode.CreateNew);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("guide.json");
        using (var writer = new StreamWriter(entry.Open())) await writer.WriteAsync(GuideJson.Serialize(g));
        foreach (var a in g.Assets)
        {
            var path = GuideJson.SafePath(DirectoryFor(g.Id), a.RelativePath);
            var bytes = await File.ReadAllBytesAsync(path);
            GuideJson.ValidatePng(bytes,a);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!hash.Equals(a.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Image checksum mismatch.");
            zip.CreateEntryFromFile(path, a.RelativePath);
        }
    }
    public async Task<Guide> ImportPortableAsync(string source)
    {
        using var zip = ZipFile.OpenRead(source);
        if (zip.Entries.Count > 10001 || zip.Entries.Sum(e => e.Length) > 512L * 1024 * 1024 || zip.Entries.Any(e => e.Length > 64L * 1024 * 1024)) throw new InvalidDataException("Package exceeds size limits.");
        foreach (var entry in zip.Entries) GuideJson.SafePath(Root, entry.FullName);
        if (zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count) throw new InvalidDataException("Duplicate package paths.");
        var metadata = zip.GetEntry("guide.json") ?? throw new InvalidDataException("Missing guide.json.");
        if (metadata.Length > 16 * 1024 * 1024) throw new InvalidDataException("Guide is too large.");
        Guide g; using (var reader = new StreamReader(metadata.Open())) g = GuideJson.Parse(await reader.ReadToEndAsync());
        g.Id = Guid.NewGuid();
        var dir = DirectoryFor(g.Id); Directory.CreateDirectory(dir);
        try
        {
            foreach (var a in g.Assets)
            {
                var entry = zip.GetEntry(a.RelativePath) ?? throw new InvalidDataException("Missing packaged image.");
                var path = GuideJson.SafePath(dir, a.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using (var output = File.Create(path)) { await using var input = entry.Open(); await input.CopyToAsync(output); }
                var bytes = await File.ReadAllBytesAsync(path); GuideJson.ValidatePng(bytes,a);
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(a.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Image checksum mismatch.");
            }
            await SaveAsync(g); return g;
        }
        catch { Directory.Delete(dir, true); throw; }
    }
    public static string Markdown(Guide g)
    {
        static string Escape(string s) => string.Concat(s.Select(c => "\\`*_{}[]<>()#+-.!|".Contains(c) ? "\\" + c : c.ToString()));
        var b = new StringBuilder().AppendLine("# " + Escape(g.Title)).AppendLine().AppendLine(Escape(g.Summary));
        foreach (var (s, i) in g.Steps.Select((s,i)=>(s,i)))
        {
            b.AppendLine().AppendLine($"## {i+1}. {Escape(s.Title)}").AppendLine().AppendLine(Escape(s.Instruction));
            if (!string.IsNullOrWhiteSpace(s.Notes)) b.AppendLine().AppendLine(Escape(s.Notes));
            if (s.AssetId is not null) b.AppendLine().AppendLine($"![Step {i+1} screenshot](assets/step-{i+1:D3}.png)");
        }
        return b.ToString();
    }
}
