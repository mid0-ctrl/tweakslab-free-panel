using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TweaksLabPanel.Helpers;

public enum SocialIconStyle
{
    DarkGlyph,
    LightGlyph,
    Raw,
}

public static class SocialIconHelper
{
    public static UIElement? CreateIcon(string fileName, SocialIconStyle style, double size = 16)
    {
        var path = AssetHelper.Social(fileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var bitmap = LoadBitmap(path);
            var source = style == SocialIconStyle.Raw
                ? bitmap
                : CreateWhiteIconBitmap(bitmap, style);

            source.Freeze();

            var image = new Image
            {
                Source = source,
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        return bitmap;
    }

    private static BitmapSource CreateWhiteIconBitmap(BitmapSource source, SocialIconStyle style)
    {
        if (source.Format != PixelFormats.Bgra32)
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var w = source.PixelWidth;
        var h = source.PixelHeight;
        var stride = w * 4;
        var pixels = new byte[h * stride];
        source.CopyPixels(pixels, stride, 0);

        var (bgR, bgG, bgB, bgCount) = EstimateOpaqueBackground(pixels, w, h);
        var lightBgMask = style == SocialIconStyle.LightGlyph
            ? MarkLightGlyphBackground(pixels, w, h)
            : null;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                var b = pixels[i];
                var g = pixels[i + 1];
                var r = pixels[i + 2];
                var a = pixels[i + 3];
                if (a == 0)
                {
                    pixels[i + 3] = 0;
                    continue;
                }

                var lum = (r * 77 + g * 150 + b * 29) >> 8;
                var glyph = style switch
                {
                    SocialIconStyle.LightGlyph => lum > 185 ? lum * a / 255 : 0,
                    SocialIconStyle.DarkGlyph => (255 - lum) * a / 255,
                    _ => 0,
                };

                if (lightBgMask is not null && lightBgMask[y * w + x])
                    glyph = 0;

                if (bgCount > 0 && style == SocialIconStyle.DarkGlyph)
                {
                    var dist = Math.Abs(r - bgR) + Math.Abs(g - bgG) + Math.Abs(b - bgB);
                    if (dist < 28)
                        glyph = 0;
                    else if (dist < 56)
                        glyph = glyph * (dist - 28) / 28;
                }

                pixels[i] = 255;
                pixels[i + 1] = 255;
                pixels[i + 2] = 255;
                pixels[i + 3] = (byte)Math.Clamp(glyph, 0, 255);
            }
        }

        return BitmapSource.Create(w, h, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
    }

    /// <summary>
    /// Removes outer white frames and dark halos (e.g. TikTok square + black circle),
    /// keeping only isolated bright glyphs such as the central note.
    /// </summary>
    private static bool[] MarkLightGlyphBackground(byte[] pixels, int w, int h)
    {
        var bg = new bool[w * h];
        var queue = new Queue<int>();

        static int Luminance(byte b, byte g, byte r) => (r * 77 + g * 150 + b * 29) >> 8;

        bool IsEdgeWhite(int idx)
        {
            var pi = idx * 4;
            if (pixels[pi + 3] < 128)
                return true;

            return Luminance(pixels[pi], pixels[pi + 1], pixels[pi + 2]) > 200;
        }

        void TryEnqueue(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
                return;

            var idx = y * w + x;
            if (bg[idx] || !IsEdgeWhite(idx))
                return;

            bg[idx] = true;
            queue.Enqueue(idx);
        }

        for (var x = 0; x < w; x++)
        {
            TryEnqueue(x, 0);
            TryEnqueue(x, h - 1);
        }

        for (var y = 0; y < h; y++)
        {
            TryEnqueue(0, y);
            TryEnqueue(w - 1, y);
        }

        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();
            var x = idx % w;
            var y = idx / w;
            TryEnqueue(x - 1, y);
            TryEnqueue(x + 1, y);
            TryEnqueue(x, y - 1);
            TryEnqueue(x, y + 1);
        }

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var idx = y * w + x;
                if (bg[idx])
                    continue;

                var pi = idx * 4;
                if (pixels[pi + 3] < 128)
                {
                    bg[idx] = true;
                    continue;
                }

                if (Luminance(pixels[pi], pixels[pi + 1], pixels[pi + 2]) < 60)
                    bg[idx] = true;
            }
        }

        return bg;
    }

    private static (int r, int g, int b, int count) EstimateOpaqueBackground(byte[] pixels, int w, int h)
    {
        var margin = Math.Max(1, Math.Min(w, h) / 10);
        long sr = 0, sg = 0, sb = 0;
        var count = 0;

        void Sample(int x, int y)
        {
            var i = (y * w + x) * 4;
            var a = pixels[i + 3];
            if (a < 200) return;
            sr += pixels[i + 2];
            sg += pixels[i + 1];
            sb += pixels[i];
            count++;
        }

        for (var x = 0; x < margin; x++)
        {
            Sample(x, margin);
            Sample(w - 1 - x, margin);
            Sample(x, h - 1 - margin);
            Sample(w - 1 - x, h - 1 - margin);
        }

        if (count == 0) return (0, 0, 0, 0);
        return ((int)(sr / count), (int)(sg / count), (int)(sb / count), count);
    }
}
