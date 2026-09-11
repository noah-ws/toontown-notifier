using System.Text.Json;
using ToontownNotifier.Options;

namespace ToontownNotifier.Services;

public sealed class CogCatalog
{
    private readonly Dictionary<string, string> _canonicalByLookup;
    private readonly Dictionary<string, string> _departmentByCog;
    private readonly List<string> _canonicalNamesLongestFirst;

    public CogCatalog(IReadOnlyDictionary<string, IReadOnlyList<string>> departments, IReadOnlyDictionary<string, string> aliases)
    {
        _departmentByCog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _canonicalByLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (department, cogs) in departments)
        {
            foreach (var cog in cogs)
            {
                _departmentByCog[cog] = department;
                AddLookup(cog, cog);
            }
        }

        foreach (var (alias, canonical) in aliases)
        {
            AddLookup(alias, canonical);
        }

        _canonicalNamesLongestFirst = _departmentByCog.Keys
            .OrderByDescending(name => name.Length)
            .ToList();
    }

    public IReadOnlyList<string> CanonicalNamesLongestFirst => _canonicalNamesLongestFirst;

    public static CogCatalog Load(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var departments = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.GetProperty("departments").EnumerateObject())
        {
            departments[property.Name] = property.Value.EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .Where(x => x.Length > 0)
                .ToList();
        }

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("aliases", out var aliasElement))
        {
            foreach (var property in aliasElement.EnumerateObject())
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    aliases[property.Name] = value;
                }
            }
        }

        return new CogCatalog(departments, aliases);
    }

    public static CogCatalog LoadFromOptions(NotifierOptions options)
    {
        var path = options.CogsPath;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Cog catalog not found at {path}", path);
        }

        return Load(path);
    }

    public string? CanonicalCog(string name)
    {
        var trimmed = name.Trim();
        return _canonicalByLookup.TryGetValue(trimmed, out var canonical) ? canonical : null;
    }

    public string? DepartmentOf(string canonicalCog) =>
        _departmentByCog.TryGetValue(canonicalCog, out var department) ? department : null;

    public string? CanonicalDepartment(string name)
    {
        var trimmed = name.Trim().TrimEnd('s', 'S');
        return trimmed.ToLowerInvariant() switch
        {
            "bossbot" => "Bossbot",
            "lawbot" => "Lawbot",
            "cashbot" => "Cashbot",
            "sellbot" => "Sellbot",
            _ => null
        };
    }

    public static (string BaseName, bool IsSkelecog, bool IsVersion2) NormalizeInvasionType(string type)
    {
        var value = type.Trim();
        var isVersion2 = value.StartsWith("Version 2.0 ", StringComparison.OrdinalIgnoreCase);
        if (isVersion2)
        {
            value = value["Version 2.0 ".Length..];
        }

        var isSkelecog = value.EndsWith(" (Skelecog)", StringComparison.OrdinalIgnoreCase);
        if (isSkelecog)
        {
            value = value[..^" (Skelecog)".Length];
        }

        return (value.Trim(), isSkelecog, isVersion2);
    }

    private void AddLookup(string lookup, string canonical)
    {
        _canonicalByLookup[lookup] = canonical;
        _canonicalByLookup[lookup.Replace("&", "and", StringComparison.Ordinal)] = canonical;
    }
}
