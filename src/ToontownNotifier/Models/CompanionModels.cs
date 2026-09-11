namespace ToontownNotifier.Models;

public sealed record CompanionSnapshot(
    string? ToonName,
    IReadOnlyList<CompanionTask> Tasks);

public sealed record CompanionTask(
    string ObjectiveText,
    string Where,
    string? ProgressText,
    int? Current,
    int? Target,
    string? Reward)
{
    public string Summary
    {
        get
        {
            var location = string.IsNullOrWhiteSpace(Where) ? "Anywhere" : Where;
            var progress = string.IsNullOrWhiteSpace(ProgressText) ? "" : $" ({ProgressText})";
            return $"{ObjectiveText} — {location}{progress}";
        }
    }
}
