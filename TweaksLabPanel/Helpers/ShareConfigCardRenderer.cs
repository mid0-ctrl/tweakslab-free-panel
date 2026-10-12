using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Helpers;

public static class ShareConfigCardRenderer
{
    private const double PadH = 20;
    private const double PadV = 18;
    private const double BorderInset = 10;
    private const double CornerRadius = 14;
    private const double NameSize = 18;
    private const double SpecSize = 13;
    private const double NameToSpecsGap = 10;
    private const double SpecLineHeight = 21;
    private const double MaxTextWidth = 640;

    public static bool TryCopyToClipboard(string displayName, SystemSpecsSnapshot specs)
    {
        try
        {
            var image = Render(displayName, specs);
            CopyImageToClipboard(image);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static BitmapSource Render(string displayName, SystemSpecsSnapshot specs)
    {
        var accent = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7));
        var specBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xE8));
        var bg = new SolidColorBrush(Color.FromRgb(0x12, 0x10, 0x1C));
        accent.Freeze();
        specBrush.Freeze();
        bg.Freeze();

        var pixelsPerDip = GetPixelsPerDip();
        var safeName = string.IsNullOrWhiteSpace(displayName)
            ? UserProfileStore.DefaultDisplayName
            : displayName.Trim();

        var nameText = Format(safeName, NameSize, FontWeights.SemiBold, accent, pixelsPerDip, MaxTextWidth);
        var specLines = BuildSpecLines(specs);
        var specTexts = specLines
            .Select(l => Format(l, SpecSize, FontWeights.Normal, specBrush, pixelsPerDip, MaxTextWidth))
            .ToList();

        var contentWidth = Math.Max(nameText.Width, specTexts.Count > 0 ? specTexts.Max(t => t.Width) : 0);
        var contentHeight = nameText.Height + NameToSpecsGap + specTexts.Count * SpecLineHeight;

        var width = Math.Max(1, (int)Math.Ceiling(BorderInset * 2 + PadH * 2 + contentWidth));
        var height = Math.Max(1, (int)Math.Ceiling(BorderInset * 2 + PadV * 2 + contentHeight));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(bg, null, new Rect(0, 0, width, height));
            dc.DrawRoundedRectangle(
                null,
                new Pen(accent, 2),
                new Rect(BorderInset, BorderInset, width - BorderInset * 2, height - BorderInset * 2),
                CornerRadius,
                CornerRadius);

            var x = BorderInset + PadH;
            var y = BorderInset + PadV;

            dc.DrawText(nameText, new Point(x, y));
            y += nameText.Height + NameToSpecsGap;

            foreach (var spec in specTexts)
            {
                dc.DrawText(spec, new Point(x, y));
                y += SpecLineHeight;
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void CopyImageToClipboard(BitmapSource image)
    {
        var clipboardImage = EnsureClipboardCompatible(image);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                var pngStream = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(clipboardImage));
                encoder.Save(pngStream);
                pngStream.Position = 0;

                var data = new DataObject();
                data.SetImage(clipboardImage);
                data.SetData("PNG", pngStream, autoConvert: false);
                Clipboard.SetDataObject(data, copy: true);
                return;
            }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 5)
            {
                Thread.Sleep(40 * (attempt + 1));
            }
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Clipboard.SetImage(clipboardImage);
                return;
            }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 3)
            {
                Thread.Sleep(40 * (attempt + 1));
            }
        }

        throw new InvalidOperationException(LocalizationService.T("Le presse-papiers est indisponible.", "The clipboard is unavailable."));
    }

    private static BitmapSource EnsureClipboardCompatible(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
            return source;

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static double GetPixelsPerDip()
    {
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow is not null && PresentationSource.FromVisual(mainWindow) is not null)
            return VisualTreeHelper.GetDpi(mainWindow).PixelsPerDip;

        return 1.0;
    }

    private static string[] BuildSpecLines(SystemSpecsSnapshot specs) =>
    [
        $"{LocalizationService.T("OS :", "OS:")} {specs.OperatingSystem}",
        $"{LocalizationService.T("CPU :", "CPU:")} {specs.Processor} ({specs.ProcessorDetails})",
        $"{LocalizationService.T("GPU :", "GPU:")} {specs.GraphicsCard}",
        $"{LocalizationService.T("RAM :", "RAM:")} {specs.Memory}",
        $"{LocalizationService.T("Carte mère :", "Motherboard:")} {specs.Motherboard}",
        $"{LocalizationService.T("Stockage :", "Storage:")} {specs.Storage}",
    ];

    private static FormattedText Format(
        string text,
        double size,
        FontWeight weight,
        Brush brush,
        double pixelsPerDip,
        double maxTextWidth)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            brush,
            pixelsPerDip)
        {
            MaxTextWidth = maxTextWidth,
            Trimming = TextTrimming.CharacterEllipsis,
        };
    }
}
