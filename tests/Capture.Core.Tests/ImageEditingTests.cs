using System.Text.Json;
using Capture.Core;
using Xunit;
namespace Capture.Tests;
public sealed class ImageEditingTests
{
    [Theory]
    [InlineData("highlight")][InlineData("rectangle")][InlineData("ellipse")][InlineData("line")][InlineData("arrow")][InlineData("text")][InlineData("callout")][InlineData("blur")][InlineData("redact")]
    public void StyledAnnotationsAndCropRoundtrip(string kind)
    {
        var a=new Annotation("a",kind,.1,.2,.3,.4) { Stroke="#123456",Fill="#abcdef",FontSize=32,Shadow=true,Text="Synthetic note",FlipX=true };
        var g=new Guide { Steps=[new(){Annotations=[a],Crop=new(.1,.1,.8,.8)}] };
        var parsed=GuideJson.Parse(GuideJson.Serialize(g));Assert.Equal(a,parsed.Steps[0].Annotations[0]);Assert.Equal(g.Steps[0].Crop,parsed.Steps[0].Crop);
    }
    [Fact] public void InvalidPropertiesAndCropsAreRejected()
    {
        var a=new Annotation("a","text",.1,.1,.2,.2);
        foreach(var invalid in new[]{a with { Stroke="url(javascript:alert(1))" },a with { FontSize=double.NaN },a with { FontSize=300 },a with { StrokeWidth=0 },a with { BlurRadius=100 },a with { Text=new string('x',4001) }})
            Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(new Guide { Steps=[new(){Annotations=[invalid]}] }));
        Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(new Guide { Steps=[new(){Crop=new(.5,.5,.8,.8)}] }));
    }
    [Fact] public async Task LegacySaveKeepsMetadataBackupAndUpgradesSchema()
    {
        var root=Path.Combine(Path.GetTempPath(),"capture-legacy-"+Guid.NewGuid());var id=Guid.NewGuid();var dir=Path.Combine(root,id.ToString());Directory.CreateDirectory(dir);
        var json=JsonSerializer.Serialize(new {schemaVersion="1.0.0",id,title="Legacy guide",steps=Array.Empty<object>(),assets=Array.Empty<object>()});
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir,"guide.json"),json);var store=new ProjectStore(root);var g=await store.LoadAsync(id);await store.SaveAsync(g);
            Assert.Equal(json,await File.ReadAllTextAsync(Path.Combine(dir,"guide.v1.0.backup.json")));
            Assert.Equal("1.1.0",(await store.LoadAsync(id)).SchemaVersion);
        }
        finally {Directory.Delete(root,true);}
    }
    [Theory][InlineData("blur")][InlineData("redact")]
    public async Task EveryExportRejectsUnappliedPixelEdits(string kind)
    {
        var g=new Guide { Steps=[new(){Annotations=[new("sensitive",kind,.1,.1,.2,.2)]}] };
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".tmp");
        Task<ExportImage> Render(Step s)=>throw new Exception("Must be blocked before rendering.");
        await Assert.ThrowsAsync<InvalidDataException>(()=>DocumentExports.DocxAsync(g,path,Render));
        await Assert.ThrowsAsync<InvalidDataException>(()=>DocumentExports.MarkdownAsync(g,path,Render));
        await Assert.ThrowsAsync<InvalidDataException>(()=>DocumentExports.HtmlAsync(g,path,Render));
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ProjectStore(Path.GetTempPath()).ExportPortableAsync(g,path));
        Assert.False(File.Exists(path));
    }
    [Fact] public void CleanupMustStayInsideUnreferencedAssetPaths()
    {
        Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(new Guide {PendingAssetDeletes=["../secret"]}));
        Assert.Throws<InvalidDataException>(()=>GuideJson.Serialize(new Guide {Assets=[new("a","assets/a.png","image/png",1,1,new string('0',64))],PendingAssetDeletes=["assets/a.png"]}));
        Assert.Throws<InvalidDataException>(()=>GuideJson.EnsurePrivacyApplied(new Guide {PendingAssetDeletes=["assets/original.png"]}));
    }
}
