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

    /// <summary>Jour du tout premier lancement du panel sur ce PC (« Membre depuis »).</summary>
    public DateOnly FirstLaunchDate { get; private set; }

    public void Load()
    {
        var readable = LoadFromDisk();
        EnsureFirstLaunchDate(persist: readable);
    }

    /// <summary>false si le fichier existe mais n'a pas pu être lu (on évite alors de l'écraser).</summary>
    private bool LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_path))
                return true;

            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path));
            if (data is null)
                return false;

            if (!string.IsNullOrWhiteSpace(data.Language))
                Language = data.Language.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(data.KeyboardLayout))
                KeyboardLayout = data.KeyboardLayout.Trim().ToLowerInvariant();

            OnboardingCompleted = data.OnboardingCompleted;
            EasterEggUnlocked = data.EasterEggUnlocked;
            LastUpdateCheckUtc = data.LastUpdateCheckUtc;
            SkippedUpdateVersion = data.SkippedUpdateVersion;
            FirstLaunchDate = data.FirstLaunchDate ?? default;
            return true;
        }
        catch
        {
            Language = "fr";
            KeyboardLayout = "azerty";
            return false;
        }
    }

    private void EnsureFirstLaunchDate(bool persist)
    {
        if (FirstLaunchDate != default)
            return;

        // Mise à jour d'une ancienne version : la date de création des fichiers du panel donne le premier lancement.
        var earliest = DateTime.Now;
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
                {
                    var created = File.GetCreationTime(file);
                    if (created < earliest)
                        earliest = created;
                }
            }
        }
        catch
        {
            // Ignorer : on garde aujourd'hui.
        }

        FirstLaunchDate = DateOnly.FromDateTime(earliest);
        if (persist)
            Persist();
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
                FirstLaunchDate = FirstLaunchDate == default ? null : FirstLaunchDate,
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
        public DateOnly? FirstLaunchDate { get; set; }
    }
}
