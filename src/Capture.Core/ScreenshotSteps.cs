namespace Capture.Core;

public static class ScreenshotSteps
{
    public static Guid Attach(Guide guide, Asset image, Guid? stepId = null)
    {
        var step = stepId is null ? new Step { Title = "Screenshot" } :
            guide.Steps.SingleOrDefault(s => s.Id == stepId) ?? throw new InvalidDataException("Select a step in the current guide first.");
        if (stepId is null) guide.Steps.Add(step);
        step.AssetId = image.Id;
        step.Annotations.Clear(); step.Crop = null; step.Cuts.Clear(); step.NeedsReview = true;
        for (var i = 0; i < guide.Steps.Count; i++) guide.Steps[i].Order = i + 1;
        return step.Id;
    }
}
