using System.IO.Compression;
using System.Text;
using System.Net;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Capture.Core;

public sealed record ExportImage(byte[] Png, int Width, int Height);

/// <summary>Document projections of a guide. The host supplies flattened export-only screenshots.</summary>
public static class DocumentExports
{
    public static async Task MarkdownAsync(Guide guide, string target, Func<Step, Task<ExportImage>> render)
    {
        var snapshot = GuideJson.Parse(GuideJson.Serialize(guide));
        GuideJson.EnsurePrivacyApplied(snapshot);
        await using var file = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        await using (var output = zip.CreateEntry("README.md").Open())
        await using (var writer = new StreamWriter(output, new UTF8Encoding(false)))
            await writer.WriteAsync(ProjectStore.Markdown(snapshot));
        for (int i = 0; i < snapshot.Steps.Count; i++)
        {
            var step = snapshot.Steps[i];
            if (step.AssetId is null) continue;
            var image = await render(step);
            ValidateImage(image);
            await using var output = zip.CreateEntry($"assets/step-{i+1:D3}.png").Open();
            await output.WriteAsync(image.Png);
        }
    }

    public static async Task DocxAsync(Guide guide, string target, Func<Step, Task<ExportImage>> render)
    {
        var snapshot = GuideJson.Parse(GuideJson.Serialize(guide));
        GuideJson.EnsurePrivacyApplied(snapshot);
        using var file = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite);
        using var document = WordprocessingDocument.Create(file, WordprocessingDocumentType.Document);
        document.PackageProperties.Title = XmlText(snapshot.Title);
        document.PackageProperties.Description = "CAPTURE DOCX export 1.0; guide schema " + snapshot.SchemaVersion;
        var main = document.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        var body = main.Document.Body!;
        AddStyles(main);
        body.Append(Paragraph(snapshot.Title, "Title"));
        if (!string.IsNullOrWhiteSpace(snapshot.Summary)) body.Append(Paragraph(snapshot.Summary));
        uint drawingId = 1;
        for (int i = 0; i < snapshot.Steps.Count; i++)
        {
            var step = snapshot.Steps[i];
            body.Append(Paragraph($"{i+1}. {step.Title}", "Heading1", step.TitleFields));
            if(!string.IsNullOrWhiteSpace(step.Instruction))
            {
                var instruction = Paragraph(step.Instruction,fields:step.TitleFields);
                if (step.AssetId is not null) instruction.PrependChild(new W.ParagraphProperties(new W.KeepNext()));
                body.Append(instruction);
            }
            if (!string.IsNullOrWhiteSpace(step.Notes))
            {
                var notes = Paragraph(step.Notes);
                if (step.AssetId is not null) notes.PrependChild(new W.ParagraphProperties(new W.KeepNext()));
                body.Append(notes);
            }
            if (step.AssetId is null) continue;
            var image = await render(step);
            ValidateImage(image);
            var part = main.AddImagePart(ImagePartType.Png);
            using (var bytes = new MemoryStream(image.Png, false)) part.FeedData(bytes);
            // Letter paper, one-inch margins; reserve space for step text and preserve image aspect ratio.
            const double emuPerPixel = 914400.0 / 96;
            var scale = Math.Min(1, Math.Min(6.5 * 914400 / (image.Width * emuPerPixel), 6.5 * 914400 / (image.Height * emuPerPixel)));
            var width = Math.Max(1, (long)Math.Round(image.Width * emuPerPixel * scale));
            var height = Math.Max(1, (long)Math.Round(image.Height * emuPerPixel * scale));
            body.Append(ImageParagraph(main.GetIdOfPart(part), drawingId++, width, height, $"Screenshot for step {i+1}: {step.Title}"));
        }
        var footer = main.AddNewPart<FooterPart>();
        footer.Footer = new W.Footer(new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Right }),
            new W.Run(new W.RunProperties(new W.Color { Val = "666666" }, new W.FontSize { Val = "18" }), new W.Text("Page ") { Space = SpaceProcessingModeValues.Preserve }),
            new W.SimpleField(new W.Run(new W.Text("1"))) { Instruction = " PAGE " }));
        footer.Footer.Save();
        body.Append(new W.SectionProperties(
            new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
            new W.PageSize { Width = 12240U, Height = 15840U },
            new W.PageMargin { Top = 1440, Right = 1440U, Bottom = 1440, Left = 1440U, Header = 720U, Footer = 720U, Gutter = 0U }));
        main.Document.Save();
    }


    public static async Task HtmlAsync(Guide guide,string target,Func<Step,Task<ExportImage>> render)
    {
        var snapshot=GuideJson.Parse(GuideJson.Serialize(guide));GuideJson.EnsurePrivacyApplied(snapshot);
        await using var file=new FileStream(target,FileMode.CreateNew,FileAccess.Write);
        await using var output=new StreamWriter(file,new UTF8Encoding(false));
        await WriteHtmlAsync(snapshot,output,render);
    }
    public static async Task<string> HtmlCodeAsync(Guide guide,Func<Step,Task<ExportImage>> render)
    {
        var snapshot=GuideJson.Parse(GuideJson.Serialize(guide));GuideJson.EnsurePrivacyApplied(snapshot);
        using var output=new StringWriter();await WriteHtmlAsync(snapshot,output,render);return output.ToString();
    }
    private static async Task WriteHtmlAsync(Guide snapshot,TextWriter output,Func<Step,Task<ExportImage>> render)
    {
        static string H(string value)=>WebUtility.HtmlEncode(value);
        await output.WriteAsync("<!doctype html><html lang=\"en-CA\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        await output.WriteAsync("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><meta name=\"generator\" content=\"CAPTURE HTML export 1.0\">");
        await output.WriteAsync($"<title>{H(snapshot.Title)}</title><style>{HtmlStyles}</style></head><body id=\"top\"><main>");
        await output.WriteAsync($"<header class=\"hero\"><div class=\"eyebrow\">STEP-BY-STEP GUIDE <span>· {snapshot.Steps.Count} {(snapshot.Steps.Count == 1 ? "STEP" : "STEPS")}</span></div><h1>{H(snapshot.Title)}</h1>");
        if (!string.IsNullOrWhiteSpace(snapshot.Summary)) await output.WriteAsync($"<p class=\"summary\">{H(snapshot.Summary)}</p>");
        await output.WriteAsync("</header>");
        if (snapshot.Steps.Count > 0)
        {
            await output.WriteAsync("<nav class=\"contents\" aria-label=\"Guide contents\"><h2>In this guide</h2><ol>");
            for (int i = 0; i < snapshot.Steps.Count; i++)
                await output.WriteAsync($"<li><a href=\"#step-{i+1}\">{StepText.Html(snapshot.Steps[i].Title,snapshot.Steps[i].TitleFields)}</a></li>");
            await output.WriteAsync("</ol></nav>");
        }
        for (int i = 0; i < snapshot.Steps.Count; i++)
        {
            var step = snapshot.Steps[i];
            await output.WriteAsync($"<article class=\"step\" id=\"step-{i+1}\"><div class=\"step-heading\"><span class=\"number\" aria-hidden=\"true\">{i+1:D2}</span><h2><span class=\"sr-only\">Step {i+1}: </span>{StepText.Html(step.Title,step.TitleFields)}</h2></div>");
            if(!string.IsNullOrWhiteSpace(step.Instruction))await output.WriteAsync($"<p class=\"instruction\">{StepText.Html(step.Instruction,step.TitleFields)}</p>");
            if (!string.IsNullOrWhiteSpace(step.Notes)) await output.WriteAsync($"<aside class=\"notes\"><strong>Note</strong><p>{H(step.Notes)}</p></aside>");
            if (step.AssetId is not null)
            {
                var image = await render(step); ValidateImage(image);
                await output.WriteAsync($"<figure><img src=\"data:image/png;base64,{Convert.ToBase64String(image.Png)}\" width=\"{image.Width}\" height=\"{image.Height}\" alt=\"{H($"Screenshot for step {i+1}: {step.Title}")}\" loading=\"lazy\"><figcaption>Step {i+1} · {H(step.Title)}</figcaption></figure>");
            }
            await output.WriteAsync("</article>");
        }
        await output.WriteAsync("<footer><span>Created with CAPTURE</span><a href=\"#top\">Back to top ↑</a></footer></main></body></html>");
    }

    private const string HtmlStyles = """
        :root{color-scheme:light;--ink:#202833;--muted:#596574;--accent:#ad2540;--line:#dce1e6}
        *{box-sizing:border-box}html{scroll-behavior:smooth;scroll-padding-top:24px}
        body{margin:0;background:#f2f4f6;color:var(--ink);font:16px/1.7 -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}
        main{max-width:1000px;margin:auto;padding:56px 36px 24px}.hero{padding:16px 0 36px;border-bottom:3px solid var(--ink)}
        .eyebrow{font-size:11px;letter-spacing:2px;font-weight:750;color:var(--accent)}.eyebrow span{color:var(--muted)}
        h1{font-size:clamp(32px,5vw,52px);line-height:1.13;letter-spacing:-1.5px;margin:18px 0 20px;overflow-wrap:anywhere}
        .summary{font-size:19px;line-height:1.65;max-width:760px;color:var(--muted);white-space:pre-wrap}
        .contents{margin:28px 0 40px;padding:24px 28px;background:#fff;border:1px solid var(--line);border-radius:12px}
        .contents h2{font-size:15px;margin:0 0 10px}.contents ol{margin:0;padding-left:24px;columns:2;column-gap:40px}
        .contents li{padding:3px 0;break-inside:avoid}.contents a{overflow-wrap:anywhere;color:var(--muted);text-decoration:none}.contents a:hover{color:var(--accent);text-decoration:underline}
        .step{margin:24px 0;padding:32px;background:#fff;border:1px solid var(--line);border-radius:14px;box-shadow:0 3px 12px #20283305;overflow-wrap:anywhere}
        .step-heading{display:flex;gap:18px;align-items:flex-start;margin-bottom:18px}.number{color:var(--accent);background:#f9e9ed;border:1px solid #edcdd5;border-radius:9px;padding:5px 10px;font-weight:750;font-size:17px;font-variant-numeric:tabular-nums}
        .step h2{font-weight:500}.step h2 strong{font-weight:750}h2{font-size:24px;line-height:1.35;margin:3px 0 0;letter-spacing:-.4px}.instruction{margin:0 0 22px;white-space:pre-wrap}
        .notes{border-left:3px solid #b9c8d9;padding:12px 18px;margin:18px 0 24px;background:#f5f7fa;font-size:14px}.notes strong{color:var(--muted);font-size:11px;text-transform:uppercase;letter-spacing:1px}.notes p{margin:4px 0 0;white-space:pre-wrap}
        figure{margin:24px 0 0}img{display:block;max-width:100%;width:auto;height:auto;border:1px solid var(--line);border-radius:6px}figcaption{font-size:12px;color:var(--muted);margin-top:10px}
        a{color:var(--accent)}a:focus-visible{outline:3px solid #2366c2;outline-offset:4px}footer{display:flex;justify-content:space-between;gap:20px;padding:28px 0;font-size:12px;color:var(--muted)}
        .sr-only{position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip-path:inset(50%);white-space:nowrap;border:0}
        @media(max-width:640px){main{padding:28px 16px}.hero{padding-top:8px}.step{padding:22px 18px}.contents{padding:18px}.contents ol{columns:1}h2{font-size:21px}.summary{font-size:17px}}
        @media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}}
        @page{size:letter;margin:.75in}@media print{body{background:#fff;font-size:11pt}main{max-width:none;padding:0}.hero{padding:0 0 20px}h1{font-size:30pt}.summary{font-size:12pt}.contents{display:none}.step{border:0;border-radius:0;box-shadow:none;padding:22px 0;margin:0;border-bottom:1px solid #ddd}.step-heading{break-after:avoid}h2{font-size:16pt}.instruction,.notes{orphans:3;widows:3}figure{break-inside:avoid}img{max-height:8in;object-fit:contain}footer a{display:none}.number{background:#fff;color:#202833;border-color:#aaa}}
        """;

    private static void ValidateImage(ExportImage image) => GuideJson.ValidatePng(image.Png,
        new Asset("export", "assets/export.png", "image/png", image.Width, image.Height, new string('0', 64)));

    private static string XmlText(string text)
    {
        var result = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
            if (rune.Value is 9 or 10 or 13 or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF)
                result.Append(rune.ToString());
        return result.ToString();
    }

    private static W.Paragraph Paragraph(string text, string? style = null,IEnumerable<string>? fields=null)
    {
        var paragraph = new W.Paragraph();
        if (style is not null) paragraph.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }));
        var lines = XmlText(text).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) paragraph.Append(new W.Run(new W.Break()));
            foreach(var part in StepText.Runs(lines[i],fields))
            {
                var run=new W.Run();if(part.Bold)run.Append(new W.RunProperties(new W.Bold()));
                run.Append(new W.Text(part.Text){Space=SpaceProcessingModeValues.Preserve});paragraph.Append(run);
            }
        }
        return paragraph;
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new W.Styles(
            new W.DocDefaults(
                new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(
                    new W.RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
                    new W.FontSize { Val = "22" }, new W.Languages { Val = "en-CA" })),
                new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(new W.SpacingBetweenLines { After = "160", Line = "260", LineRule = W.LineSpacingRuleValues.Auto }))),
            new W.Style(new W.StyleName { Val = "Normal" }) { Type = W.StyleValues.Paragraph, StyleId = "Normal", Default = true },
            new W.Style(new W.StyleName { Val = "Title" }, new W.BasedOn { Val = "Normal" },
                new W.StyleParagraphProperties(new W.KeepNext()), new W.StyleRunProperties(new W.Bold(), new W.FontSize { Val = "40" })) { Type = W.StyleValues.Paragraph, StyleId = "Title" },
            new W.Style(new W.StyleName { Val = "heading 1" }, new W.BasedOn { Val = "Normal" },
                new W.StyleParagraphProperties(new W.KeepNext(), new W.SpacingBetweenLines { Before = "240", After = "120" }, new W.OutlineLevel { Val = 0 }),
                new W.StyleRunProperties(new W.FontSize { Val = "28" })) { Type = W.StyleValues.Paragraph, StyleId = "Heading1" });
        part.Styles.Save();
    }

    private static W.Paragraph ImageParagraph(string relationshipId, uint id, long width, long height, string description)
    {
        var picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = id, Name = $"Screenshot {id}", Description = XmlText(description) },
                new PIC.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
            new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        var graphicData = new A.GraphicData(picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        var inline = new DW.Inline(
            new DW.Extent { Cx = width, Cy = height },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = id, Name = $"Screenshot {id}", Description = XmlText(description) },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(graphicData)) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U };
        return new W.Paragraph(new W.ParagraphProperties(new W.KeepLines()), new W.Run(new W.Drawing(inline)));
    }
}
