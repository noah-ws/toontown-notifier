using Microsoft.Extensions.Options;
using ToontownNotifier.Options;
using ToontownNotifier.Services;

namespace ToontownNotifier;

public class Program
{
    public static void Main(string[] args)
    {
        LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services
            .AddOptions<NotifierOptions>()
            .Bind(builder.Configuration.GetSection(NotifierOptions.SectionName))
            .PostConfigure<IConfiguration>(ApplyEnvironment);

        builder.Services.AddHttpClient("companion", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        builder.Services.AddHttpClient("invasions", (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<NotifierOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
        });

        builder.Services.AddHttpClient("discord", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        builder.Services.AddSingleton(provider =>
            CogCatalog.LoadFromOptions(provider.GetRequiredService<IOptions<NotifierOptions>>().Value));
        builder.Services.AddSingleton<CompanionClient>();
        builder.Services.AddSingleton<InvasionClient>();
        builder.Services.AddSingleton<StateStore>();
        builder.Services.AddSingleton<ManualTaskLoader>();
        builder.Services.AddSingleton<TaskParser>();
        builder.Services.AddSingleton<InvasionMatcher>();
        builder.Services.AddSingleton<DiscordNotifier>();

        var testDiscord = args.Any(arg => string.Equals(arg, "--test-discord", StringComparison.OrdinalIgnoreCase));
        if (testDiscord)
        {
            builder.Services.AddHostedService<DiscordTestHost>();
        }
        else
        {
            builder.Services.AddHostedService<Worker>();
        }

        builder.Build().Run();
    }

    private static void ApplyEnvironment(NotifierOptions options, IConfiguration configuration)
    {
        options.DiscordWebhookUrl = configuration["DISCORD_WEBHOOK_URL"] ?? options.DiscordWebhookUrl;
        options.CompanionHost = configuration["COMPANION_HOST"] ?? options.CompanionHost;
        options.StatePath = configuration["STATE_PATH"] ?? options.StatePath;
        options.TasksYamlPath = configuration["TASKS_YAML_PATH"] ?? options.TasksYamlPath;
        options.CogsPath = configuration["COGS_PATH"] ?? options.CogsPath;
        options.ToonHqInvasionsUrl = configuration["TOONHQ_INVASIONS_URL"] ?? options.ToonHqInvasionsUrl;
        options.InvasionsApiUrl = configuration["INVASIONS_API_URL"] ?? options.InvasionsApiUrl;
        options.UserAgent = configuration["USER_AGENT"] ?? options.UserAgent;
        options.MockTask = configuration["MOCK_TASK"] ?? options.MockTask;

        if (int.TryParse(configuration["POLL_INTERVAL_SECONDS"], out var poll))
        {
            options.PollIntervalSeconds = poll;
        }

        if (int.TryParse(configuration["COMPANION_PORT_START"], out var start))
        {
            options.CompanionPortStart = start;
        }

        if (int.TryParse(configuration["COMPANION_PORT_END"], out var end))
        {
            options.CompanionPortEnd = end;
        }

        if (int.TryParse(configuration["COMPANION_PROXY_PORT"], out var proxyPort))
        {
            options.CompanionProxyPort = proxyPort;
        }
    }

    private static void LoadDotEnv(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"').Trim('\'');
            if (!string.IsNullOrWhiteSpace(key) && Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
