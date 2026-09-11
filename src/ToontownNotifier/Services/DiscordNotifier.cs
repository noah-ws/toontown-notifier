using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ToontownNotifier.Models;
using ToontownNotifier.Options;

namespace ToontownNotifier.Services;

public sealed class DiscordNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotifierOptions _options;
    private readonly ILogger<DiscordNotifier> _logger;

    public DiscordNotifier(
        IHttpClientFactory httpClientFactory,
        IOptions<NotifierOptions> options,
        ILogger<DiscordNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyAsync(InvasionMatch match, NeededCogs needed, string? toonName, CancellationToken cancellationToken)
    {
        var invasion = match.Invasion;
        var title = $"{invasion.Type} in {invasion.District}";
        var description = string.IsNullOrWhiteSpace(toonName)
            ? $"This invasion matches your ToonTasks ({match.Reason})."
            : $"This invasion matches **{toonName}**'s ToonTasks ({match.Reason}).";

        var taskLines = needed.SourceSummaries.Count == 0
            ? needed.Describe()
            : string.Join("\n", needed.SourceSummaries.Take(4).Select(summary => $"• {summary}"));

        _logger.LogInformation("Matching invasion: {Type} in {District} ({Progress})", invasion.Type, invasion.District, invasion.Progress);

        if (string.IsNullOrWhiteSpace(_options.DiscordWebhookUrl))
        {
            _logger.LogWarning("DISCORD_WEBHOOK_URL is not set; skipping Discord ping for {Title}", title);
            return;
        }

        var payload = new
        {
            username = "Toontown Notifier",
            embeds = new[]
            {
                new
                {
                    title,
                    description,
                    url = _options.ToonHqInvasionsUrl,
                    color = 0xF5C400,
                    fields = new[]
                    {
                        new { name = "Cog", value = invasion.Type, inline = true },
                        new { name = "District", value = invasion.District, inline = true },
                        new { name = "Progress", value = invasion.Progress, inline = true },
                        new { name = "Why", value = match.Reason, inline = true },
                        new { name = "Your tasks", value = string.IsNullOrWhiteSpace(taskLines) ? "cached/manual cog task" : taskLines, inline = false }
                    },
                    footer = new { text = "Live data from TTR invasions API · tracker on ToonHQ" }
                }
            }
        };

        var client = _httpClientFactory.CreateClient("discord");
        using var response = await client.PostAsJsonAsync(_options.DiscordWebhookUrl, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Discord webhook failed ({Status}): {Body}", (int)response.StatusCode, body);
        }
    }
}
