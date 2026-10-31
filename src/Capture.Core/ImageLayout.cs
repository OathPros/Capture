namespace Capture.Core;
public sealed record ImageCut(string Axis,double Start,double End);
public sealed record ImageSegment(int Source,int Length,int Output);
public sealed record ImageTile(int SourceX,int SourceY,int Width,int Height,int X,int Y);
public sealed class ImageLayout
{
    public required IReadOnlyList<ImageSegment> Columns {get;init;}
    public required IReadOnlyList<ImageSegment> Rows {get;init;}
    public int Left{get;init;} public int Top{get;init;} public int Right{get;init;} public int Bottom{get;init;}
    public ControlBounds Project(ControlBounds rect)
    {
        double x=Map(rect.X,Columns,Left,Right),y=Map(rect.Y,Rows,Top,Bottom);
        return new(x,y,Map(rect.X+rect.Width,Columns,Left,Right)-x,Map(rect.Y+rect.Height,Rows,Top,Bottom)-y);
    }
    private static double Map(double value,IReadOnlyList<ImageSegment> segments,int from,int to)
    {
        if(value<from)return value-from;double total=segments.Sum(s=>s.Length);if(value>to)return total+value-to;
        double output=0;foreach(var s in segments){if(value<=s.Source)return output;if(value<s.Source+s.Length)return output+value-s.Source;output+=s.Length;}return output;
    }
    public int Width=>Columns.Sum(s=>s.Length);
    public int Height=>Rows.Sum(s=>s.Length);
    public IEnumerable<ImageTile> Tiles=>Rows.SelectMany(y=>Columns.Select(x=>new ImageTile(x.Source,y.Source,x.Length,y.Length,x.Output,y.Output)));
    public static ImageLayout Create(int width,int height,CropRegion? crop,IEnumerable<ImageCut>? cuts)
    {
        crop??=new(0,0,1,1);var all=(cuts??[]).ToArray();
        int left=Math.Clamp((int)Math.Round(crop.X*width),0,width-1),top=Math.Clamp((int)Math.Round(crop.Y*height),0,height-1);
        int right=Math.Clamp((int)Math.Round((crop.X+crop.Width)*width),left+1,width),bottom=Math.Clamp((int)Math.Round((crop.Y+crop.Height)*height),top+1,height);
        return new(){Left=left,Top=top,Right=right,Bottom=bottom,Columns=Segments(width,crop.X,crop.X+crop.Width,all.Where(c=>c.Axis=="vertical")),Rows=Segments(height,crop.Y,crop.Y+crop.Height,all.Where(c=>c.Axis=="horizontal"))};
    }
    private static List<ImageSegment> Segments(int size,double from,double to,IEnumerable<ImageCut> cuts)
    {
        int start=Math.Clamp((int)Math.Round(from*size),0,size-1),end=Math.Clamp((int)Math.Round(to*size),start+1,size),position=start,output=0;
        var result=new List<ImageSegment>();
        foreach(var cut in cuts.Select(c=>(Start:Math.Clamp((int)Math.Round(c.Start*size),start,end),End:Math.Clamp((int)Math.Round(c.End*size),start,end))).Where(c=>c.End>c.Start).OrderBy(c=>c.Start))
        {
            if(cut.Start>position){result.Add(new(position,cut.Start-position,output));output+=cut.Start-position;}
            position=Math.Max(position,cut.End);
        }
        if(position<end)result.Add(new(position,end-position,output));return result;
    }
}
