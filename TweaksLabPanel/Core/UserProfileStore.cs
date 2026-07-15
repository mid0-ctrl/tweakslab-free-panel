using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Core;

public sealed class UserProfileStore
{
    public static UserProfileStore Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "profile.json");

    public string DisplayName { get; private set; } = string.Empty;
    public bool HasDisplayName { get; private set; }
    public const string DefaultDisplayName = "User";
    public string ResolvedDisplayName => HasDisplayName ? DisplayName : DefaultDisplayName;
    public IReadOnlyList<string> FavoriteGames { get; private set; } = [];
    public string AvatarId { get; private set; } = "avatar-01";
    public string? AvatarCustomPath { get; private set; }
    public string BackgroundId { get; private set; } = BackgroundCatalog.DefaultId;
    public string? BackgroundCustomPath { get; private set; }
    public string Bio { get; private set; } = string.Empty;

    public void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = File.ReadAllText(_settingsPath);
            var data = JsonSerializer.Deserialize<ProfileData>(json);
            if (data is null)
                return;

            if (!string.IsNullOrWhiteSpace(data.DisplayName))
            {
                DisplayName = data.DisplayName.Trim();
                HasDisplayName = true;
            }

            FavoriteGames = data.FavoriteGames?
                .Where(static game => !string.IsNullOrWhiteSpace(game))
                .Select(static game => game.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray() ?? [];

            AvatarId = string.IsNullOrWhiteSpace(data.AvatarId) ? "avatar-01" : data.AvatarId.Trim();
            AvatarCustomPath = NormalizePath(data.AvatarCustomPath);
            BackgroundId = string.IsNullOrWhiteSpace(data.BackgroundId) ? BackgroundCatalog.DefaultId : data.BackgroundId.Trim();
            BackgroundCustomPath = NormalizePath(data.BackgroundCustomPath);
            Bio = TrimBio(data.Bio);

            BackgroundSelectionState.SelectedId = BackgroundId;
            BackgroundSelectionState.IsCustom = !string.IsNullOrWhiteSpace(BackgroundCustomPath);
            BackgroundSelectionState.CustomPath = BackgroundCustomPath;
        }
        catch
        {
            ResetDefaults();
        }
    }

    public void SaveDisplayName(string displayName)
    {
        var trimmed = displayName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        DisplayName = trimmed;
        HasDisplayName = true;
        Persist();
    }

    public void SaveFavoriteGames(IEnumerable<string> favoriteGames)
    {
        FavoriteGames = favoriteGames
            .Where(static game => !string.IsNullOrWhiteSpace(game))
            .Select(static game => game.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        Persist();
    }

    public void SaveAvatar(string avatarId, string? customPath = null)
    {
        AvatarId = string.IsNullOrWhiteSpace(avatarId) ? "avatar-01" : avatarId.Trim();
        AvatarCustomPath = NormalizePath(customPath);
        Persist();
    }

    public void SaveBackground(string backgroundId, string? customPath = null)
    {
        BackgroundId = string.IsNullOrWhiteSpace(backgroundId) ? BackgroundCatalog.DefaultId : backgroundId.Trim();
        BackgroundCustomPath = NormalizePath(customPath);
        BackgroundSelectionState.SelectedId = BackgroundId;
        BackgroundSelectionState.IsCustom = !string.IsNullOrWhiteSpace(BackgroundCustomPath);
        BackgroundSelectionState.CustomPath = BackgroundCustomPath;
        Persist();
    }

    public void SaveBio(string bio)
    {
        Bio = TrimBio(bio);
        Persist();
    }

    public ImageSource? ResolveAvatarSource()
    {
        if (!string.IsNullOrWhiteSpace(AvatarCustomPath) && File.Exists(AvatarCustomPath))
            return new BitmapImage(new Uri(AvatarCustomPath, UriKind.Absolute));

        var file = AvatarId.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? AvatarId
            : $"{AvatarId}.png";
        return new BitmapImage(new Uri(AssetHelper.Avatar(file)));
    }

    public string ResolveBackgroundFileName()
    {
        if (!string.IsNullOrWhiteSpace(BackgroundCustomPath) && File.Exists(BackgroundCustomPath))
            return BackgroundCustomPath;

        return BackgroundCatalog.FileNameFor(BackgroundId)
            ?? BackgroundCatalog.FileNameFor(BackgroundCatalog.DefaultId)
            ?? BackgroundCatalog.All[0].FileName;
    }

    public bool HasCustomBackground => !string.IsNullOrWhiteSpace(BackgroundCustomPath) && File.Exists(BackgroundCustomPath);

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var data = new ProfileData
            {
                DisplayName = DisplayName,
                FavoriteGames = FavoriteGames.ToList(),
                AvatarId = AvatarId,
                AvatarCustomPath = AvatarCustomPath,
                BackgroundId = BackgroundId,
                BackgroundCustomPath = BackgroundCustomPath,
                Bio = Bio,
            };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Persistance locale : ignorer si indisponible.
        }
    }

    private static void ResetDefaults()
    {
        Instance.DisplayName = string.Empty;
        Instance.HasDisplayName = false;
        Instance.FavoriteGames = [];
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.Trim();
        return File.Exists(trimmed) ? trimmed : null;
    }

    private static string TrimBio(string? bio)
    {
        if (string.IsNullOrWhiteSpace(bio))
            return string.Empty;

        var trimmed = bio.Trim();
        return trimmed.Length <= 20 ? trimmed : trimmed[..20];
    }

    private sealed class ProfileData
    {
        public string DisplayName { get; set; } = string.Empty;
        public List<string> FavoriteGames { get; set; } = [];
        public string AvatarId { get; set; } = "avatar-01";
        public string? AvatarCustomPath { get; set; }
        public string BackgroundId { get; set; } = BackgroundCatalog.DefaultId;
        public string? BackgroundCustomPath { get; set; }
        public string Bio { get; set; } = string.Empty;
    }
}
