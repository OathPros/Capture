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
public sealed class VersionFourWindowsTests : IDisposable
{
    private readonly string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"capture-v4-"+Guid.NewGuid());
    [Theory][InlineData("docx")][InlineData("markdown")][InlineData("html")]
    public async Task CutStripsJoinPixelsWithoutChangingOriginalInEveryFormat(string format)
    {
        var store=new ProjectStore(root);var g=new Guide{Steps=[new(){AssetId="a",Cuts=[new("horizontal",0,.1),new("horizontal",.3,.4),new("vertical",.2,.3)]}]};
        var dir=System.IO.Path.Combine(store.DirectoryFor(g.Id),"assets");Directory.CreateDirectory(dir);
        using var original=new Bitmap(100,100);for(int y=0;y<100;y++)for(int x=0;x<100;x++)original.SetPixel(x,y,Color.FromArgb(x*2,y*2,(x+y)%255));
        var source=System.IO.Path.Combine(dir,"a.png");original.Save(source,ImageFormat.Png);var before=await File.ReadAllBytesAsync(source);g.Assets=[new("a","assets/a.png","image/png",100,100,Convert.ToHexString(SHA256.HashData(before)))];
        Task<ExportImage> Render(Step step)=>ExportImageRenderer.RenderAsync(g,step,store.DirectoryFor(g.Id));
        var path=System.IO.Path.Combine(root,"guide."+format);byte[] png;
        if(format=="docx")
        {await DocumentExports.DocxAsync(g,path,Render);using var doc=WordprocessingDocument.Open(path,false);var main=doc.MainDocumentPart!;using var stream=main.GetPartById(main.Document!.Descendants<Blip>().Single().Embed!).GetStream();using var output=new MemoryStream();stream.CopyTo(output);png=output.ToArray();}
        else if(format=="markdown")
        {await DocumentExports.MarkdownAsync(g,path,Render);using var zip=ZipFile.OpenRead(path);using var stream=zip.GetEntry("assets/step-001.png")!.Open();using var output=new MemoryStream();stream.CopyTo(output);png=output.ToArray();}
        else
        {await DocumentExports.HtmlAsync(g,path,Render);var html=await File.ReadAllTextAsync(path);var start=html.IndexOf("data:image/png;base64,",StringComparison.Ordinal)+"data:image/png;base64,".Length;png=Convert.FromBase64String(html[start..html.IndexOf('"',start)]);}
        using var image=new Bitmap(new MemoryStream(png));Assert.Equal(90,image.Width);Assert.Equal(80,image.Height);
        Assert.Equal(original.GetPixel(0,10),image.GetPixel(0,0));Assert.Equal(original.GetPixel(30,40),image.GetPixel(20,20));Assert.Equal(original.GetPixel(99,99),image.GetPixel(89,79));Assert.Equal(before,await File.ReadAllBytesAsync(source));
    }
    [Fact] public async Task ClipboardPublishesHtmlAsCodeAndWordAsEmbeddedRichText()
    {
        Directory.CreateDirectory(root);var g=new Guide{Title="Synthetic clipboard v4",Steps=[StepText.FromControl("Button","Save draft")]};
        using var bitmap=new Bitmap(32,16);using(var graphics=Graphics.FromImage(bitmap))graphics.Clear(Color.CornflowerBlue);using var output=new MemoryStream();bitmap.Save(output,ImageFormat.Png);var png=output.ToArray();
        g.Assets=[new("a","assets/a.png","image/png",32,16,Convert.ToHexString(SHA256.HashData(png)))];g.Steps[0].AssetId="a";
        Task<ExportImage> Render(Step s)=>Task.FromResult(new ExportImage(png,32,16));var word=await WordClipboard.CreateAsync(g,Render);var html=await DocumentExports.HtmlCodeAsync(g,Render);
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                ClipboardWriter.HtmlCode(html);Assert.Equal(html,System.Windows.Forms.Clipboard.GetText());Assert.False(System.Windows.Forms.Clipboard.ContainsData(System.Windows.Forms.DataFormats.Html));
                ClipboardWriter.WordSteps(word);Assert.True(System.Windows.Forms.Clipboard.ContainsData(System.Windows.Forms.DataFormats.Rtf));Assert.Contains(@"\pngblip",System.Windows.Forms.Clipboard.GetText(System.Windows.Forms.TextDataFormat.Rtf));Assert.Equal(word.Text,System.Windows.Forms.Clipboard.GetText());Assert.False(System.Windows.Forms.Clipboard.ContainsData(System.Windows.Forms.DataFormats.Html));
                Assert.Empty(Directory.GetFiles(root));done.SetResult();
            }
            catch(Exception ex){done.SetException(ex);}
            finally {System.Windows.Forms.Clipboard.Clear();}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
