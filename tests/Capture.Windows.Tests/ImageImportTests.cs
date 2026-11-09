using System.Drawing;
using System.Drawing.Imaging;
using Capture.App;
using Capture.Core;
using Xunit;
namespace Capture.Windows.Tests;

public sealed class ImageImportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "capture-import-" + Guid.NewGuid());
    [Theory][InlineData("png")][InlineData("jpeg")][InlineData("bmp")]
    public async Task ImportedPixelsPersistAndExportWithoutTheSource(string format)
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source." + format);
        using (var image = new Bitmap(80, 40))
        {
            using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.Red);
            image.Save(source, format == "jpeg" ? ImageFormat.Jpeg : format == "bmp" ? ImageFormat.Bmp : ImageFormat.Png);
        }
        var store = new ProjectStore(Path.Combine(root, "projects"));
        var original = new Guide { Steps = [new() { Title = "Existing instructions" }] };
        await store.SaveAsync(original);
        var (guide, asset) = await ImageImport.ImportAsync(original, source, store);
        Assert.Empty(original.Assets);
        Assert.Equal("Existing instructions", guide.Steps[0].Title);
        Assert.Single((await store.LoadAsync(guide.Id)).Assets);
        File.Delete(source);
        var bytes = await File.ReadAllBytesAsync(GuideJson.SafePath(store.DirectoryFor(guide.Id), asset.RelativePath));
        GuideJson.ValidatePng(bytes, asset);
        using (var decoded = new Bitmap(new MemoryStream(bytes))) Assert.True(decoded.GetPixel(20, 20).R > 240);
        guide.Steps[0].AssetId = asset.Id;
        await store.SaveAsync(guide);
        var target = Path.Combine(root, "guide.capture");
        await store.ExportPortableAsync(guide, target);
        var imported = await store.ImportPortableAsync(target);
        Assert.Equal(asset.Sha256, Assert.Single(imported.Assets).Sha256);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task InvalidOrOversizedImageDoesNotChangeSavedGuide(bool oversized)
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "invalid.png");
        if (oversized) { using var image = new Bitmap(4096, 2160); image.Save(source, ImageFormat.Png); }
        else await File.WriteAllTextAsync(source, "not an image");
        var store = new ProjectStore(Path.Combine(root, "projects"));
        var guide = new Guide(); await store.SaveAsync(guide);
        await Assert.ThrowsAsync<InvalidDataException>(() => ImageImport.ImportAsync(guide, source, store));
        Assert.Empty((await store.LoadAsync(guide.Id)).Assets);
        Assert.False(Directory.Exists(Path.Combine(store.DirectoryFor(guide.Id), "assets")));
    }
    [Fact]
    public async Task FailedSaveRemovesOnlyTheNewManagedImage()
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.png");
        using (var image = new Bitmap(80, 40)) image.Save(source, ImageFormat.Png);
        var store = new ProjectStore(Path.Combine(root, "projects"));
        var guide = new Guide(); await store.SaveAsync(guide);
        // A directory at the temporary save path forces a deterministic save failure.
        Directory.CreateDirectory(Path.Combine(store.DirectoryFor(guide.Id), "guide.json.tmp"));
        var failure = await Record.ExceptionAsync(() => ImageImport.ImportAsync(guide, source, store));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.Empty((await store.LoadAsync(guide.Id)).Assets);
        Assert.Empty(Directory.GetFiles(Path.Combine(store.DirectoryFor(guide.Id), "assets")));
        Assert.True(File.Exists(source));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
