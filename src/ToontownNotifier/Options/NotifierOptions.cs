namespace ToontownNotifier.Options;

public sealed class NotifierOptions
{
    public const string SectionName = "Notifier";

    public string DiscordWebhookUrl { get; set; } = "";
    public int PollIntervalSeconds { get; set; } = 30;
    public string CompanionHost { get; set; } = "127.0.0.1";
    public int CompanionPortStart { get; set; } = 1547;
    public int CompanionPortEnd { get; set; } = 1552;
    public int CompanionProxyPort { get; set; }
    public string StatePath { get; set; } = "data/state.json";
    public string TasksYamlPath { get; set; } = "tasks.yaml";
    public string CogsPath { get; set; } = "Data/cogs.json";
    public string ToonHqInvasionsUrl { get; set; } = "https://toonhq.org/invasions/";
    public string InvasionsApiUrl { get; set; } = "https://www.toontownrewritten.com/api/invasions";
    public string UserAgent { get; set; } = "ToontownNotifier/1.0 (personal invasion task matcher)";
    public string MockTask { get; set; } = "";
}
