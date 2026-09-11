using ToontownNotifier.Models;

namespace ToontownNotifier.Services;

public sealed class InvasionMatcher
{
    private readonly CogCatalog _catalog;

    public InvasionMatcher(CogCatalog catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyList<InvasionMatch> Match(IEnumerable<Invasion> invasions, NeededCogs needed)
    {
        if (needed.IsEmpty)
        {
            return [];
        }

        var matches = new List<InvasionMatch>();
        foreach (var invasion in invasions)
        {
            if (TryMatch(invasion, needed) is { } reason)
            {
                matches.Add(new InvasionMatch(invasion, reason));
            }
        }

        return matches;
    }

    private string? TryMatch(Invasion invasion, NeededCogs needed)
    {
        var (baseName, isSkelecog, _) = CogCatalog.NormalizeInvasionType(invasion.Type);
        var canonical = _catalog.CanonicalCog(baseName) ?? baseName;
        var department = _catalog.DepartmentOf(canonical);

        if (needed.SkelecogsOnly)
        {
            return isSkelecog ? "Skelecog task" : null;
        }

        if (needed.AnyCog)
        {
            return "any Cog task";
        }

        if (needed.SpecificCogs.Contains(canonical))
        {
            return $"{canonical} task";
        }

        if (department is not null && needed.Departments.Contains(department))
        {
            return $"{department} task";
        }

        return null;
    }
}
