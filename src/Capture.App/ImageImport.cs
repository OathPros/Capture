using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using Capture.Core;
namespace Capture.App;

public static class ImageImport
{
    public static async Task<(Guide Guide, Asset Asset)> ImportAsync(Guide guide, string source, ProjectStore store)
    {
        if (new FileInfo(source).Length > 64L * 1024 * 1024)
            throw new InvalidDataException("Choose an image smaller than 64 MB.");
        byte[] png;
        int width, height;
        try
        {
            using var input = File.OpenRead(source);
            using var image = Image.FromStream(input, false, true);
            if (image.RawFormat.Guid != ImageFormat.Png.Guid && image.RawFormat.Guid != ImageFormat.Jpeg.Guid && image.RawFormat.Guid != ImageFormat.Bmp.Guid)
                throw new InvalidDataException("Choose a PNG, JPEG or BMP image.");
            width = image.Width; height = image.Height;
            if (width > 32768 || height > 32768 || (long)width * height > 3840L * 2160)
                throw new InvalidDataException("Choose an image with no more than 8,294,400 pixels (4K), and neither side above 32,768 pixels.");
            // Re-encode pixels to a managed PNG; do not retain source metadata or file paths.
            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImage(image, 0, 0, width, height);
            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png); png = output.ToArray();
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException or OutOfMemoryException)
        { throw new InvalidDataException("This image could not be decoded. Choose a valid PNG, JPEG or BMP image.", ex); }
        var id = Guid.NewGuid().ToString();
        var asset = new Asset(id, $"assets/{id}.png", "image/png", width, height, Convert.ToHexString(SHA256.HashData(png)));
        GuideJson.ValidatePng(png, asset);
        var next = GuideJson.Parse(GuideJson.Serialize(guide));
        next.Assets.Add(asset); next.UpdatedAt = DateTimeOffset.UtcNow;
        GuideJson.Validate(next);
        var path = GuideJson.SafePath(store.DirectoryFor(next.Id), asset.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try { await File.WriteAllBytesAsync(path, png); await store.SaveAsync(next); }
        catch { File.Delete(path); throw; }
        return (next, asset);
    }
}
