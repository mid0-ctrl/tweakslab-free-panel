namespace TweaksLabPanel.Core;

public enum StartupEntrySource
{
    UserRun,
    MachineRun,
    UserStartupFolder,
    CommonStartupFolder,
}

public sealed class StartupAppEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public StartupEntrySource Source { get; init; }
    public bool IsEnabled { get; init; }
    public bool CanToggle { get; init; }
}
