using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Security.Cryptography;
using Capture.App;
using Capture.Core;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Drawing;
using Xunit;
namespace Capture.Windows.Tests;
public sealed class ImageMarkupTests : IDisposable
{
    private readonly string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"capture-markup-"+Guid.NewGuid());
    private readonly ProjectStore store;
    public ImageMarkupTests()=>store=new(root);
    private async Task<Guide> Fixture(bool checker=false)
    {
        var guide=new Guide { Steps=[new(){AssetId="image"}] };var dir=System.IO.Path.Combine(store.DirectoryFor(guide.Id),"assets");Directory.CreateDirectory(dir);
        using var image=new Bitmap(640,360);using(var g=Graphics.FromImage(image)){g.Clear(Color.White);if(checker)for(int y=0;y<360;y+=4)for(int x=0;x<640;x+=4)if((x/4+y/4)%2==0)g.FillRectangle(Brushes.Black,x,y,4,4);}
        var file=System.IO.Path.Combine(dir,"image.png");image.Save(file,ImageFormat.Png);var bytes=await File.ReadAllBytesAsync(file);
        guide.Assets=[new("image","assets/image.png","image/png",640,360,Convert.ToHexString(SHA256.HashData(bytes)))];await store.SaveAsync(guide);return guide;
    }
    private static Bitmap Decode(ExportImage image)=>new(new MemoryStream(image.Png));
    private async Task<byte[]> Export(string format,Guide guide)
    {
        var file=System.IO.Path.Combine(root,"result."+format);Task<ExportImage> Render(Step s)=>ExportImageRenderer.RenderAsync(guide,s,store.DirectoryFor(guide.Id));
        if(format=="docx") {await DocumentExports.DocxAsync(guide,file,Render);using var doc=WordprocessingDocument.Open(file,false);var main=doc.MainDocumentPart!;using var input=main.GetPartById(main.Document!.Descendants<Blip>().First().Embed!).GetStream();using var output=new MemoryStream();input.CopyTo(output);return output.ToArray();}
        if(format=="markdown") {await DocumentExports.MarkdownAsync(guide,file,Render);using var zip=ZipFile.OpenRead(file);using var input=zip.GetEntry("assets/step-001.png")!.Open();using var output=new MemoryStream();input.CopyTo(output);return output.ToArray();}
        await DocumentExports.HtmlAsync(guide,file,Render);var html=await File.ReadAllTextAsync(file);const string prefix="data:image/png;base64,";var start=html.IndexOf(prefix,StringComparison.Ordinal)+prefix.Length;return Convert.FromBase64String(html[start..html.IndexOf('"',start)]);
    }
    [Theory][InlineData("docx")][InlineData("markdown")][InlineData("html")]
    public async Task EveryFormatIncludesStylesShapesTextAndCrop(string format)
    {
        var guide=await Fixture();var before=await File.ReadAllBytesAsync(System.IO.Path.Combine(store.DirectoryFor(guide.Id),"assets/image.png"));
        guide.Steps[0].Annotations=[new("r","rectangle",.1,.1,.2,.2){Fill="#00ff00",Stroke="#ff0000",StrokeWidth=4,Shadow=true},new("e","ellipse",.5,.1,.2,.2){Fill="#0000ff"},new("l","line",.1,.5,.25,.002){Stroke="#ff0000",StrokeWidth=5},new("a","arrow",.5,.5,.25,.1){Stroke="#008000",FlipX=true},new("t","text",.1,.65,.3,.25){Text="Saved",FontSize=30,Shadow=true},new("c","callout",.55,.65,.3,.25){Text="Check",FontSize=24,Fill="#ffff00"}];guide.Steps[0].Crop=new(.1,.1,.8,.8);
        using var bitmap=new Bitmap(new MemoryStream(await Export(format,guide)));Assert.Equal(512,bitmap.Width);Assert.Equal(288,bitmap.Height);
        Assert.Equal(Color.Lime.ToArgb(),bitmap.GetPixel(64,36).ToArgb());Assert.Equal(Color.Blue.ToArgb(),bitmap.GetPixel(320,36).ToArgb());Assert.Equal(Color.Yellow.ToArgb(),bitmap.GetPixel(386,209).ToArgb());
        var ink=0;for(int y=203;y<280;y++)for(int x=0;x<192;x++)if(bitmap.GetPixel(x,y).R<180)ink++;Assert.True(ink>50,"Text must be rasterized into export pixels.");Assert.Equal(before,await File.ReadAllBytesAsync(System.IO.Path.Combine(store.DirectoryFor(guide.Id),"assets/image.png")));
    }
    [Fact] public async Task BlurChangesOnlyTheSelectedRegion()
    {
        var guide=await Fixture(true);using var before=Decode(await ExportImageRenderer.ReadSourceAsync(guide,"image",store.DirectoryFor(guide.Id)));guide.Steps[0].Annotations=[new("blur","blur",.2,.2,.4,.4){BlurRadius=12}];using var after=Decode(await ExportImageRenderer.RenderAsync(guide,guide.Steps[0],store.DirectoryFor(guide.Id)));
        Assert.Equal(before.GetPixel(10,10),after.GetPixel(10,10));Assert.InRange(after.GetPixel(200,120).R,60,195);Assert.NotEqual(before.GetPixel(200,120),after.GetPixel(200,120));
    }
    [Fact] public async Task PermanentEditsSanitizeSharedAssetsAndPortableExports()
    {
        var guide=await Fixture();guide.Steps[0].Annotations=[new("redact","redact",.1,.1,.3,.3){Fill="#000000"}];guide.Steps.Add(new(){AssetId="image",Annotations=[new("redact2","redact",.6,.1,.2,.2){Fill="#000000"}]});await store.SaveAsync(guide);
        var next=await PixelEditStorage.ApplyAsync(guide,"image",store);Assert.Single(next.PendingAssetDeletes);Assert.Throws<InvalidDataException>(()=>GuideJson.EnsurePrivacyApplied(next));var final=await PixelEditStorage.CleanupAsync(next,store);Assert.Empty(final.PendingAssetDeletes);Assert.All(final.Steps,s=>Assert.Empty(s.Annotations));
        Assert.False(File.Exists(System.IO.Path.Combine(store.DirectoryFor(guide.Id),"assets/image.png")));Assert.Single(final.Assets);Assert.All(final.Steps,s=>Assert.Equal(final.Assets[0].Id,s.AssetId));
        var bundle=System.IO.Path.Combine(root,"sanitized.capture");await store.ExportPortableAsync(final,bundle);using var zip=ZipFile.OpenRead(bundle);Assert.Equal(2,zip.Entries.Count);using var bytes=zip.GetEntry(final.Assets[0].RelativePath)!.Open();using var image=new Bitmap(bytes);Assert.Equal(Color.Black.ToArgb(),image.GetPixel(100,60).ToArgb());Assert.Equal(Color.Black.ToArgb(),image.GetPixel(430,60).ToArgb());
    }
    [Fact] public async Task FailedSaveRetainsOriginal()
    {
        var guide=await Fixture();guide.Steps[0].Annotations=[new("a","redact",.1,.1,.3,.3)];var dir=store.DirectoryFor(guide.Id);var file=System.IO.Path.Combine(dir,"assets/image.png");var original=await File.ReadAllBytesAsync(file);Directory.CreateDirectory(System.IO.Path.Combine(dir,"guide.json.tmp"));
        Assert.NotNull(await Record.ExceptionAsync(()=>PixelEditStorage.ApplyAsync(guide,"image",store)));Assert.Equal(original,await File.ReadAllBytesAsync(file));Assert.Single(Directory.GetFiles(System.IO.Path.Combine(dir,"assets"),"*.png"));
    }
    [Fact] public async Task CleanupCanResumeAfterWriteFailure()
    {
        var guide=await Fixture();guide.Steps[0].Annotations=[new("a","redact",.1,.1,.3,.3)];var next=await PixelEditStorage.ApplyAsync(guide,"image",store);var temporary=System.IO.Path.Combine(store.DirectoryFor(guide.Id),"guide.json.tmp");Directory.CreateDirectory(temporary);
        Assert.NotNull(await Record.ExceptionAsync(()=>PixelEditStorage.CleanupAsync(next,store)));Directory.Delete(temporary);var recovered=await store.LoadAsync(guide.Id);Assert.Single(recovered.PendingAssetDeletes);var clean=await PixelEditStorage.CleanupAsync(recovered,store);Assert.Empty(clean.PendingAssetDeletes);GuideJson.EnsurePrivacyApplied(clean);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
