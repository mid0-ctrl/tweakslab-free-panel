using System.IO;
using System.Text.Json;

namespace TweaksLabPanel.Core;

public sealed class AppSettingsStore
{
    public static AppSettingsStore Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "settings.json");

    public string Language { get; private set; } = "fr";
    public string KeyboardLayout { get; private set; } = "azerty";
    public bool OnboardingCompleted { get; private set; }
    public bool EasterEggUnlocked { get; private set; }
    public DateTime LastUpdateCheckUtc { get; private set; }
    public string? SkippedUpdateVersion { get; private set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;

            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path));
            if (data is null)
                return;

            if (!string.IsNullOrWhiteSpace(data.Language))
                Language = data.Language.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(data.KeyboardLayout))
                KeyboardLayout = data.KeyboardLayout.Trim().ToLowerInvariant();

            OnboardingCompleted = data.OnboardingCompleted;
            EasterEggUnlocked = data.EasterEggUnlocked;
            LastUpdateCheckUtc = data.LastUpdateCheckUtc;
            SkippedUpdateVersion = data.SkippedUpdateVersion;
        }
        catch
        {
            Language = "fr";
            KeyboardLayout = "azerty";
        }
    }

    public void SaveLanguage(string language)
    {
        Language = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";
        Persist();
    }

    public void SaveKeyboardLayout(string layout)
    {
        KeyboardLayout = string.Equals(layout, "qwerty", StringComparison.OrdinalIgnoreCase) ? "qwerty" : "azerty";
        Persist();
    }

    public void MarkOnboardingCompleted()
    {
        OnboardingCompleted = true;
        Persist();
    }

    public void ResetOnboarding()
    {
        OnboardingCompleted = false;
        Persist();
    }

    public void UnlockEasterEgg()
    {
        EasterEggUnlocked = true;
        Persist();
    }

    public void MarkUpdateChecked()
    {
        LastUpdateCheckUtc = DateTime.UtcNow;
        Persist();
    }

    public void SkipUpdateVersion(string tag)
    {
        SkippedUpdateVersion = tag.Trim();
        Persist();
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var data = new SettingsData
            {
                Language = Language,
                KeyboardLayout = KeyboardLayout,
                OnboardingCompleted = OnboardingCompleted,
                EasterEggUnlocked = EasterEggUnlocked,
                LastUpdateCheckUtc = LastUpdateCheckUtc,
                SkippedUpdateVersion = SkippedUpdateVersion,
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Ignorer.
        }
    }

    private sealed class SettingsData
    {
        public string Language { get; set; } = "fr";
        public string KeyboardLayout { get; set; } = "azerty";
        public bool OnboardingCompleted { get; set; }
        public bool EasterEggUnlocked { get; set; }
        public DateTime LastUpdateCheckUtc { get; set; }
        public string? SkippedUpdateVersion { get; set; }
    }
}
