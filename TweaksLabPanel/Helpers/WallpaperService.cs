using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Helpers;

public static class WallpaperService
{
    private const int SpiSetDeskWallpaper = 0x0014;
    private const int SpifUpdateIniFile = 0x01;
    private const int SpifSendChange = 0x02;

    public static int Count => WallpaperCatalog.All.Length;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    public static BitmapImage? GetPreview(int index)
    {
        var preset = WallpaperCatalog.FindByIndex(index);
        if (preset is null)
            return null;

        return LoadBitmap(preset.FileName);
    }

    public static BitmapImage? LoadFeatured(int arrayIndex)
    {
        var featured = WallpaperCatalog.Featured;
        if (arrayIndex < 0 || arrayIndex >= featured.Length)
            return null;

        return LoadBitmap(featured[arrayIndex].FileName);
    }

    public static bool Apply(int index)
    {
        var preset = WallpaperCatalog.FindByIndex(index);
        if (preset is null)
            return false;

        try
        {
            var source = AssetHelper.Wallpaper(preset.FileName);
            if (!File.Exists(source))
                return false;

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TweaksLabPanel");
            Directory.CreateDirectory(dir);

            var dest = Path.Combine(dir, preset.FileName);
            File.Copy(source, dest, overwrite: true);

            if (SystemParametersInfo(SpiSetDeskWallpaper, 0, dest, SpifUpdateIniFile | SpifSendChange) == 0)
                return false;

            WallpaperSelectionState.LastAppliedIndex = index;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static BitmapImage? LoadBitmap(string fileName)
    {
        try
        {
            var path = AssetHelper.Wallpaper(fileName);
            return BitmapLoader.LoadPreview(path, 480);
        }
        catch
        {
            return null;
        }
    }
}
