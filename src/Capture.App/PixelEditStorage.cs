using System;
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
        GuideJson.Validate(guide);
        var next=GuideJson.Parse(GuideJson.Serialize(guide));
        if(!next.Steps.Where(s=>s.AssetId==assetId).SelectMany(s=>s.Annotations).Any(a=>a.Kind is "blur" or "redact")) throw new InvalidOperationException("There are no pixel edits to apply to this screenshot.");
        var old=next.Assets.Single(a=>a.Id==assetId);
        var image=await ExportImageRenderer.PixelEditsAsync(next,assetId,store.DirectoryFor(next.Id));
        var id=Guid.NewGuid().ToString();var relative=$"assets/{id}.png";
        var asset=new Asset(id,relative,"image/png",image.Width,image.Height,Convert.ToHexString(SHA256.HashData(image.Png)));
        next.Assets.RemoveAll(a=>a.Id==assetId);next.Assets.Add(asset);
        foreach(var step in next.Steps.Where(s=>s.AssetId==assetId)) { step.AssetId=id;step.NeedsReview=true;step.Annotations.RemoveAll(a=>a.Kind is "blur" or "redact"); }
        next.PendingAssetDeletes.Add(old.RelativePath);next.UpdatedAt=DateTimeOffset.UtcNow;
        var path=GuideJson.SafePath(store.DirectoryFor(next.Id),relative);
        try { await File.WriteAllBytesAsync(path,image.Png);await store.SaveAsync(next); }
        catch { File.Delete(path); throw; }
        return next;
    }
    public static async Task<Guide> CleanupAsync(Guide guide,ProjectStore store)
    {
        GuideJson.Validate(guide);
        foreach(var relative in guide.PendingAssetDeletes) File.Delete(GuideJson.SafePath(store.DirectoryFor(guide.Id),relative));
        var next=GuideJson.Parse(GuideJson.Serialize(guide));next.PendingAssetDeletes.Clear();await store.SaveAsync(next);return next;
    }
}
