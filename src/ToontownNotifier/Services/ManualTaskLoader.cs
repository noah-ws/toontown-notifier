using Microsoft.Extensions.Options;
using ToontownNotifier.Models;
using ToontownNotifier.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ToontownNotifier.Services;

public sealed class ManualTaskLoader
{
    private readonly NotifierOptions _options;
    private readonly CogCatalog _catalog;
    private readonly ILogger<ManualTaskLoader> _logger;

    public ManualTaskLoader(IOptions<NotifierOptions> options, CogCatalog catalog, ILogger<ManualTaskLoader> logger)
    {
        _options = options.Value;
        _catalog = catalog;
        _logger = logger;
    }

    public NeededCogs? TryLoadMock()
    {
        var mock = _options.MockTask.Trim();
        if (mock.Length == 0)
        {
            return null;
        }

        if (mock.Equals("any", StringComparison.OrdinalIgnoreCase)
            || mock.Equals("true", StringComparison.OrdinalIgnoreCase)
            || mock.Equals("*", StringComparison.OrdinalIgnoreCase)
            || mock.Equals("any_cog", StringComparison.OrdinalIgnoreCase))
        {
            return new NeededCogs
            {
                AnyCog = true,
                SourceLabel = "mock",
                SourceSummaries = ["Mock task: defeat any Cog"]
            };
        }

        var cog = _catalog.CanonicalCog(mock);
        if (cog is not null)
        {
            return new NeededCogs
            {
                SpecificCogs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cog },
                SourceLabel = "mock",
                SourceSummaries = [$"Mock task: defeat {cog}"]
            };
        }

        var department = _catalog.CanonicalDepartment(mock);
        if (department is not null)
        {
            return new NeededCogs
            {
                Departments = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { department },
                SourceLabel = "mock",
                SourceSummaries = [$"Mock task: defeat {department}s"]
            };
        }

        _logger.LogWarning("MOCK_TASK value is not a known cog or department: {Mock}", mock);
        return null;
    }

    public NeededCogs? TryLoad()
    {
        var path = ResolvePath(_options.TasksYamlPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var manual = deserializer.Deserialize<ManualTasks>(yaml) ?? new ManualTasks();
            var specific = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cog in manual.Cogs)
            {
                var canonical = _catalog.CanonicalCog(cog);
                if (canonical is null)
                {
                    _logger.LogWarning("Unknown cog in tasks.yaml: {Cog}", cog);
                    continue;
                }

                specific.Add(canonical);
            }

            var departments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var department in manual.Departments)
            {
                var canonical = _catalog.CanonicalDepartment(department);
                if (canonical is null)
                {
                    _logger.LogWarning("Unknown department in tasks.yaml: {Department}", department);
                    continue;
                }

                departments.Add(canonical);
            }

            var needed = new NeededCogs
            {
                AnyCog = manual.AnyCog,
                SpecificCogs = specific,
                Departments = departments,
                SourceLabel = "tasks.yaml",
                SourceSummaries = BuildSummaries(manual.AnyCog, specific, departments)
            };

            return needed.IsEmpty ? null : needed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read {Path}", path);
            return null;
        }
    }

    private static List<string> BuildSummaries(bool anyCog, HashSet<string> cogs, HashSet<string> departments)
    {
        var summaries = new List<string>();
        if (anyCog)
        {
            summaries.Add("any Cog (tasks.yaml)");
        }

        summaries.AddRange(cogs.Select(cog => $"{cog} (tasks.yaml)"));
        summaries.AddRange(departments.Select(department => $"{department}s (tasks.yaml)"));
        return summaries;
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
}
