using Capture.Core;
using Xunit;
namespace Capture.Tests;

public sealed class ScreenshotStepTests
{
    private static readonly Asset Image = new("image", "assets/image.png", "image/png", 80, 40, new string('0',64));
    [Fact]
    public void ScreenshotReplacesOnlyTheSelectedImageAndKeepsWrittenContent()
    {
        var guide = new Guide { Assets = [Image], Steps = [new() { Title = "First" }, new()
        {
            Title = "Select Save", TitleFields = ["Save"], Instruction = "Keep instructions", Notes = "Keep notes",
            Annotations = [new("a","redact",.1,.1,.2,.2)], Crop = new(.1,.1,.8,.8), Cuts = [new("horizontal",.1,.2)], NeedsReview = false
        }] };
        var id = guide.Steps[1].Id;
        Assert.Equal(id, ScreenshotSteps.Attach(guide, Image, id));
        var step = guide.Steps[1];
        Assert.Null(guide.Steps[0].AssetId);
        Assert.Equal("Select Save", step.Title); Assert.Equal("Keep instructions", step.Instruction); Assert.Equal("Keep notes", step.Notes);
        Assert.Equal("Save", Assert.Single(step.TitleFields)); Assert.True(step.NeedsReview);
        Assert.Empty(step.Annotations); Assert.Null(step.Crop); Assert.Empty(step.Cuts);
        GuideJson.Validate(guide);
    }
    [Fact]
    public void ScreenshotCreatesStepsInEmptyAndExistingGuidesAndRejectsUnknownTargets()
    {
        var guide = new Guide { Assets = [Image] };
        var first = ScreenshotSteps.Attach(guide, Image);
        var second = ScreenshotSteps.Attach(guide, Image);
        Assert.NotEqual(first, second);
        Assert.Equal(new[] { 1, 2 }, guide.Steps.Select(s => s.Order));
        Assert.All(guide.Steps, s => { Assert.Equal("image", s.AssetId); Assert.True(s.NeedsReview); });
        Assert.Throws<InvalidDataException>(() => ScreenshotSteps.Attach(guide, Image, Guid.NewGuid()));
        Assert.Equal(2, guide.Steps.Count);
    }
}
