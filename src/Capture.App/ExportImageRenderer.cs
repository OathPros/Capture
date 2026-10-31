using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Capture.Core;

namespace Capture.App;

/// <summary>Flatten annotations into an export copy without changing the project image.</summary>
public static class ExportImageRenderer
{
    public static async Task<ExportImage> RenderAsync(Guide guide, Step step, string projectDirectory)
    {
        var asset = guide.Assets.Single(a => a.Id == step.AssetId);
        var path = GuideJson.SafePath(projectDirectory, asset.RelativePath);
        if (!File.Exists(path)) throw new InvalidDataException("A screenshot is missing. Restore the project image before exporting.");
        if (new FileInfo(path).Length > 64L * 1024 * 1024) throw new InvalidDataException("A screenshot exceeds the export size limit.");
        var bytes = await File.ReadAllBytesAsync(path);
        GuideJson.ValidatePng(bytes, asset);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A screenshot checksum has changed. Restore the original project image before exporting.");
        using var input = new MemoryStream(bytes, false);
        using var source = new Bitmap(input);
        using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImageUnscaled(source, 0, 0);
            using var pen = new Pen(Color.FromArgb(227, 24, 55), Math.Max(3, bitmap.Width / 400f));
            foreach (var annotation in step.Annotations)
                graphics.DrawRectangle(pen, (float)(annotation.X * bitmap.Width), (float)(annotation.Y * bitmap.Height),
                    (float)(annotation.Width * bitmap.Width), (float)(annotation.Height * bitmap.Height));
        }
        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return new ExportImage(output.ToArray(), bitmap.Width, bitmap.Height);
    }
}
