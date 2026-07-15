using System.IO;
using System.Windows.Media.Imaging;

namespace TweaksLabPanel.Helpers;

public static class BitmapLoader
{
    public static BitmapImage? Load(string path, int? decodePixelWidth = null)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            if (decodePixelWidth is > 0)
                bmp.DecodePixelWidth = decodePixelWidth.Value;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Aperçu tuile : charge ~2x la largeur affichée pour un rendu net.</summary>
    public static BitmapImage? LoadPreview(string path, double displayWidth) =>
        Load(path, (int)Math.Ceiling(displayWidth * 2));

    /// <summary>Fond plein écran : résolution source complète.</summary>
    public static BitmapImage? LoadFull(string path) => Load(path);
}
