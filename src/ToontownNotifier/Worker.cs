using Microsoft.Extensions.Options;
using ToontownNotifier.Models;
using ToontownNotifier.Options;
using ToontownNotifier.Services;

namespace ToontownNotifier;

public sealed class Worker : BackgroundService
{
    private readonly CompanionClient _companion;
    private readonly InvasionClient _invasions;
    private readonly TaskParser _taskParser;
    private readonly ManualTaskLoader _manualTasks;
    private readonly InvasionMatcher _matcher;
    private readonly DiscordNotifier _discord;
    private readonly StateStore _stateStore;
    private readonly NotifierOptions _options;
    private readonly ILogger<Worker> _logger;

    private bool _loggedCompanionDown;
    private string? _lastNeededDescription;

    public Worker(
        CompanionClient companion,
        InvasionClient invasions,
        TaskParser taskParser,
        ManualTaskLoader manualTasks,
        InvasionMatcher matcher,
        DiscordNotifier discord,
        StateStore stateStore,
        IOptions<NotifierOptions> options,
        ILogger<Worker> logger)
    {
        _companion = companion;
        _invasions = invasions;
        _taskParser = taskParser;
        _manualTasks = manualTasks;
        _matcher = matcher;
        _discord = discord;
        _stateStore = stateStore;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Toontown notifier started. Polling every {Seconds}s. Companion host {Host}:{Start}-{End}",
            _options.PollIntervalSeconds,
            _options.CompanionHost,
            _options.CompanionPortStart,
            _options.CompanionPortEnd);

        var state = _stateStore.Load();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(state, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling loop failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PollOnceAsync(AppState state, CancellationToken cancellationToken)
    {
        var live = await _companion.TryGetSnapshotAsync(cancellationToken);
        NeededCogs needed;
        string? toonName = state.ToonName;

        if (live is not null)
        {
            _loggedCompanionDown = false;
            toonName = live.ToonName ?? toonName;
            needed = _taskParser.FromTasks(live.Tasks, "companion");
            state.ToonName = toonName;
            state.CachedTasks = live.Tasks.Select(CachedTask.From).ToList();
            state.CachedAt = DateTimeOffset.UtcNow;
            _stateStore.Save(state);
        }
        else
        {
            if (!_loggedCompanionDown)
            {
                _logger.LogInformation(
                    "Toontown Companion App is not reachable on {Host}. Using cached tasks or tasks.yaml. Enable Companion App Support in-game and approve the prompt when you play.",
                    _options.CompanionHost);
                _loggedCompanionDown = true;
            }

            if (state.CachedTasks.Count > 0)
            {
                needed = _taskParser.FromTasks(state.CachedTasks.Select(t => t.ToCompanionTask()), "cache");
            }
            else
            {
                needed = _manualTasks.TryLoad() ?? new NeededCogs { SourceLabel = "none" };
            }
        }

        LogNeededIfChanged(needed, toonName, live is not null);

        var invasions = await _invasions.GetInvasionsAsync(cancellationToken);
        var activeKeys = invasions.Select(invasion => invasion.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        state.NotifiedInvasionKeys.RemoveWhere(key => !activeKeys.Contains(key));

        var matches = _matcher.Match(invasions, needed);
        foreach (var match in matches)
        {
            if (!state.NotifiedInvasionKeys.Add(match.Invasion.Key))
            {
                continue;
            }

            await _discord.NotifyAsync(match, needed, toonName, cancellationToken);
        }

        _stateStore.Save(state);
    }

    private void LogNeededIfChanged(NeededCogs needed, string? toonName, bool live)
    {
        var description = $"{needed.SourceLabel}:{needed.Describe()}";
        if (description == _lastNeededDescription)
        {
            return;
        }

        _lastNeededDescription = description;
        var who = string.IsNullOrWhiteSpace(toonName) ? "your Toon" : toonName;
        var freshness = live ? "live" : needed.SourceLabel;
        if (needed.IsEmpty)
        {
            _logger.LogInformation("{Toon} has {Needed} ({Source})", who, needed.Describe(), freshness);
            return;
        }

        _logger.LogInformation("{Toon} needs: {Needed} ({Source})", who, needed.Describe(), freshness);
        foreach (var summary in needed.SourceSummaries)
        {
            _logger.LogInformation("  {Task}", summary);
        }
    }
}
