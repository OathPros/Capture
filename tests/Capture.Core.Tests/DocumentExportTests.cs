using System.IO.Compression;
using System.Security.Cryptography;
using AngleSharp.Html.Parser;
using Capture.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Capture.Tests;

public sealed class DocumentExportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "capture-exports-" + Guid.NewGuid());
    private readonly Dictionary<string, ExportImage> images = new()
    {
        ["landscape"] = new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"fixtures/landscape.png")),1600,800),
        ["portrait"] = new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"fixtures/portrait.png")),800,1600)
    };
    public DocumentExportTests() => Directory.CreateDirectory(root);
    private Guide Fixture()
    {
        var g = new Guide { Title = "Synthetic export test guide <script>alert('title')</script>", Summary = "A fictional training workflow.\nSecond line & details. 😀" };
        foreach(var (id,image) in images)
            g.Assets.Add(new(id,$"assets/{id}.png","image/png",image.Width,image.Height,Convert.ToHexString(SHA256.HashData(image.Png))));
        g.Assets.Add(new("unused","assets/deleted.png","image/png",1,1,new string('0',64)));
        for(int i=0;i<12;i++)
            g.Steps.Add(new() { Title=$"Action {i+1} <b>literal</b>", Instruction="Select the fictional control.\n" + new string('x',i==5?3000:30), Notes=$"Note {i+1} & <img src=x onerror=alert(1)> \"quoted\"", AssetId=i==3?null:(i%2==0?"landscape":"portrait"), Annotations=[new("a","highlight",.2,.25,.4,.25)] });
        return g;
    }
    private Task<ExportImage> Render(Step step) => Task.FromResult(images[step.AssetId!]);

    [Fact] public async Task MarkdownZipContainsAllTextAndResolvesEveryImage()
    {
        var guide=Fixture();var file=Path.Combine(root,"guide.zip");await DocumentExports.MarkdownAsync(guide,file,Render);
        using var zip=ZipFile.OpenRead(file);
        using var reader=new StreamReader(zip.GetEntry("README.md")!.Open());var markdown=await reader.ReadToEndAsync();
        var html=Markdig.Markdown.ToHtml(markdown);var dom=new HtmlParser().ParseDocument(html);
        Assert.Equal(guide.Title,dom.QuerySelector("h1")!.TextContent);
        Assert.Equal(guide.Steps.Select((s,i)=>$"{i+1}. {s.Title}"),dom.QuerySelectorAll("h2").Select(e=>e.TextContent));
        Assert.Empty(dom.QuerySelectorAll("script, b, [onerror]"));
        Assert.Contains("Note 12",dom.Body!.TextContent);Assert.Contains(new string('x',3000),dom.Body.TextContent);
        var references=dom.Images.Select(i=>i.GetAttribute("src")!).ToArray();Assert.Equal(11,references.Length);
        foreach(var reference in references) Assert.NotNull(zip.GetEntry(reference));
        Assert.Equal(12,zip.Entries.Count);Assert.DoesNotContain("assets/deleted.png",zip.Entries.Select(e=>e.FullName));
        foreach(var (step,i) in guide.Steps.Select((step,i)=>(step,i)).Where(x=>x.step.AssetId is not null))
        {
            using var source=zip.GetEntry($"assets/step-{i+1:D3}.png")!.Open();using var output=new MemoryStream();await source.CopyToAsync(output);
            Assert.Equal(images[step.AssetId!].Png,output.ToArray());
        }
    }

    [Fact] public async Task DocxIsSchemaValidAndEmbedsOrderedAccessibleImages()
    {
        var guide=Fixture();var file=Path.Combine(root,"guide.docx");await DocumentExports.DocxAsync(guide,file,Render);
        using var doc=WordprocessingDocument.Open(file,false);var main=doc.MainDocumentPart!;
        var validation=new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).ToArray();
        Assert.True(validation.Length==0,string.Join("\n",validation.Select(e=>e.Description+" "+e.Path?.XPath)));
        var paragraphs=main.Document!.Body!.Elements<W.Paragraph>().ToArray();
        Assert.Equal(guide.Title,paragraphs[0].InnerText);
        Assert.Contains("😀",main.Document!.InnerText);Assert.Contains("Note 12",main.Document!.InnerText);Assert.Contains(new string('x',3000),main.Document!.InnerText);
        Assert.Equal(guide.Steps.Select((s,i)=>$"{i+1}. {s.Title}"),paragraphs.Where(p=>p.ParagraphProperties?.ParagraphStyleId?.Val?.Value=="Heading1").Select(p=>p.InnerText));
        Assert.Equal(11,main.ImageParts.Count());
        Assert.Single(main.FooterParts);
        Assert.Contains(main.FooterParts.Single().Footer!.Descendants<W.SimpleField>(), f=>f.Instruction!.Value!.Contains("PAGE"));
        var blips=main.Document!.Descendants<A.Blip>().ToArray();
        var expected=guide.Steps.Where(s=>s.AssetId is not null).ToArray();
        for(int i=0;i<blips.Length;i++)
        {
            using var source=main.GetPartById(blips[i].Embed!).GetStream();using var output=new MemoryStream();await source.CopyToAsync(output);
            Assert.Equal(images[expected[i].AssetId!].Png,output.ToArray());
        }
        var extents=main.Document!.Descendants<DW.Extent>().ToArray();
        for(int i=0;i<extents.Length;i++)
        {
            var image=images[expected[i].AssetId!];var extent=extents[i];
            Assert.InRange(extent.Cx!.Value,1,5943600);Assert.InRange(extent.Cy!.Value,1,7315200);
            Assert.Equal((double)image.Width/image.Height,(double)extent.Cx.Value/extent.Cy.Value,5);
        }
        Assert.All(main.Document!.Descendants<DW.DocProperties>(),p=>Assert.StartsWith("Screenshot for step",p.Description!.Value));
        Assert.Equal("en-CA",main.StyleDefinitionsPart!.Styles!.Descendants<W.Languages>().First().Val!.Value);
        Assert.Contains(main.StyleDefinitionsPart.Styles.Descendants<W.Style>(),s=>s.StyleId=="Heading1" && s.Descendants<W.KeepNext>().Any());
    }

    [Fact] public async Task HtmlIsSelfContainedEscapesContentAndIncludesPrintLayout()
    {
        var guide=Fixture();var file=Path.Combine(root,"guide.html");await DocumentExports.HtmlAsync(guide,file,Render);
        var html=await File.ReadAllTextAsync(file);var dom=new HtmlParser().ParseDocument(html);
        Assert.Equal(guide.Title,dom.Title);Assert.Equal(guide.Title,dom.QuerySelector("h1")!.TextContent);
        Assert.Equal(12,dom.QuerySelectorAll("article.step").Length);Assert.Equal(12,dom.QuerySelectorAll("nav a").Length);
        Assert.Empty(dom.QuerySelectorAll("script, link, iframe, [onerror]"));
        Assert.Contains("default-src 'none'",dom.QuerySelector("meta[http-equiv='Content-Security-Policy']")!.GetAttribute("content"));
        Assert.Contains("@media print",dom.QuerySelector("style")!.TextContent);
        var expected=guide.Steps.Where(s=>s.AssetId is not null).ToArray();Assert.Equal(11,dom.Images.Length);
        for(int i=0;i<dom.Images.Length;i++)
        {
            var image=dom.Images[i];Assert.StartsWith("data:image/png;base64,",image.Source);
            Assert.Equal(images[expected[i].AssetId!].Png,Convert.FromBase64String(image.GetAttribute("src")!.Split(',')[1]));
            Assert.StartsWith("Screenshot for step",image.AlternativeText);
        }
        Assert.Contains("Note 12",dom.Body!.TextContent);Assert.Contains(new string('x',3000),dom.Body.TextContent);
        foreach(var a in dom.QuerySelectorAll("a")) Assert.NotNull(dom.QuerySelector(a.GetAttribute("href")!));
        Assert.DoesNotContain("assets/deleted.png",html);Assert.DoesNotContain(root,html);
        var samples=Environment.GetEnvironmentVariable("CAPTURE_EXPORT_SAMPLES");
        if(!string.IsNullOrEmpty(samples))
        {
            Directory.CreateDirectory(samples);
            foreach(var name in new[]{"synthetic-guide.html","synthetic-guide.docx","synthetic-guide.zip"})
            { var existing=Path.Combine(samples,name);if(File.Exists(existing))File.Delete(existing); }
            var sample=GuideJson.Parse(GuideJson.Serialize(guide));sample.Title="Create a training article";
            sample.Summary="A synthetic example of a clear, shareable workflow. Follow these steps to draft and review a training article.";
            sample.Steps=sample.Steps.Take(3).ToList();
            var titles=new[]{"Open the training portal","Write the article","Review and save"};
            var instructions=new[]{"Open the fictional training portal and choose New article.","Add a clear title and describe the steps in the article editor.","Review the content for accuracy, then save your draft."};
            for(int i=0;i<sample.Steps.Count;i++){sample.Steps[i].Title=titles[i];sample.Steps[i].Instruction=instructions[i];sample.Steps[i].Notes=i==1?"Use fictional information in this training example.":"";}
            await DocumentExports.HtmlAsync(sample,Path.Combine(samples,"synthetic-guide.html"),Render);
            await DocumentExports.DocxAsync(sample,Path.Combine(samples,"synthetic-guide.docx"),Render);
            await DocumentExports.MarkdownAsync(sample,Path.Combine(samples,"synthetic-guide.zip"),Render);
        }
    }

    [Theory] [InlineData("docx")] [InlineData("markdown")] [InlineData("html")]
    public async Task TextOnlyGuidesExportWithoutRequestingAnImage(string format)
    {
        var g=new Guide { Steps=[new(){Title="Text step",Instruction="No screenshot required",Notes="Retain this note"}] };
        Task<ExportImage> Never(Step step)=>throw new InvalidOperationException("Text-only steps must not request screenshots.");
        var file=Path.Combine(root,"text."+format);
        await Export(format,g,file,Never);Assert.True(new FileInfo(file).Length>100);
    }
    [Theory] [InlineData("docx")] [InlineData("markdown")] [InlineData("html")]
    public async Task MissingImagesFailInsteadOfSilentlyDroppingScreenshots(string format)
    {
        Task<ExportImage> Missing(Step step)=>throw new InvalidDataException("Missing screenshot");
        await Assert.ThrowsAsync<InvalidDataException>(()=>Export(format,Fixture(),Path.Combine(root,"missing."+format),Missing));
    }
    private static Task Export(string format,Guide guide,string path,Func<Step,Task<ExportImage>> render)=>format switch
    { "docx"=>DocumentExports.DocxAsync(guide,path,render),"html"=>DocumentExports.HtmlAsync(guide,path,render),_=>DocumentExports.MarkdownAsync(guide,path,render) };
    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
}
