using System.Text.RegularExpressions;
using ToontownNotifier.Models;

namespace ToontownNotifier.Services;

public sealed class TaskParser
{
    private static readonly Regex CogWord = new(@"\bcogs?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SkelecogWord = new(@"\bskelecogs?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DepartmentWord = new(
        @"\b(bossbots?|lawbots?|cashbots?|sellbots?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CombatWord = new(
        @"\b(defeat|recover|destroy)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NonInvasion = new(
        @"\b(fish|fishing|catch|deliver|visit|golf|racing|race)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FacilityWord = new(
        @"\b(buildings?|floors?|mints?|factories|factory|field offices?|da offices?|cogs? hq)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly CogCatalog _catalog;

    public TaskParser(CogCatalog catalog)
    {
        _catalog = catalog;
    }

    public NeededCogs FromTasks(IEnumerable<CompanionTask> tasks, string sourceLabel)
    {
        var specific = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var departments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var summaries = new List<string>();
        var anyCog = false;
        var skelecogsOnly = false;

        foreach (var task in tasks)
        {
            var parsed = ParseObjective(task.ObjectiveText);
            if (parsed is null)
            {
                continue;
            }

            anyCog |= parsed.AnyCog;
            skelecogsOnly |= parsed.SkelecogsOnly;
            foreach (var cog in parsed.SpecificCogs)
            {
                specific.Add(cog);
            }

            foreach (var department in parsed.Departments)
            {
                departments.Add(department);
            }

            summaries.Add(task.Summary);
        }

        return new NeededCogs
        {
            AnyCog = anyCog,
            SkelecogsOnly = skelecogsOnly && specific.Count == 0 && departments.Count == 0 && !anyCog,
            SpecificCogs = specific,
            Departments = departments,
            SourceSummaries = summaries,
            SourceLabel = sourceLabel
        };
    }

    private NeededCogs? ParseObjective(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || NonInvasion.IsMatch(text) || !CombatWord.IsMatch(text))
        {
            return null;
        }

        var specific = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cog in _catalog.CanonicalNamesLongestFirst)
        {
            if (ContainsCogName(text, cog))
            {
                specific.Add(cog);
            }
        }

        var departments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in DepartmentWord.Matches(text))
        {
            var department = _catalog.CanonicalDepartment(match.Value);
            if (department is not null)
            {
                departments.Add(department);
            }
        }

        var skelecogsOnly = SkelecogWord.IsMatch(text);
        var mentionsCog = CogWord.IsMatch(text) || skelecogsOnly;
        var facilityOnly = FacilityWord.IsMatch(text) && specific.Count == 0 && departments.Count == 0;
        if (facilityOnly)
        {
            return null;
        }

        var anyCog = specific.Count == 0 && departments.Count == 0 && mentionsCog && !skelecogsOnly;
        if (specific.Count == 0 && departments.Count == 0 && !anyCog && !skelecogsOnly)
        {
            return null;
        }

        return new NeededCogs
        {
            AnyCog = anyCog,
            SkelecogsOnly = skelecogsOnly && specific.Count == 0 && departments.Count == 0,
            SpecificCogs = specific,
            Departments = departments
        };
    }

    private static bool ContainsCogName(string text, string cogName)
    {
        var index = text.IndexOf(cogName, StringComparison.OrdinalIgnoreCase);
        return index >= 0;
    }
}
