using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Capture.Core;

namespace Capture.App;

public static class ExportImageRenderer
{
    public static async Task<ExportImage> ReadSourceAsync(Guide guide, string assetId, string projectDirectory)
    {
        var asset = guide.Assets.Single(a => a.Id == assetId);
        var path = GuideJson.SafePath(projectDirectory, asset.RelativePath);
        if (!File.Exists(path)) throw new InvalidDataException("This screenshot file is missing. Restore it from a project backup, or import the complete portable package.");
        if (new FileInfo(path).Length > 64L * 1024 * 1024) throw new InvalidDataException("The screenshot exceeds the image size limit.");
        var bytes = await File.ReadAllBytesAsync(path);
        GuideJson.ValidatePng(bytes, asset);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The screenshot checksum does not match this project. Restore the original image from a backup.");
        return new(bytes, asset.Width, asset.Height);
    }

    public static async Task<ExportImage> RenderAsync(Guide guide, Step step, string projectDirectory)
    {
        GuideJson.Validate(guide);
        var source = await ReadSourceAsync(guide, step.AssetId!, projectDirectory);
        using var bitmap = Decode(source);
        ApplyPixels(bitmap, step.Annotations);
        var layout=Capture.Core.ImageLayout.Create(bitmap.Width,bitmap.Height,step.Crop,step.Cuts);
        using var result=new Bitmap(layout.Width,layout.Height,PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(result))
            foreach(var tile in layout.Tiles)
            {using var piece=bitmap.Clone(new Rectangle(tile.SourceX,tile.SourceY,tile.Width,tile.Height),PixelFormat.Format32bppArgb);graphics.DrawImageUnscaled(piece,tile.X,tile.Y);}
        using(var graphics=Graphics.FromImage(result))
        {
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            foreach(var a in step.Annotations.Where(a=>a.Kind is not ("blur" or "redact")))
            {
                var box=layout.Project(new(a.X*bitmap.Width,a.Y*bitmap.Height,a.Width*bitmap.Width,a.Height*bitmap.Height));
                if(box.Width<=0||box.Height<=0)continue;
                Draw(graphics,a with {X=box.X/layout.Width,Y=box.Y/layout.Height,Width=box.Width/layout.Width,Height=box.Height/layout.Height},layout.Width,layout.Height);
            }
        }
        return Encode(result);
    }

    public static async Task<ExportImage> PixelEditsAsync(Guide guide, string assetId, string projectDirectory)
    {
        GuideJson.Validate(guide);
        var source = await ReadSourceAsync(guide, assetId, projectDirectory);
        using var bitmap = Decode(source);
        ApplyPixels(bitmap, guide.Steps.Where(s => s.AssetId == assetId).SelectMany(s => s.Annotations));
        return Encode(bitmap);
    }

    private static Bitmap Decode(ExportImage image)
    {
        using var input = new MemoryStream(image.Png, false);
        using var source = new Bitmap(input);
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.DrawImageUnscaled(source, 0, 0);
        return bitmap;
    }
    private static ExportImage Encode(Bitmap bitmap)
    {
        using var output = new MemoryStream(); bitmap.Save(output, ImageFormat.Png);
        return new(output.ToArray(), bitmap.Width, bitmap.Height);
    }
    private static Rectangle Bounds(double x, double y, double width, double height, int imageWidth, int imageHeight)
    {
        int left = Math.Clamp((int)Math.Round(x * imageWidth), 0, imageWidth-1), top = Math.Clamp((int)Math.Round(y * imageHeight), 0, imageHeight-1);
        int right = Math.Clamp((int)Math.Round((x+width)*imageWidth), left+1, imageWidth), bottom = Math.Clamp((int)Math.Round((y+height)*imageHeight), top+1, imageHeight);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
    private static Color ColorOf(string value) => value == "transparent" ? Color.Transparent : ColorTranslator.FromHtml(value);

    private static void Draw(Graphics g, Annotation a, int width, int height)
    {
        float x=(float)(a.X*width), y=(float)(a.Y*height), w=(float)(a.Width*width), h=(float)(a.Height*height);
        using var path = new GraphicsPath();
        if (a.Kind == "ellipse") path.AddEllipse(x,y,w,h);
        else if (a.Kind is "line" or "arrow")
        {
            var start = new PointF(a.FlipX?x+w:x,a.FlipY?y+h:y); var end = new PointF(a.FlipX?x:x+w,a.FlipY?y:y+h);
            using var pen = new Pen(ColorOf(a.Stroke),(float)a.StrokeWidth) { StartCap=LineCap.Round, EndCap=LineCap.Round };
            if(a.Shadow) { using var shadow = new Pen(Color.FromArgb(75,0,0,0),(float)a.StrokeWidth+2); g.DrawLine(shadow,start.X+3,start.Y+3,end.X+3,end.Y+3); }
            g.DrawLine(pen,start,end);
            if(a.Kind=="arrow")
            {
                double angle=Math.Atan2(end.Y-start.Y,end.X-start.X), size=Math.Max(12,a.StrokeWidth*4);
                using var brush=new SolidBrush(ColorOf(a.Stroke));
                g.FillPolygon(brush,[end,new PointF(end.X-(float)(Math.Cos(angle-.45)*size),end.Y-(float)(Math.Sin(angle-.45)*size)),new PointF(end.X-(float)(Math.Cos(angle+.45)*size),end.Y-(float)(Math.Sin(angle+.45)*size))]);
            }
            return;
        }
        else if(a.Kind=="callout") path.AddPolygon(new PointF[]{new(x,y),new(x+w,y),new(x+w,y+h*.72f),new(x+w*.4f,y+h*.72f),new(x+w*.2f,y+h),new(x+w*.24f,y+h*.72f),new(x,y+h*.72f)});
        else path.AddRectangle(new RectangleF(x,y,w,h));
        if(a.Shadow)
        {
            using var shadowPath=(GraphicsPath)path.Clone(); using var transform=new Matrix(); transform.Translate(3,4); shadowPath.Transform(transform);
            if(a.Fill!="transparent") { using var shadowBrush=new SolidBrush(Color.FromArgb(65,0,0,0));g.FillPath(shadowBrush,shadowPath); }
            else if(a.Kind!="text") { using var shadowPen=new Pen(Color.FromArgb(75,0,0,0),(float)a.StrokeWidth);g.DrawPath(shadowPen,shadowPath); }
        }
        if(a.Fill!="transparent") { using var fill=new SolidBrush(ColorOf(a.Fill));g.FillPath(fill,path); }
        if(a.Kind!="text") { using var pen=new Pen(ColorOf(a.Stroke),(float)a.StrokeWidth);g.DrawPath(pen,path); }
        if(a.Kind is "text" or "callout")
        {
            using var font=new Font("Arial",(float)a.FontSize,FontStyle.Regular,GraphicsUnit.Pixel);
            using var brush=new SolidBrush(ColorOf(a.TextColor));
            var bounds=new RectangleF(x+6,y+3,Math.Max(1,w-12),Math.Max(1,h*(a.Kind=="callout"?.72f:1)-6));
            using var format=new StringFormat { Trimming=StringTrimming.EllipsisCharacter, FormatFlags=StringFormatFlags.LineLimit };
            if(a.Shadow) { using var shadowBrush=new SolidBrush(Color.FromArgb(90,0,0,0));var shadowBounds=bounds;shadowBounds.Offset(3,4);g.DrawString(a.Text,font,shadowBrush,shadowBounds,format); }
            g.DrawString(a.Text,font,brush,bounds,format);
        }
    }

    private static void ApplyPixels(Bitmap bitmap, IEnumerable<Annotation> annotations)
    {
        // Blur first; opaque redaction last so another overlapping blur can never soften a redacted boundary.
        var edits=annotations.ToArray();
        foreach(var a in edits.Where(a=>a.Kind=="blur")) Blur(bitmap,Bounds(a.X,a.Y,a.Width,a.Height,bitmap.Width,bitmap.Height),a.BlurRadius);
        using var graphics=Graphics.FromImage(bitmap);
        foreach(var a in edits.Where(a=>a.Kind=="redact"))
        { using var brush=new SolidBrush(a.Fill=="transparent"?Color.Black:ColorOf(a.Fill));graphics.FillRectangle(brush,Bounds(a.X,a.Y,a.Width,a.Height,bitmap.Width,bitmap.Height)); }
    }

    private static void Blur(Bitmap bitmap, Rectangle area, int radius)
    {
        var data=bitmap.LockBits(area,ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try
        {
            int w=area.Width,h=area.Height,row=w*4;
            var input=new byte[row*h];var horizontal=new byte[input.Length];var output=new byte[input.Length];
            for(int y=0;y<h;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),input,y*row,row);
            int diameter=radius*2+1;
            for(int y=0;y<h;y++) for(int channel=0;channel<4;channel++)
            {
                int sum=0;for(int dx=-radius;dx<=radius;dx++)sum+=input[y*row+Math.Clamp(dx,0,w-1)*4+channel];
                for(int x=0;x<w;x++)
                {horizontal[y*row+x*4+channel]=(byte)(sum/diameter);sum+=input[y*row+Math.Clamp(x+radius+1,0,w-1)*4+channel]-input[y*row+Math.Clamp(x-radius,0,w-1)*4+channel];}
            }
            for(int x=0;x<w;x++) for(int channel=0;channel<4;channel++)
            {
                int sum=0;for(int dy=-radius;dy<=radius;dy++)sum+=horizontal[Math.Clamp(dy,0,h-1)*row+x*4+channel];
                for(int y=0;y<h;y++)
                {output[y*row+x*4+channel]=(byte)(sum/diameter);sum+=horizontal[Math.Clamp(y+radius+1,0,h-1)*row+x*4+channel]-horizontal[Math.Clamp(y-radius,0,h-1)*row+x*4+channel];}
            }
            for(int y=0;y<h;y++)Marshal.Copy(output,y*row,IntPtr.Add(data.Scan0,y*data.Stride),row);
        }
        finally { bitmap.UnlockBits(data); }
    }
}
