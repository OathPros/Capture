using System.Text;
namespace Capture.Core;
public sealed record WordClipboardContent(string Rtf,string Text);
public static class WordClipboard
{
    public static async Task<WordClipboardContent> CreateAsync(Guide guide,Func<Step,Task<ExportImage>> render)
    {
        var g=GuideJson.Parse(GuideJson.Serialize(guide));GuideJson.EnsurePrivacyApplied(g);
        var rtf=new StringBuilder(@"{\rtf1\ansi\deff0\uc1{\fonttbl{\f0\fnil\fcharset0 Calibri;}}\viewkind4\paperw12240\paperh15840\margl1440\margr1440\margt1440\margb1440\f0\lang4105 ");
        var plain=new StringBuilder();
        void Paragraph(string text,int size=22,bool keep=false,IEnumerable<string>? fields=null,bool allBold=false)
        {
            rtf.Append(@"\pard\sa160\fs").Append(size).Append(keep?@"\keepn ":" ");
            if(allBold)rtf.Append(@"\b ");
            foreach(var run in StepText.Runs(text,fields))
            { if(run.Bold&&!allBold)rtf.Append(@"\b ");rtf.Append(Escape(run.Text));if(run.Bold&&!allBold)rtf.Append(@"\b0 "); }
            if(allBold)rtf.Append(@"\b0 ");rtf.Append(@"\par ");plain.AppendLine(text).AppendLine();
        }
        Paragraph(g.Title,40,true,allBold:true);if(!string.IsNullOrWhiteSpace(g.Summary))Paragraph(g.Summary);
        for(int i=0;i<g.Steps.Count;i++)
        {
            var step=g.Steps[i];Paragraph($"{i+1}. {step.Title}",28,true,step.TitleFields);
            if(!string.IsNullOrWhiteSpace(step.Instruction))Paragraph(step.Instruction,keep:step.AssetId is not null,fields:step.TitleFields);
            if(!string.IsNullOrWhiteSpace(step.Notes))Paragraph(step.Notes,keep:step.AssetId is not null);
            if(step.AssetId is null)continue;
            var image=await render(step);GuideJson.ValidatePng(image.Png,new("export","assets/export.png","image/png",image.Width,image.Height,new string('0',64)));
            double scale=Math.Min(1,Math.Min(624.0/image.Width,624.0/image.Height));
            int width=(int)Math.Round(image.Width*15*scale),height=(int)Math.Round(image.Height*15*scale);
            rtf.Append(@"\pard\sa180 {\pict\pngblip\picw").Append(image.Width).Append(@"\pich").Append(image.Height).Append(@"\picwgoal").Append(width).Append(@"\pichgoal").Append(height).AppendLine();
            var hex=Convert.ToHexString(image.Png);for(int at=0;at<hex.Length;at+=128)rtf.AppendLine(hex.Substring(at,Math.Min(128,hex.Length-at)));
            rtf.Append(@"}\par ");
            if(rtf.Length>128*1024*1024)throw new InvalidOperationException("This guide is too large for the clipboard. Use Word DOCX export instead.");
        }
        rtf.Append('}');return new(rtf.ToString(),plain.ToString());
    }
    private static string Escape(string text)
    {
        var output=new StringBuilder();
        foreach(char c in text.Replace("\r\n","\n").Replace('\r','\n'))
        {
            if(c is '\\' or '{' or '}')output.Append('\\').Append(c);
            else if(c=='\n')output.Append(@"\line ");else if(c=='\t')output.Append(@"\tab ");
            else if(c>=32&&c<=126)output.Append(c);else if(c>=32)output.Append(@"\u").Append(unchecked((short)c)).Append('?');
        }
        return output.ToString();
    }
}
