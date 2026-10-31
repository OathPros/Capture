using System.Security.Cryptography;
using System.Text;
using AngleSharp.Html.Parser;
using Capture.Core;
using DocumentFormat.OpenXml.Packaging;
using Xunit;
using W=DocumentFormat.OpenXml.Wordprocessing;
namespace Capture.Tests;
public sealed class VersionFourTests
{
    [Theory][InlineData("Button","Save draft","Select Save draft")][InlineData("Edit","Article title","Enter information in Article title")][InlineData("TabItem","Knowledge Base","Open the Knowledge Base tab")][InlineData("CheckBox","Show names","Change the Show names option")]
    public void GeneratedTitlesPreserveLabelsAndLeaveDetailsBlank(string type,string label,string expected)
    {var s=StepText.FromControl(type,label);Assert.Equal(expected,s.Title);Assert.Equal([label],s.TitleFields);Assert.Empty(s.Instruction);}
    [Theory][InlineData(null)][InlineData("None")][InlineData(" ")][InlineData("pane")]
    public void MissingLabelsStayHonest(string? label)
    {var s=StepText.FromControl("Button",label);Assert.Equal("Select the highlighted control",s.Title);Assert.Empty(s.TitleFields);Assert.Empty(s.Instruction);}
    [Theory][InlineData("articleTitleTextBox","Article title")][InlineData("searchInput","Search")][InlineData("input_9292",null)][InlineData("johndoe_email",null)]
    public void StableFieldIdentifiersCanSupplyLabelsWithoutReadingValues(string id,string? expected)=>Assert.Equal(expected,StepText.FieldLabelFromId(id));
    [Fact] public void FieldsAreBoldWithoutChangingProseOrInterpretingHtml()
    {
        Assert.Equal("Select <strong>Select</strong>",StepText.Html("Select Select",["Select"]));
        Assert.Equal("Select GUID",StepText.Html("Select GUID",["ID"]));
        Assert.Equal("Choose **Save draft**",StepText.Markdown("Choose Save draft",["Save draft"]));
        var html=StepText.Html("Select <script>",["<script>"]);var dom=new HtmlParser().ParseDocument(html);Assert.Empty(dom.Scripts);Assert.Equal("<script>",dom.QuerySelector("strong")!.TextContent);
    }
    [Fact] public void LegacyRepeatedDetailsBecomeBlankAndDetectedNamesBecomeBold()
    {
        var g=new Guide{SchemaVersion="1.1.0",Steps=[new(){Title="Select Save draft",Instruction="Select Save draft."},new(){Title="Open settings",Instruction="Then choose the training environment."}]};
        var parsed=GuideJson.Parse(GuideJson.Serialize(g));Assert.Empty(parsed.Steps[0].Instruction);Assert.Equal(["Save draft"],parsed.Steps[0].TitleFields);Assert.Equal("Then choose the training environment.",parsed.Steps[1].Instruction);
    }
    [Fact] public void ControlBoundsUsePhysicalPixelsIncludingNegativeMonitorOrigins()
    {
        var frame=new ControlBounds(-1920,0,1920,1080);var a=CaptureTargets.Highlight(-800,250,new(-900,220,300,60),frame);
        Assert.Equal("highlight",a.Kind);Assert.Equal((1020-3)/1920.0,a.X,6);Assert.Equal(306/1920.0,a.Width,6);Assert.Equal(66/1080.0,a.Height,6);
        var b=CaptureTargets.Highlight(-800,250,new(-1920,0,1920,1080),frame);Assert.Equal("ellipse",b.Kind);
        var c=CaptureTargets.Highlight(-800,250,new(-100,200,80,30),frame);Assert.Equal("ellipse",c.Kind);
    }
    [Fact] public void StripCutsMergeAndJoinBothAxesAndProjectAnnotations()
    {
        var layout=ImageLayout.Create(1000,800,null,[new("horizontal",0,.1),new("horizontal",.3,.4),new("horizontal",.35,.45),new("vertical",.2,.3)]);
        Assert.Equal(900,layout.Width);Assert.Equal(600,layout.Height);Assert.Equal(4,layout.Tiles.Count());
        var p=layout.Project(new(400,400,200,80));Assert.Equal(300,p.X);Assert.Equal(200,p.Y);Assert.Equal(200,p.Width);Assert.Equal(80,p.Height);
    }
    [Fact] public void InvalidAndEmptyCutsAreRejected()
    {
        var g=new Guide{Assets=[new("a","assets/a.png","image/png",100,80,new string('0',64))],Steps=[new(){AssetId="a",Cuts=[new("horizontal",0,1)]}]};
        Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(g));g.Steps[0].Cuts=[new("diagonal",.1,.2)];Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(g));
    }
    [Fact] public async Task HtmlCodeMatchesExportAndWordClipboardEmbedsScreenshotsAndBoldFields()
    {
        var png=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"fixtures/landscape.png"));
        var g=new Guide{Title="Synthetic clipboard guide 😀",Summary="No temporary export files required.",Assets=[new("a","assets/a.png","image/png",1600,800,Convert.ToHexString(SHA256.HashData(png)))],Steps=[StepText.FromControl("Button","Save draft")]};g.Steps[0].AssetId="a";
        Task<ExportImage> Render(Step s)=>Task.FromResult(new ExportImage(png,1600,800));
        var html=await DocumentExports.HtmlCodeAsync(g,Render);var dom=new HtmlParser().ParseDocument(html);Assert.Equal("Save draft",dom.QuerySelector("article h2 strong")!.TextContent);Assert.Empty(dom.QuerySelectorAll(".instruction"));Assert.Single(dom.Images);
        var word=await WordClipboard.CreateAsync(g,Render);Assert.Contains(@"\b Save draft\b0",word.Rtf);Assert.Contains(@"\pict\pngblip",word.Rtf);Assert.Contains(Convert.ToHexString(png)[..64],word.Rtf);Assert.DoesNotContain("**",word.Text);Assert.Contains("1. Select Save draft",word.Text);Assert.Contains(@"\u-",word.Rtf);
        var file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".html");try{await DocumentExports.HtmlAsync(g,file,Render);Assert.Equal(html,await File.ReadAllTextAsync(file));}finally{File.Delete(file);}
        var samples=Environment.GetEnvironmentVariable("CAPTURE_EXPORT_SAMPLES");if(samples is not null){Directory.CreateDirectory(samples);await File.WriteAllTextAsync(Path.Combine(samples,"synthetic-word-clipboard.rtf"),word.Rtf,Encoding.ASCII);}
        var docx=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");try{await DocumentExports.DocxAsync(g,docx,Render);using var doc=WordprocessingDocument.Open(docx,false);var heading=doc.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single(p=>p.ParagraphProperties?.ParagraphStyleId?.Val?.Value=="Heading1");Assert.Equal("Save draft",heading.Elements<W.Run>().Single(r=>r.RunProperties?.Bold is not null).InnerText);}finally{File.Delete(docx);}
    }
    [Fact] public async Task ClipboardRejectsUnappliedPrivacyEditsBeforeRendering()
    {
        var g=new Guide{Steps=[new(){Annotations=[new("a","redact",.1,.1,.2,.2)]}]};Task<ExportImage> Never(Step s)=>throw new Exception("Must not render before privacy validation.");
        await Assert.ThrowsAsync<InvalidDataException>(()=>WordClipboard.CreateAsync(g,Never));await Assert.ThrowsAsync<InvalidDataException>(()=>DocumentExports.HtmlCodeAsync(g,Never));
    }
}
