using System.Net;
using System.Text;
using System.Text.RegularExpressions;
namespace Capture.Core;
public sealed record TextRun(string Text,bool Bold);
public static class StepText
{
    public static Step FromControl(string type,string? label)
    {
        label=CleanLabel(label);
        var step=new Step { Instruction="" };
        if(label is null){step.Title=type=="Edit"?"Enter the required information":"Select the highlighted control";return step;}
        step.Title=type switch
        {
            "Edit"=>$"Enter information in {label}","ComboBox"=>$"Choose an option in {label}","TabItem"=>$"Open the {label} tab",
            "MenuItem"=>$"Choose {label}","Hyperlink"=>$"Open {label}","CheckBox"=>$"Change the {label} option", "RadioButton"=>$"Select the {label} option",_=>$"Select {label}"
        };
        step.TitleFields=[label];return step;
    }
    public static string? CleanLabel(string? label)
    {
        if(string.IsNullOrWhiteSpace(label))return null;
        label=string.Join(' ',label.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
        if(label.StartsWith("the highlighted",StringComparison.OrdinalIgnoreCase)||label.StartsWith("highlighted ",StringComparison.OrdinalIgnoreCase))return null;
        if(label.Length>120 || label.Any(char.IsControl) || new[]{"none","null","custom","pane","document","button","edit","text","unknown"}.Contains(label,StringComparer.OrdinalIgnoreCase))return null;
        return label;
    }
    public static string? FieldLabelFromId(string? id)
    {
        if(string.IsNullOrWhiteSpace(id)||id.Length>80||!Regex.IsMatch(id,@"^[A-Za-z_-]+$"))return null;
        id=Regex.Replace(id,@"^(txt|input|field|edt)[_-]?(?=[A-Z_])","",RegexOptions.CultureInvariant);
        id=Regex.Replace(id,@"(TextBox|Input|Field|Editor|Edit|Box)$","",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        var words=Regex.Replace(id,"([a-z])([A-Z])","$1 $2").Replace('_',' ').Replace('-',' ').Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(w=>w.ToLowerInvariant()).ToArray();
        string[] fields=["title","summary","description","search","name","email","username","subject","message","address","phone","category","tag","tags","url","content","notes","comment"];
        string[] contexts=["article","guide","step","first","last","full","user","display","project","page","item","short","long","customer","contact","account","record","request","service"];
        if(words.Length==0||words.Length>4||!fields.Contains(words[^1])||words.Any(w=>!fields.Contains(w)&&!contexts.Contains(w)))return null;
        var label=string.Join(' ',words);return char.ToUpperInvariant(label[0])+label[1..];
    }
    public static List<string> LegacyFields(string title)
    {
        foreach(var (prefix,suffix) in new[]{("Open the "," tab"),("Change the "," option"),("Select ",""),("Choose ",""),("Open ","")})
            if(title.StartsWith(prefix,StringComparison.Ordinal)&&title.EndsWith(suffix,StringComparison.Ordinal)&&title.Length>prefix.Length+suffix.Length)
            {var label=CleanLabel(title.Substring(prefix.Length,title.Length-prefix.Length-suffix.Length));return label is null?[]:[label];}
        return [];
    }
    public static bool IsDuplicate(string title,string instruction)
    {
        static string Normalize(string text)=>text.Trim().TrimEnd('.','!','?').Trim();
        return string.Equals(Normalize(title),Normalize(instruction),StringComparison.OrdinalIgnoreCase);
    }
    public static IEnumerable<TextRun> Runs(string text,IEnumerable<string>? fields)
    {
        var ranges=new List<(int Start,int End)>();
        foreach(var field in (fields??[]).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct().OrderByDescending(s=>s.Length))
        {
            int at=text.LastIndexOf(field,StringComparison.Ordinal);
            while(at>=0)
            {
                int end=at+field.Length;
                bool boundary=(!char.IsLetterOrDigit(field[0])||at==0||!char.IsLetterOrDigit(text[at-1]))&&(!char.IsLetterOrDigit(field[^1])||end==text.Length||!char.IsLetterOrDigit(text[end]));
                if(boundary&&!ranges.Any(r=>at<r.End&&end>r.Start)){ranges.Add((at,end));break;}
                if(at==0)break;at=text.LastIndexOf(field,at-1,StringComparison.Ordinal);
            }
        }
        int position=0;
        foreach(var range in ranges.OrderBy(r=>r.Start))
        {if(range.Start>position)yield return new(text[position..range.Start],false);yield return new(text[range.Start..range.End],true);position=range.End;}
        if(position<text.Length)yield return new(text[position..],false);
    }

    public static string Html(string text,IEnumerable<string>? fields)=>string.Concat(Runs(text,fields).Select(r=>r.Bold?"<strong>"+WebUtility.HtmlEncode(r.Text)+"</strong>":WebUtility.HtmlEncode(r.Text)));
    public static string Markdown(string text,IEnumerable<string>? fields)
    {
        static string Escape(string s)=>string.Concat(s.Select(c=>"\\`*_{}[]<>()#+-.!|".Contains(c)?"\\"+c:c.ToString()));
        return string.Concat(Runs(text,fields).Select(r=>r.Bold?"**"+Escape(r.Text)+"**":Escape(r.Text)));
    }
}
public sealed record ControlBounds(double X,double Y,double Width,double Height);
public static class CaptureTargets
{
    public static Annotation Highlight(double px,double py,ControlBounds? control,ControlBounds frame)
    {
        bool valid=control is not null && double.IsFinite(control.X+control.Y+control.Width+control.Height) && control.Width>=3 && control.Height>=3 &&
            px>=control.X-2 && px<=control.X+control.Width+2 && py>=control.Y-2 && py<=control.Y+control.Height+2 &&
            control.Width<=frame.Width*.95 && control.Height<=frame.Height*.8 && control.Width*control.Height<=frame.Width*frame.Height*.35;
        if(!valid)return Coordinates.Marker(px,py,frame.X,frame.Y,frame.Width,frame.Height) with {Kind="ellipse"};
        var c=control!;const double pad=3;
        double x=Math.Max(frame.X,c.X-pad),y=Math.Max(frame.Y,c.Y-pad),right=Math.Min(frame.X+frame.Width,c.X+c.Width+pad),bottom=Math.Min(frame.Y+frame.Height,c.Y+c.Height+pad);
        if(right<=x||bottom<=y)return Coordinates.Marker(px,py,frame.X,frame.Y,frame.Width,frame.Height) with {Kind="ellipse"};
        return new(Guid.NewGuid().ToString(),"highlight",(x-frame.X)/frame.Width,(y-frame.Y)/frame.Height,(right-x)/frame.Width,(bottom-y)/frame.Height);
    }
}
