using Microsoft.Win32;
using TweaksLabPanel.Core;
using TweaksLabPanel.Services;

// Reset saved profile to test defaults
WindowsAppearanceState.RestoredProfile = null;
WindowsAppearanceState.Active = false;

static void Dump(string label)
{
    using var adv = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
    using var desk = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
    using var fx = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
    var mask = desk?.GetValue("UserPreferencesMask") as byte[];
    Console.WriteLine($"{label}: VFX={fx?.GetValue("VisualFXSetting")} mask={BitConverter.ToString(mask ?? [])} checked≈registry");
}

Dump("start");
var disable = WindowsAppearanceService.DisableAsync().GetAwaiter().GetResult();
Console.WriteLine($"Disable={disable.Success}");
Dump("after disable");
var enable = WindowsAppearanceService.EnableAsync().GetAwaiter().GetResult();
Console.WriteLine($"Enable={enable.Success}");
Dump("after enable");
