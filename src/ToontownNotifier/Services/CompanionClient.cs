using System.Text.Json;
using Microsoft.Extensions.Options;
using ToontownNotifier.Models;
using ToontownNotifier.Options;

namespace ToontownNotifier.Services;

public sealed class CompanionClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotifierOptions _options;
    private readonly ILogger<CompanionClient> _logger;
    private readonly string _authorization = Guid.NewGuid().ToString("N");
    private int? _lastPort;

    public CompanionClient(
        IHttpClientFactory httpClientFactory,
        IOptions<NotifierOptions> options,
        ILogger<CompanionClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CompanionSnapshot?> TryGetSnapshotAsync(CancellationToken cancellationToken)
    {
        var ports = EnumeratePorts();
        foreach (var port in ports)
        {
            try
            {
                var snapshot = await FetchAsync(port, cancellationToken);
                if (snapshot is not null)
                {
                    _lastPort = port;
                    return snapshot;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Companion App port {Port} is unavailable", port);
            }
        }

        return null;
    }

    private IEnumerable<int> EnumeratePorts()
    {
        if (_lastPort is int last)
        {
            yield return last;
        }

        for (var port = _options.CompanionPortStart; port <= _options.CompanionPortEnd; port++)
        {
            if (port != _lastPort)
            {
                yield return port;
            }
        }
    }

    private async Task<CompanionSnapshot?> FetchAsync(int port, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("companion");
        var url = $"http://{FormatHost(_options.CompanionHost)}:{port}/info.json";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Host", $"localhost:{port}");
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
        request.Headers.TryAddWithoutValidation("Authorization", _authorization);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Companion App on port {Port} returned {StatusCode}", port, (int)response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseSnapshot(document.RootElement);
    }

    private static CompanionSnapshot ParseSnapshot(JsonElement root)
    {
        string? toonName = null;
        if (root.TryGetProperty("toon", out var toon) && toon.ValueKind == JsonValueKind.Object
            && toon.TryGetProperty("name", out var nameElement))
        {
            toonName = nameElement.GetString();
        }

        var tasks = ParseTasks(root.TryGetProperty("tasks", out var tasksElement) ? tasksElement : default);
        return new CompanionSnapshot(toonName, tasks);
    }

    private static List<CompanionTask> ParseTasks(JsonElement tasksElement)
    {
        JsonElement array = default;
        if (tasksElement.ValueKind == JsonValueKind.Array)
        {
            array = tasksElement;
        }
        else if (tasksElement.ValueKind == JsonValueKind.Object
                 && tasksElement.TryGetProperty("tasks", out var nested)
                 && nested.ValueKind == JsonValueKind.Array)
        {
            array = nested;
        }
        else
        {
            return [];
        }

        var tasks = new List<CompanionTask>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var objective = item.TryGetProperty("objective", out var objectiveElement) ? objectiveElement : default;
            var text = ReadString(objective, "text") ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var where = ReadString(objective, "where") ?? "Anywhere";
            string? progressText = null;
            int? current = null;
            int? target = null;
            if (objective.ValueKind == JsonValueKind.Object
                && objective.TryGetProperty("progress", out var progress)
                && progress.ValueKind == JsonValueKind.Object)
            {
                progressText = ReadString(progress, "text");
                current = ReadInt(progress, "current");
                target = ReadInt(progress, "target");
            }

            tasks.Add(new CompanionTask(
                text,
                where,
                progressText,
                current,
                target,
                ReadString(item, "reward")));
        }

        return tasks;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind is JsonValueKind.String or JsonValueKind.Null ? value.GetString() : value.ToString();
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    }

    private static string FormatHost(string host) =>
        host.Contains(':') && !host.StartsWith('[') ? $"[{host}]" : host;
}
