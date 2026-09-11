namespace ToontownNotifier.Models;

public sealed class NeededCogs
{
    public bool AnyCog { get; init; }
    public bool SkelecogsOnly { get; init; }
    public HashSet<string> SpecificCogs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Departments { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> SourceSummaries { get; init; } = [];
    public string SourceLabel { get; init; } = "unknown";

    public bool IsEmpty =>
        !AnyCog && !SkelecogsOnly && SpecificCogs.Count == 0 && Departments.Count == 0;

    public string Describe()
    {
        if (IsEmpty)
        {
            return "no cog-related tasks";
        }

        var parts = new List<string>();
        if (AnyCog)
        {
            parts.Add("any Cog");
        }

        if (SkelecogsOnly)
        {
            parts.Add("Skelecogs");
        }

        parts.AddRange(SpecificCogs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        parts.AddRange(Departments.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(d => $"{d}s"));
        return string.Join(", ", parts);
    }
}
