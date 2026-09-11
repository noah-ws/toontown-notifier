using Microsoft.Extensions.Options;
using ToontownNotifier.Models;
using ToontownNotifier.Options;
using ToontownNotifier.Services;

namespace ToontownNotifier;

public sealed class DiscordTestHost : IHostedService
{
    private readonly InvasionClient _invasions;
    private readonly DiscordNotifier _discord;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly NotifierOptions _options;
    private readonly ILogger<DiscordTestHost> _logger;

    public DiscordTestHost(
        InvasionClient invasions,
        DiscordNotifier discord,
        IHostApplicationLifetime lifetime,
        IOptions<NotifierOptions> options,
        ILogger<DiscordTestHost> logger)
    {
        _invasions = invasions;
        _discord = discord;
        _lifetime = lifetime;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.DiscordWebhookUrl)
                || _options.DiscordWebhookUrl.Contains("your-token", StringComparison.OrdinalIgnoreCase)
                || _options.DiscordWebhookUrl.Contains("your-id", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Set DISCORD_WEBHOOK_URL in .env to a real webhook before using --test-discord.");
                Environment.ExitCode = 1;
                return;
            }

            Invasion invasion;
            try
            {
                var live = await _invasions.GetInvasionsAsync(cancellationToken);
                invasion = live.FirstOrDefault()
                           ?? new Invasion("Test District", "Pencil Pusher", "1200/4000", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch live invasions; sending a fake test invasion instead.");
                invasion = new Invasion("Test District", "Pencil Pusher", "1200/4000", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }

            var needed = new NeededCogs
            {
                SpecificCogs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { invasion.Type },
                SourceLabel = "test",
                SourceSummaries = ["Forced test ping (--test-discord)"]
            };

            await _discord.NotifyAsync(
                new InvasionMatch(invasion, "test ping"),
                needed,
                "Test Toon",
                cancellationToken,
                test: true);

            _logger.LogInformation("Test Discord ping sent for {Type} in {District}.", invasion.Type, invasion.District);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test Discord ping failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
