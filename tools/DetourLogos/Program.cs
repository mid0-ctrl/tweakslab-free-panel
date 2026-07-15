using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

if (args.Length > 0 && args[0] == "--prepare-logo")
{
    PrepareLogo();
    return 0;
}

if (args.Length > 0 && args[0] == "--prepare-nvidia")
{
    PrepareNvidiaLogo();
    return 0;
}

const int BlackThreshold = 42;
const int WhiteThreshold = 245;

if (args.Length == 1 && args[0] == "--inspect-logo")
{
    InspectFiles(@"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\Assets\logo.png");
    return 0;
}

if (args.Length == 1 && args[0] == "--inspect")
{
    InspectFiles(
        @"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\Assets\gpu\nvidia.png",
        @"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\Assets\gpu\amd.png");
    return 0;
}

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DetourLogos <input> <output> [more pairs...]");
    return 1;
}

if (args.Length % 2 != 0)
{
    Console.Error.WriteLine("Provide input/output path pairs.");
    return 1;
}

for (var i = 0; i < args.Length; i += 2)
{
    var input = Path.GetFullPath(args[i]);
    var output = Path.GetFullPath(args[i + 1]);

    if (!File.Exists(input))
    {
        Console.Error.WriteLine($"Missing input: {input}");
        return 1;
    }

    using var image = Image.Load<Rgba32>(input);
    RemoveEdgeConnectedBlackBackground(image);
    GrowBlackFromWhiteBorders(image);
    RemoveWhiteBackground(image);
    CropToContent(image);

    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    image.SaveAsPng(output);
    Console.WriteLine($"Saved {output} ({image.Width}x{image.Height})");
}

return 0;

static void PrepareLogo()
{
    var downloadsHq = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "TweaksLab_logo_L_navy@2x.png");

    const string logoPath = @"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\Assets\logo.png";
    const string icoPath = @"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\Assets\logo.ico";
    const string binIcoPath = @"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\bin\Debug\net8.0-windows\Assets\logo.ico";
    const string binPngPath = @"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\bin\Debug\net8.0-windows\Assets\logo.png";

    var sourcePath = File.Exists(downloadsHq) ? downloadsHq : logoPath;
    Console.WriteLine($"Source logo: {sourcePath}");

    using var image = Image.Load<Rgba32>(sourcePath);
    RemoveEdgeConnectedBlackBackground(image);
    GrowBlackFromWhiteBorders(image);
    CropToContent(image);
    AddTransparentPadding(image, 0.06f);
    image.SaveAsPng(logoPath);
    Console.WriteLine($"Saved detoured PNG {logoPath} ({image.Width}x{image.Height})");

    WritePngIcon(image, icoPath);
    if (Directory.Exists(Path.GetDirectoryName(binIcoPath)))
    {
        File.Copy(icoPath, binIcoPath, true);
        File.Copy(logoPath, binPngPath, true);
        Console.WriteLine($"Copied ICO to {binIcoPath}");
    }
}

static void RemoveWhiteBackground(Image<Rgba32> image)
{
    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                if (pixel.A == 0)
                    continue;

                if (pixel.R >= WhiteThreshold && pixel.G >= WhiteThreshold && pixel.B >= WhiteThreshold)
                {
                    pixel.A = 0;
                    continue;
                }

                var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                if (min >= WhiteThreshold - 20 && max >= WhiteThreshold)
                {
                    var lightness = (max - (WhiteThreshold - 20)) / 20f;
                    pixel.A = (byte)Math.Clamp(pixel.A * (1f - lightness), 0, 255);
                }
            }
        }
    });
}

static void WritePngIcon(Image<Rgba32> source, string icoPath, int[]? sizes = null)
{
    sizes ??= [16, 24, 32, 48, 64, 128, 256];
    var pngFrames = new List<byte[]>();

    foreach (var size in sizes)
    {
        using var canvasImage = new Image<Rgba32>(size, size, new Rgba32(0, 0, 0, 0));
        var scale = Math.Min(size / (float)source.Width, size / (float)source.Height) * 0.92f;
        var drawW = Math.Max(1, (int)Math.Round(source.Width * scale));
        var drawH = Math.Max(1, (int)Math.Round(source.Height * scale));
        var offsetX = (size - drawW) / 2;
        var offsetY = (size - drawH) / 2;
        using var resizedSource = source.Clone(r => r.Resize(drawW, drawH, KnownResamplers.Lanczos3));
        canvasImage.Mutate(c => c.DrawImage(resizedSource, new Point(offsetX, offsetY), 1f));

        using var ms = new MemoryStream();
        canvasImage.SaveAsPng(ms);
        pngFrames.Add(ms.ToArray());
    }

    using var stream = File.Create(icoPath);
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)pngFrames.Count);

    var offset = 6 + 16 * pngFrames.Count;
    for (var i = 0; i < sizes.Length; i++)
    {
        var size = sizes[i];
        var png = pngFrames[i];
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)png.Length);
        writer.Write((uint)offset);
        offset += png.Length;
    }

    foreach (var png in pngFrames)
        writer.Write(png);
}

static void PrepareNvidiaLogo()
{
    var downloadsPng = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "logooonvidiagood.png");
    var downloadsJpg = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "20336426-nvidia-logo-vecteur-nvidia-icone-gratuit-vecteur-gratuit-vectoriel.jpg");

    const string amdPath = @"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\Assets\gpu\amd.png";
    const string output = @"C:\Users\TweaksLab User\Projects\tweakslab-panel-neuf\TweaksLabPanel\Assets\gpu\nvidia.png";

    using var amd = Image.Load<Rgba32>(amdPath);
    using var input = File.Exists(downloadsPng)
        ? Image.Load<Rgba32>(downloadsPng)
        : File.Exists(downloadsJpg)
            ? Image.Load<Rgba32>(downloadsJpg)
            : Image.Load<Rgba32>(output);

    if (File.Exists(downloadsPng))
        ExtractWhiteLogoFromGoodPng(input);
    else if (File.Exists(downloadsJpg))
    {
        RemoveNeutralBackdrop(input);
        ExtractWhiteLogoFromDarkBackground(input);
    }
    else
        ConvertDarkLogoToWhiteTransparent(input);

    CropToContent(input);

    const int padding = 8;
    var targetWidth = amd.Width;
    var targetHeight = amd.Height;
    var innerWidth = targetWidth - padding * 2;
    var innerHeight = targetHeight - padding * 2;
    var amdBounds = GetOpaqueBounds(amd);

    var scale = amdBounds.Height / (float)input.Height;
    var drawWidth = Math.Max(1, (int)Math.Round(input.Width * scale));
    var drawHeight = Math.Max(1, (int)Math.Round(input.Height * scale));

    if (drawWidth > innerWidth || drawHeight > innerHeight)
    {
        scale = Math.Min(innerWidth / (float)input.Width, innerHeight / (float)input.Height);
        drawWidth = Math.Max(1, (int)Math.Round(input.Width * scale));
        drawHeight = Math.Max(1, (int)Math.Round(input.Height * scale));
    }

    var offsetX = (targetWidth - drawWidth) / 2;
    var offsetY = (targetHeight - drawHeight) / 2;

    using var resized = input.Clone(ctx => ctx.Resize(drawWidth, drawHeight));
    using var canvas = new Image<Rgba32>(targetWidth, targetHeight, new Rgba32(0, 0, 0, 0));
    canvas.Mutate(ctx => ctx.DrawImage(resized, new Point(offsetX, offsetY), 1f));

    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    canvas.SaveAsPng(output);
    Console.WriteLine($"Saved {output} ({targetWidth}x{targetHeight}) — draw {drawWidth}x{drawHeight}");
}

static void ExtractWhiteLogoFromGoodPng(Image<Rgba32> image)
{
    RemoveNeutralBackdrop(image);
    RemoveEdgeConnectedBlackBackground(image);

    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                if (pixel.A < 16)
                    continue;

                var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var luminance = (pixel.R + pixel.G + pixel.B) / 3;
                var isWhite = luminance >= 165 && max - min <= 40;

                pixel = isWhite
                    ? new Rgba32(255, 255, 255, 255)
                    : new Rgba32(0, 0, 0, 0);
            }
        }
    });
}

static Rectangle GetOpaqueBounds(Image<Rgba32> image)
{
    var minX = image.Width;
    var minY = image.Height;
    var maxX = 0;
    var maxY = 0;

    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                if (row[x].A < 16)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }
    });

    if (maxX < minX || maxY < minY)
        return new Rectangle(0, 0, image.Width, image.Height);

    return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
}

static void RemoveNeutralBackdrop(Image<Rgba32> image)
{
    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                if (pixel.A < 16)
                    continue;

                var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var isNeutral = max - min <= 24;
                var luminance = (pixel.R + pixel.G + pixel.B) / 3;

                if (isNeutral && luminance is >= 45 and <= 200)
                    pixel = new Rgba32(0, 0, 0, 0);
            }
        }
    });
}

static void ExtractWhiteLogoFromDarkBackground(Image<Rgba32> image)
{
    var splitY = FindIconTextSplitY(image);
    var midX = FindIconSplitX(image, splitY);
    RemoveEdgeConnectedBlackBackground(image);

    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                if (pixel.A < 16)
                    continue;

                var luminance = (pixel.R + pixel.G + pixel.B) / 3;
                var isBright = luminance >= 120;
                var keepBright = y >= splitY || x < midX;

                if (keepBright)
                    pixel = isBright
                        ? new Rgba32(255, 255, 255, 255)
                        : new Rgba32(0, 0, 0, 0);
                else
                    pixel = isBright
                        ? new Rgba32(0, 0, 0, 0)
                        : new Rgba32(255, 255, 255, 255);
            }
        }
    });

    RemoveWhiteConnectedToTransparentInRegion(image, midX, image.Width, splitY);
}

static void RemoveWhiteConnectedToTransparentInRegion(
    Image<Rgba32> image,
    int minX,
    int maxX,
    int maxY)
{
    var width = image.Width;
    var visited = new bool[width * image.Height];
    var queue = new Queue<(int X, int Y)>();

    static bool IsTransparent(Rgba32 pixel) => pixel.A < 16;
    static bool IsWhiteish(Rgba32 pixel) =>
        pixel.A > 0 && pixel.R >= 170 && pixel.G >= 170 && pixel.B >= 170;

    void TryEnqueue(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= image.Height)
            return;
        if (x < minX || x >= maxX || y >= maxY)
            return;

        var index = y * width + x;
        if (visited[index])
            return;

        var pixel = image[x, y];
        if (!IsTransparent(pixel) && !IsWhiteish(pixel))
            return;

        visited[index] = true;
        queue.Enqueue((x, y));
    }

    for (var x = minX; x < maxX; x++)
    {
        TryEnqueue(x, 0);
        TryEnqueue(x, maxY - 1);
    }

    for (var y = 0; y < maxY; y++)
    {
        TryEnqueue(minX, y);
        TryEnqueue(maxX - 1, y);
    }

    while (queue.Count > 0)
    {
        var (x, y) = queue.Dequeue();
        TryEnqueue(x - 1, y);
        TryEnqueue(x + 1, y);
        TryEnqueue(x, y - 1);
        TryEnqueue(x, y + 1);
    }

    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < maxY; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = minX; x < maxX && x < row.Length; x++)
            {
                var index = y * width + x;
                if (visited[index] && IsWhiteish(row[x]))
                    row[x].A = 0;
            }
        }
    });
}

static int FindIconTextSplitY(Image<Rgba32> image)
{
    var scanStart = (int)(image.Height * 0.45);
    var scanEnd = (int)(image.Height * 0.8);

    for (var y = scanStart; y < scanEnd; y++)
    {
        var darkCount = 0;
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            if (pixel.A > 0 && Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) <= 40)
                darkCount++;
        }

        if (darkCount >= image.Width * 0.7)
            return y;
    }

    return (int)(image.Height * 0.62);
}

static int FindIconSplitX(Image<Rgba32> image, int iconMaxY)
{
    var start = (int)(image.Width * 0.42);
    var end = (int)(image.Width * 0.62);
    var bestX = image.Width / 2;
    var bestDark = -1;

    for (var x = start; x < end; x++)
    {
        var darkCount = 0;
        for (var y = 0; y < iconMaxY; y++)
        {
            var pixel = image[x, y];
            if (pixel.A > 0 && Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) <= 60)
                darkCount++;
        }

        if (darkCount > bestDark)
        {
            bestDark = darkCount;
            bestX = x;
        }
    }

    return bestX;
}

static void ConvertDarkLogoToWhiteTransparent(Image<Rgba32> image)
{
    RemoveWhiteBackground(image);
    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                if (pixel.A < 16)
                    continue;

                pixel = new Rgba32(255, 255, 255, 255);
            }
        }
    });
}

static void RemoveEdgeConnectedBlackBackground(Image<Rgba32> image)
{
    var width = image.Width;
    var height = image.Height;
    var clear = new bool[width * height];
    var queue = new Queue<(int X, int Y)>();

    static bool IsBackdrop(Rgba32 pixel, int threshold) =>
        pixel.A > 0 && Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) <= threshold;

    void TryEnqueue(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return;

        var index = y * width + x;
        if (clear[index])
            return;

        if (!IsBackdrop(image[x, y], BlackThreshold))
            return;

        clear[index] = true;
        queue.Enqueue((x, y));
    }

    for (var x = 0; x < width; x++)
    {
        TryEnqueue(x, 0);
        TryEnqueue(x, height - 1);
    }

    for (var y = 0; y < height; y++)
    {
        TryEnqueue(0, y);
        TryEnqueue(width - 1, y);
    }

    while (queue.Count > 0)
    {
        var (x, y) = queue.Dequeue();
        TryEnqueue(x - 1, y);
        TryEnqueue(x + 1, y);
        TryEnqueue(x, y - 1);
        TryEnqueue(x, y + 1);
    }

    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < width; x++)
            {
                if (clear[y * width + x])
                    row[x].A = 0;
            }
        }
    });
}

static void GrowBlackFromWhiteBorders(Image<Rgba32> image, int passes = 64)
{
    static bool IsTransparent(Rgba32 pixel) => pixel.A < 16;
    static bool IsWhiteish(Rgba32 pixel) =>
        pixel.A > 0 && pixel.R >= WhiteThreshold - 20 && pixel.G >= WhiteThreshold - 20 && pixel.B >= WhiteThreshold - 20;

    Span<(int Dx, int Dy)> neighbors = stackalloc (int, int)[] { (-1, 0), (1, 0), (0, -1), (0, 1) };

    for (var pass = 0; pass < passes; pass++)
    {
        var changes = new List<(int X, int Y)>();

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (!IsTransparent(image[x, y]))
                    continue;

                var touchesWhite = false;
                foreach (var (dx, dy) in neighbors)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= image.Width || ny >= image.Height)
                        continue;

                    if (IsWhiteish(image[nx, ny]))
                    {
                        touchesWhite = true;
                        break;
                    }
                }

                if (touchesWhite)
                    changes.Add((x, y));
            }
        }

        if (changes.Count == 0)
            break;

        foreach (var (x, y) in changes)
            image[x, y] = new Rgba32(0, 0, 0, 255);
    }
}

static void AddTransparentPadding(Image<Rgba32> image, float ratio)
{
    var pad = Math.Max(4, (int)Math.Round(Math.Max(image.Width, image.Height) * ratio));
    image.Mutate(ctx => ctx.Pad(image.Width + pad * 2, image.Height + pad * 2, Color.Transparent));
}

static void InspectFiles(params string[] paths)
{
    foreach (var path in paths)
    {
        using var image = Image.Load<Rgba32>(path);
        var transparent = 0;
        var opaque = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (ref var pixel in row)
                {
                    if (pixel.A == 0) transparent++;
                    else opaque++;
                }
            }
        });

        var corner = image[0, 0];
        var center = image[image.Width / 2, image.Height / 2];
        var tArea = image[(int)(image.Width * 0.58), (int)(image.Height * 0.32)];
        var lArea = image[(int)(image.Width * 0.32), (int)(image.Height * 0.58)];

        var rightHalfMaxA = 0;
        var rightHalfBlack = 0;
        var rightHalfWhite = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = image.Width / 2; x < image.Width; x++)
            {
                var pixel = image[x, y];
                rightHalfMaxA = Math.Max(rightHalfMaxA, pixel.A);
                if (pixel.A > 200 && pixel.R < 40 && pixel.G < 40 && pixel.B < 40)
                    rightHalfBlack++;
                if (pixel.A > 200 && pixel.R >= WhiteThreshold - 20 && pixel.G >= WhiteThreshold - 20 && pixel.B >= WhiteThreshold - 20)
                    rightHalfWhite++;
            }
        }

        Console.WriteLine($"{Path.GetFileName(path)}: {image.Width}x{image.Height}, transparent={transparent}, opaque={opaque}, cornerA={corner.A}, centerA={center.A}, tAreaA={tArea.A}, lAreaA={lArea.A}, rightHalfMaxA={rightHalfMaxA}, rightHalfBlack={rightHalfBlack}, rightHalfWhite={rightHalfWhite}");
    }
}

static void CropToContent(Image<Rgba32> image)
{
    var minX = image.Width;
    var minY = image.Height;
    var maxX = 0;
    var maxY = 0;

    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                if (row[x].A == 0)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }
    });

    if (maxX < minX || maxY < minY)
        return;

    const int padding = 2;
    minX = Math.Max(0, minX - padding);
    minY = Math.Max(0, minY - padding);
    maxX = Math.Min(image.Width - 1, maxX + padding);
    maxY = Math.Min(image.Height - 1, maxY + padding);

    var cropRect = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    image.Mutate(ctx => ctx.Crop(cropRect));
}
