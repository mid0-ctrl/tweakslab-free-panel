namespace TweaksLabPanel.Core;

public static class BackgroundSelectionState
{
    public static string SelectedId { get; set; } = BackgroundCatalog.DefaultId;
    public static bool IsCustom { get; set; }
    public static string? CustomPath { get; set; }
}
