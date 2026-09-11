namespace ToontownNotifier.Models;

public sealed record Invasion(
    string District,
    string Type,
    string Progress,
    long AsOf)
{
    public string Key => $"{District}|{Type}";
}

public sealed record InvasionMatch(Invasion Invasion, string Reason);
