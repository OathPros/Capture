using System.IO.Compression;
using System.Security.Cryptography;
using Capture.Core;
using Xunit;
namespace Capture.Tests;
public sealed class GuideTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"capture-tests-"+Guid.NewGuid());
    [Fact] public void SchemaRoundtripPreservesIdsAndNormalizesOrder()
    {
        var guide = new Guide { Steps = [new() { Order=8,Instruction="Synthetic instruction" }] };
        var parsed = GuideJson.Parse(GuideJson.Serialize(guide));
        Assert.Equal(guide.Id,parsed.Id); Assert.Equal(guide.Steps[0].Id,parsed.Steps[0].Id); Assert.Equal(1,parsed.Steps[0].Order);
    }
    [Fact] public void FutureSchemaIsRejected() => Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(new Guide { SchemaVersion="2.0.0" }));
    [Theory] [InlineData("../secret")] [InlineData("/etc/passwd")] [InlineData("C:/secret")] [InlineData("assets/../../secret")] [InlineData("assets\\secret")] [InlineData("assets//a")]
    public void UnsafePathsAreRejected(string path) => Assert.Throws<InvalidDataException>(()=>GuideJson.SafePath(root,path));
    [Fact] public void MarkerMapsNegativeMonitorOrigin()
    {
        var a = Coordinates.Marker(-960,540,-1920,0,1920,1080);
        Assert.Equal(.5,a.X+a.Width/2,6); Assert.Equal(.5,a.Y+a.Height/2,6);
    }
    [Fact] public void MarkerClampsEdges()
    {
        var a=Coordinates.Marker(-1920,0,-1920,0,1920,1080); Assert.Equal(0,a.X); Assert.Equal(0,a.Y);
    }
    [Fact] public void SessionRejectsDoubleStartAndAllowsPauseFinish()
    {
        var s=new Session();s.Start();Assert.Throws<InvalidOperationException>(s.Start);s.Pause();s.Resume();s.Pause();s.Finish();Assert.Equal(SessionState.Finalizing,s.State);s.Reset();s.Start();
    }
    [Fact] public async Task SaveReplaceAndReopen()
    {
        var store=new ProjectStore(root);var g=new Guide();await store.SaveAsync(g);g.Title="Updated";await store.SaveAsync(g);
        Assert.Equal("Updated",(await store.LoadAsync(g.Id)).Title);Assert.Single(await store.ListAsync());Assert.False(File.Exists(Path.Combine(store.DirectoryFor(g.Id),"guide.json.tmp")));
    }
    [Fact] public async Task PortableRoundtripVerifiesAssets()
    {
        var store=new ProjectStore(root);var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aXioAAAAASUVORK5CYII=");
        var g=new Guide { Assets=[new("image","assets/image.png","image/png",1,1,Convert.ToHexString(SHA256.HashData(bytes)))],Steps=[new(){AssetId="image"}] };
        var path=GuideJson.SafePath(store.DirectoryFor(g.Id),"assets/image.png");Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path,bytes);await store.SaveAsync(g);
        var bundle=Path.Combine(root,"roundtrip.capture");await store.ExportPortableAsync(g,bundle);var imported=await store.ImportPortableAsync(bundle);
        Assert.NotEqual(g.Id,imported.Id);Assert.Equal(g.Steps[0].Id,imported.Steps[0].Id);Assert.Equal(bytes,await File.ReadAllBytesAsync(GuideJson.SafePath(store.DirectoryFor(imported.Id),"assets/image.png")));
    }
    [Fact] public async Task ZipTraversalIsRejectedBeforeWriting()
    {
        Directory.CreateDirectory(root);var file=Path.Combine(root,"attack.capture");using(var zip=ZipFile.Open(file,ZipArchiveMode.Create)) { using var writer=new StreamWriter(zip.CreateEntry("../escape").Open());writer.Write("bad"); }
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ProjectStore(root).ImportPortableAsync(file));
    }
    [Fact] public void InvalidAnnotationsAndMissingAssetsAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(new Guide { Steps=[new(){AssetId="missing"}] }));
        Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(new Guide { Steps=[new(){Annotations=[new("a","highlight",.9,.9,.5,.5)]}] }));
    }
    [Fact] public void MarkdownEscapesUserContent()
    {
        var text=ProjectStore.Markdown(new Guide { Title="<script>",Steps=[new(){Instruction="[run](javascript:alert(1))"}] });
        Assert.Contains("\\<script\\>",text);Assert.Contains("\\[run\\]",text);
    }
    [Fact] public async Task TamperedImageIsRejectedAndImportRolledBack()
    {
        Directory.CreateDirectory(root);var source=Path.Combine(root,"tampered.capture");
        var g=new Guide { Assets=[new("image","assets/image.png","image/png",1,1,new string('0',64))] };
        using(var zip=ZipFile.Open(source,ZipArchiveMode.Create))
        {
            using(var writer=new StreamWriter(zip.CreateEntry("guide.json").Open())) writer.Write(GuideJson.Serialize(g));
            using(var writer=new StreamWriter(zip.CreateEntry("assets/image.png").Open())) writer.Write("not an image");
        }
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ProjectStore(root).ImportPortableAsync(source));
        Assert.Empty(Directory.EnumerateDirectories(root));
    }
    [Fact] public async Task PortableExportOmitsAssetsFromDeletedSteps()
    {
        var store=new ProjectStore(root);var g=new Guide { Assets=[new("unused","assets/unused.png","image/png",1,1,new string('0',64))] };
        await store.SaveAsync(g);var file=Path.Combine(root,"clean.capture");await store.ExportPortableAsync(g,file);
        using var zip=ZipFile.OpenRead(file);Assert.Single(zip.Entries);
        using var reader=new StreamReader(zip.GetEntry("guide.json")!.Open());var exported=GuideJson.Parse(await reader.ReadToEndAsync());Assert.Empty(exported.Assets);Assert.Single(g.Assets);
    }
    [Fact] public void PngHeaderDimensionsMustMatchMetadata()
    {
        var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aXioAAAAASUVORK5CYII=");
        Assert.Throws<InvalidDataException>(()=>GuideJson.ValidatePng(bytes,new("x","assets/x.png","image/png",2,2,new string('0',64))));
    }
    public void Dispose() { if(Directory.Exists(root)) Directory.Delete(root,true); }
}
