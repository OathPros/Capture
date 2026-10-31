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
public sealed class ExportImageTests : IDisposable
{
    private readonly string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"capture-image-test-"+Guid.NewGuid());
    private async Task<(Guide guide,Step step,byte[] original)> Fixture()
    {
        Directory.CreateDirectory(System.IO.Path.Combine(root,"assets"));
        using var bitmap=new Bitmap(100,80);using(var g=Graphics.FromImage(bitmap))g.Clear(Color.White);
        var path=System.IO.Path.Combine(root,"assets/image.png");bitmap.Save(path,ImageFormat.Png);var bytes=await File.ReadAllBytesAsync(path);
        var step=new Step { AssetId="image",Annotations=[new("a","highlight",.2,.25,.4,.25)] };
        var guide=new Guide { Steps=[step],Assets=[new("image","assets/image.png","image/png",100,80,Convert.ToHexString(SHA256.HashData(bytes)))] };
        return (guide,step,bytes);
    }
    [Theory] [InlineData("docx")] [InlineData("markdown")] [InlineData("html")]
    public async Task EveryFormatContainsRasterizedHighlightsAndLeavesSourceUntouched(string format)
    {
        var (guide,step,original)=await Fixture();
        Task<ExportImage> Render(Step s)=>ExportImageRenderer.RenderAsync(guide,s,root);
        var path=System.IO.Path.Combine(root,"export."+format);
        byte[] png;
        if(format=="docx")
        {
            await DocumentExports.DocxAsync(guide,path,Render);using var doc=WordprocessingDocument.Open(path,false);
            var main=doc.MainDocumentPart!;using var input=main.GetPartById(main.Document!.Descendants<Blip>().Single().Embed!).GetStream();using var output=new MemoryStream();input.CopyTo(output);png=output.ToArray();
        }
        else if(format=="markdown")
        {
            await DocumentExports.MarkdownAsync(guide,path,Render);using var zip=ZipFile.OpenRead(path);using var input=zip.GetEntry("assets/step-001.png")!.Open();using var output=new MemoryStream();input.CopyTo(output);png=output.ToArray();
        }
        else
        {
            await DocumentExports.HtmlAsync(guide,path,Render);var html=await File.ReadAllTextAsync(path);var prefix="data:image/png;base64,";var start=html.IndexOf(prefix,StringComparison.Ordinal)+prefix.Length;png=Convert.FromBase64String(html[start..html.IndexOf('"',start)]);
        }
        using var image=new Bitmap(new MemoryStream(png));
        Assert.Equal(Color.FromArgb(227,24,55).ToArgb(),image.GetPixel(20,20).ToArgb());
        Assert.Equal(Color.White.ToArgb(),image.GetPixel(40,30).ToArgb());
        Assert.Equal(original,await File.ReadAllBytesAsync(System.IO.Path.Combine(root,"assets/image.png")));
    }
    [Fact] public async Task RendererRejectsChangedSourceImage()
    {
        var (guide,step,_)=await Fixture();guide.Assets[0]=guide.Assets[0] with { Sha256=new string('0',64) };
        await Assert.ThrowsAsync<InvalidDataException>(()=>ExportImageRenderer.RenderAsync(guide,step,root));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
