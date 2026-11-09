using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Capture.Core;
namespace Capture.App;

public static class PixelEditStorage
{
    // Commit a new sanitized asset and durable cleanup intent before removing the original.
    public static async Task<Guide> ApplyAsync(Guide guide, string assetId, ProjectStore store)
    {
        return await ApplyManyAsync(guide, [assetId], store);
    }
    public static Task<Guide> ApplyAllAsync(Guide guide, ProjectStore store) => ApplyManyAsync(guide,
        guide.Steps.Where(s => s.Annotations.Any(a => a.Kind is "blur" or "redact")).Select(s => s.AssetId ?? throw new InvalidDataException("Pixel edits require an image.")).Distinct().ToArray(), store);

    private static async Task<Guide> ApplyManyAsync(Guide guide, string[] assetIds, ProjectStore store)
    {
        GuideJson.Validate(guide);
        var next = GuideJson.Parse(GuideJson.Serialize(guide));
        var created = new List<string>();
        try
        {
            foreach (var assetId in assetIds)
            {
                if (!guide.Steps.Where(s => s.AssetId == assetId).SelectMany(s => s.Annotations).Any(a => a.Kind is "blur" or "redact"))
                    throw new InvalidOperationException("There are no pixel edits to apply to this screenshot.");
                var old = next.Assets.Single(a => a.Id == assetId);
                var image = await ExportImageRenderer.PixelEditsAsync(guide, assetId, store.DirectoryFor(guide.Id));
                var id = Guid.NewGuid().ToString(); var relative = $"assets/{id}.png";
                var asset = new Asset(id, relative, "image/png", image.Width, image.Height, Convert.ToHexString(SHA256.HashData(image.Png)));
                next.Assets.RemoveAll(a => a.Id == assetId); next.Assets.Add(asset);
                foreach (var step in next.Steps.Where(s => s.AssetId == assetId))
                { step.AssetId = id; step.NeedsReview = true; step.Annotations.RemoveAll(a => a.Kind is "blur" or "redact"); }
                next.PendingAssetDeletes.Add(old.RelativePath);
                var path = GuideJson.SafePath(store.DirectoryFor(next.Id), relative);
                created.Add(path); await File.WriteAllBytesAsync(path, image.Png);
            }
            next.UpdatedAt = DateTimeOffset.UtcNow;
            await store.SaveAsync(next);
            return next;
        }
        catch
        {
            foreach (var path in created) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            throw;
        }
    }
    public static async Task<Guide> CleanupAsync(Guide guide,ProjectStore store)
    {
        GuideJson.Validate(guide);
        foreach(var relative in guide.PendingAssetDeletes) File.Delete(GuideJson.SafePath(store.DirectoryFor(guide.Id),relative));
        var next=GuideJson.Parse(GuideJson.Serialize(guide));next.PendingAssetDeletes.Clear();await store.SaveAsync(next);return next;
    }
}
