namespace ToontownNotifier.Models;

public sealed class AppState
{
    public string? ToonName { get; set; }
    public List<CachedTask> CachedTasks { get; set; } = [];
    public DateTimeOffset? CachedAt { get; set; }
    public HashSet<string> NotifiedInvasionKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CachedTask
{
    public string ObjectiveText { get; set; } = "";
    public string Where { get; set; } = "Anywhere";
    public string? ProgressText { get; set; }
    public int? Current { get; set; }
    public int? Target { get; set; }
    public string? Reward { get; set; }

    public CompanionTask ToCompanionTask() =>
        new(ObjectiveText, Where, ProgressText, Current, Target, Reward);

    public static CachedTask From(CompanionTask task) => new()
    {
        ObjectiveText = task.ObjectiveText,
        Where = task.Where,
        ProgressText = task.ProgressText,
        Current = task.Current,
        Target = task.Target,
        Reward = task.Reward
    };
}
