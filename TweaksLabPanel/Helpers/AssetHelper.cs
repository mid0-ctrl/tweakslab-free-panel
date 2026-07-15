using System.IO;
using System.Windows.Media;

namespace TweaksLabPanel.Helpers;

public static class AssetHelper
{
    public static string Resolve(string relativePath)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, relativePath);
    }

    public static string Pack(string resourcePath) => $"pack://application:,,,/{resourcePath}";

    public static ImageSource LoadImage(string relativePath)
    {
        var file = Resolve(relativePath);
        if (File.Exists(file))
            return new System.Windows.Media.Imaging.BitmapImage(new Uri(file, UriKind.Absolute));

        return new System.Windows.Media.Imaging.BitmapImage(new Uri(Pack(relativePath), UriKind.Absolute));
    }

    public static string Background(string fileName) => Resolve(Path.Combine("Assets", "Backgrounds", fileName));
    public static string Avatar(string fileName) => Resolve(Path.Combine("Assets", "Avatars", fileName));
    public static string Wallpaper(string fileName) => Resolve(Path.Combine("Assets", "Wallpapers", fileName));
    public static string Game(string fileName) => Resolve(Path.Combine("Assets", "games", fileName));
    public static string App(string fileName) => Resolve(Path.Combine("Assets", "apps", fileName));
    public static string Social(string fileName) => Resolve(Path.Combine("Assets", "social", fileName));
    public static string Gpu(string fileName) => Resolve(Path.Combine("Assets", "gpu", fileName));
    public static string GpuGuide(string fileName) => Resolve(Path.Combine("Assets", "gpu", "guide", fileName));
    public static string PowerPlan => Resolve(Path.Combine("Assets", "power-plans", "Free-TweaksLab-Community.pow"));
    public static string Logo => Resolve(Path.Combine("Assets", "logo.png"));
    public static string LogoIco => Resolve(Path.Combine("Assets", "logo.ico"));
    public static string LogoTetsuya => Resolve(Path.Combine("Assets", "logo-tetsuya.png"));
    public static string AssistantBryan => Resolve(Path.Combine("Assets", "assistant-bryan.png"));
}
