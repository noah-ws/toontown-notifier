namespace ToontownNotifier.Models;

public sealed class ManualTasks
{
    public List<string> Cogs { get; set; } = [];
    public List<string> Departments { get; set; } = [];
    public bool AnyCog { get; set; }
}
