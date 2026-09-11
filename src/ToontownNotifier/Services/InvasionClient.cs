using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ToontownNotifier.Models;
using ToontownNotifier.Options;

namespace ToontownNotifier.Services;

public sealed class InvasionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotifierOptions _options;

    public InvasionClient(IHttpClientFactory httpClientFactory, IOptions<NotifierOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Invasion>> GetInvasionsAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("invasions");
        var payload = await client.GetFromJsonAsync<InvasionApiResponse>(_options.InvasionsApiUrl, JsonOptions, cancellationToken);
        if (payload is null || !string.IsNullOrWhiteSpace(payload.Error) || payload.Invasions is null)
        {
            return [];
        }

        return payload.Invasions
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Type))
            .Select(pair => new Invasion(
                pair.Key,
                pair.Value.Type!,
                pair.Value.Progress ?? "unknown",
                pair.Value.AsOf))
            .ToList();
    }

    private sealed class InvasionApiResponse
    {
        public string? Error { get; set; }
        public Dictionary<string, InvasionApiItem>? Invasions { get; set; }
    }

    private sealed class InvasionApiItem
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("progress")]
        public string? Progress { get; set; }

        [JsonPropertyName("asOf")]
        public long AsOf { get; set; }
    }
}
