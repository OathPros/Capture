using System.Security.Cryptography;
using AngleSharp.Html.Parser;
using Capture.Core;
using Xunit;

namespace Capture.Tests;

public sealed class SectionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "capture-sections-" + Guid.NewGuid());
    public SectionTests() => Directory.CreateDirectory(root);

    private static Guide Fixture()
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aXioAAAAASUVORK5CYII=");
        var install = new Section { Title = "Installation" };
        var config = new Section { Title = "Configuration", RestartNumbering = true };
        var g = new Guide { Title = "Sectioned guide", Sections = [install, config] };
        g.Assets.Add(new("image", "assets/image.png", "image/png", 1, 1, Convert.ToHexString(SHA256.HashData(bytes))));
        g.Steps.Add(new() { Title = "Welcome", Instruction = "Overview." });
        g.Steps.Add(new() { Title = "Download", SectionId = install.Id });
        g.Steps.Add(new() { Title = "Run setup", SectionId = install.Id, AssetId = "image" });
        g.Steps.Add(new() { Title = "Set options", SectionId = config.Id });
        g.Steps.Add(new() { Title = "Save", SectionId = config.Id });
        g.Steps.Add(new() { Title = "Finish" });
        return g;
    }
    private static Task<ExportImage> Render(Step step)
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aXioAAAAASUVORK5CYII=");
        return Task.FromResult(new ExportImage(bytes, 1, 1));
    }

    [Fact] public void SectionsRoundtripAndStepsKeepMembership()
    {
        var guide = Fixture();
        var parsed = GuideJson.Parse(GuideJson.Serialize(guide));
        Assert.Equal("1.3.0", parsed.SchemaVersion);
        Assert.Equal(2, parsed.Sections.Count);
        Assert.Equal(guide.Sections[0].Id, parsed.Steps[1].SectionId);
        Assert.True(parsed.Sections[1].RestartNumbering);
        Assert.Null(parsed.Steps[0].SectionId);
    }
    [Fact] public void OlderSchemasWithoutSectionsStillParse()
    {
        var guide = new Guide { SchemaVersion = "1.2.0", Steps = [new() { Title = "Plain" }] };
        var parsed = GuideJson.Parse(GuideJson.Serialize(guide));
        Assert.Empty(parsed.Sections);
        Assert.Null(parsed.Steps[0].SectionId);
    }
    [Fact] public void UnknownSectionReferenceIsRejected()
    {
        var guide = Fixture();
        guide.Steps[0].SectionId = Guid.NewGuid();
        Assert.Throws<InvalidDataException>(() => GuideJson.Serialize(guide));
    }
    [Fact] public void DuplicateSectionIdsAreRejected()
    {
        var guide = Fixture();
        guide.Sections.Add(new Section { Id = guide.Sections[0].Id });
        Assert.Throws<InvalidDataException>(() => GuideJson.Serialize(guide));
    }
    [Fact] public void NumberingRestartsOnlyInsideRestartSections()
    {
        var guide = Fixture();
        Assert.Equal([1, 2, 3, 1, 2, 4], GuideNumbering.StepNumbers(guide));
        guide.Sections[1].RestartNumbering = false;
        Assert.Equal([1, 2, 3, 4, 5, 6], GuideNumbering.StepNumbers(guide));
    }
    [Fact] public void NumberingRestartsAgainForNonContiguousRuns()
    {
        var restart = new Section { Title = "Restart", RestartNumbering = true };
        var g = new Guide { Sections = [restart] };
        g.Steps.Add(new() { Title = "A", SectionId = restart.Id });
        g.Steps.Add(new() { Title = "B" });
        g.Steps.Add(new() { Title = "C", SectionId = restart.Id });
        Assert.Equal([1, 1, 1], GuideNumbering.StepNumbers(g));
    }
    [Fact] public void RunsGroupContiguousStepsBySection()
    {
        var guide = Fixture();
        var runs = GuideNumbering.Runs(guide).ToArray();
        Assert.Equal(4, runs.Length);
        Assert.Null(runs[0].Section); Assert.Equal((0, 1), (runs[0].Start, runs[0].Count));
        Assert.Equal("Installation", runs[1].Section!.Title); Assert.Equal((1, 2), (runs[1].Start, runs[1].Count));
        Assert.Equal("Configuration", runs[2].Section!.Title); Assert.Equal((3, 2), (runs[2].Start, runs[2].Count));
        Assert.Null(runs[3].Section); Assert.Equal((5, 1), (runs[3].Start, runs[3].Count));
    }
    [Fact] public async Task HtmlGroupsStepsUnderSectionsWithRestartedNumbersAndLinkedToc()
    {
        var guide = Fixture(); var file = Path.Combine(root, "sectioned.html");
        await DocumentExports.HtmlAsync(guide, file, Render);
        var dom = new HtmlParser().ParseDocument(await File.ReadAllTextAsync(file));
        Assert.Equal(2, dom.QuerySelectorAll("section.guide-section").Length);
        Assert.Equal("Installation", dom.QuerySelectorAll("section.guide-section .section-title")[0].TextContent);
        Assert.Equal("Configuration", dom.QuerySelectorAll("section.guide-section .section-title")[1].TextContent);
        Assert.Equal(2, dom.QuerySelectorAll("section.guide-section")[0].QuerySelectorAll("article.step").Length);
        Assert.Equal(["01", "02", "03", "01", "02", "04"], dom.QuerySelectorAll("article.step .number").Select(e => e.TextContent).ToArray());
        var tocSections = dom.QuerySelectorAll("nav.contents .toc-section");
        Assert.Equal(2, tocSections.Length);
        Assert.Equal(2, tocSections[0].QuerySelectorAll("a[href^='#step-']").Length);
        foreach (var a in dom.QuerySelectorAll("nav.contents a")) Assert.NotNull(dom.QuerySelector(a.GetAttribute("href")!));
        Assert.All(dom.QuerySelectorAll("nav.contents details"), d => Assert.NotNull(d.GetAttribute("open")));
    }
    [Fact] public async Task DocxIncludesSectionHeadingsAndRestartedNumbers()
    {
        var guide = Fixture(); var file = Path.Combine(root, "sectioned.docx");
        await DocumentExports.DocxAsync(guide, file, Render);
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(file, false);
        var paragraphs = doc.MainDocumentPart!.Document!.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToArray();
        Assert.Equal(["Installation", "Configuration"], paragraphs.Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading2").Select(p => p.InnerText).ToArray());
        Assert.Equal(["1. Welcome", "2. Download", "3. Run setup", "1. Set options", "2. Save", "4. Finish"], paragraphs.Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading1").Select(p => p.InnerText).ToArray());
    }
    [Fact] public void MarkdownNestsStepsUnderSectionHeadings()
    {
        var markdown = ProjectStore.Markdown(Fixture()).Replace("\r\n","\n");
        Assert.Contains("\n## Installation\n", markdown);
        Assert.Contains("\n### 2. Download\n", markdown);
        Assert.Contains("\n## Configuration\n", markdown);
        Assert.Contains("\n### 1. Set options\n", markdown);
        Assert.Contains("\n## 4. Finish\n", markdown);
    }
    [Fact] public async Task WordClipboardIncludesSectionHeadingsAndRestartedNumbers()
    {
        var content = await WordClipboard.CreateAsync(Fixture(), Render);
        Assert.Contains("Installation", content.Rtf);
        Assert.Contains("1. Set options", content.Rtf);
        Assert.Contains("4. Finish", content.Text);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
