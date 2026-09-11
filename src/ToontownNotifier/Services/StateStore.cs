using System.Text.Json;
using Microsoft.Extensions.Options;
using ToontownNotifier.Models;
using ToontownNotifier.Options;

namespace ToontownNotifier.Services;

public sealed class StateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly NotifierOptions _options;
    private readonly ILogger<StateStore> _logger;

    public StateStore(IOptions<NotifierOptions> options, ILogger<StateStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public AppState Load()
    {
        var path = ResolvePath(_options.StatePath);
        if (!File.Exists(path))
        {
            return new AppState();
        }

        try
        {
            var json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<AppState>(json, JsonOptions) ?? new AppState();
            state.NotifiedInvasionKeys = new HashSet<string>(state.NotifiedInvasionKeys ?? [], StringComparer.OrdinalIgnoreCase);
            state.CachedTasks ??= [];
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read state file {Path}; starting fresh", path);
            return new AppState();
        }
    }

    public void Save(AppState state)
    {
        var path = ResolvePath(_options.StatePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
}
