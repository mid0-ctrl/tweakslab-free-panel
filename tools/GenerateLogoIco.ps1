Add-Type -AssemblyName System.Drawing

function Write-PngIcon {
    param(
        [string]$PngPath,
        [string]$IcoPath,
        [int[]]$Sizes = @(16, 24, 32, 48, 64, 128, 256)
    )

    $source = [System.Drawing.Bitmap]::FromFile($PngPath)
    try {
        $pngImages = New-Object System.Collections.Generic.List[byte[]]

        foreach ($size in $Sizes) {
            $frame = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($frame)
            try {
                $graphics.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

                $scale = [Math]::Min($size / $source.Width, $size / $source.Height) * 0.92
                $width = [int][Math]::Round($source.Width * $scale)
                $height = [int][Math]::Round($source.Height * $scale)
                $x = [int][Math]::Round(($size - $width) / 2.0)
                $y = [int][Math]::Round(($size - $height) / 2.0)
                $graphics.DrawImage($source, $x, $y, $width, $height)
            }
            finally {
                $graphics.Dispose()
            }

            $stream = New-Object System.IO.MemoryStream
            try {
                $frame.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $pngImages.Add($stream.ToArray())
            }
            finally {
                $stream.Dispose()
                $frame.Dispose()
            }
        }

        $stream = [System.IO.File]::Create($IcoPath)
        try {
            $writer = New-Object System.IO.BinaryWriter($stream)
            $writer.Write([UInt16]0) # reserved
            $writer.Write([UInt16]1) # type: icon
            $writer.Write([UInt16]$pngImages.Count)

            $offset = 6 + (16 * $pngImages.Count)
            $index = 0
            foreach ($size in $Sizes) {
                $png = $pngImages[$index]
                $widthByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }
                $heightByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }
                $writer.Write($widthByte)
                $writer.Write($heightByte)
                $writer.Write([byte]0)      # colors
                $writer.Write([byte]0)      # reserved
                $writer.Write([UInt16]1)    # planes
                $writer.Write([UInt16]32)   # bit count
                $writer.Write([UInt32]$png.Length)
                $writer.Write([UInt32]$offset)
                $offset += $png.Length
                $index++
            }

            foreach ($png in $pngImages) {
                $writer.Write($png)
            }

            $writer.Flush()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

$root = Split-Path -Parent $PSScriptRoot
$detour = Join-Path $root 'tools\DetourLogos\DetourLogos.csproj'
if (Test-Path $detour) {
    dotnet run --project $detour -c Release -- --prepare-logo | Out-Host
} else {
    $png = Join-Path $root 'TweaksLabPanel\Assets\logo.png'
    $ico = Join-Path $root 'TweaksLabPanel\Assets\logo.ico'
    $binIco = Join-Path $root 'TweaksLabPanel\bin\Debug\net8.0-windows\Assets\logo.ico'
    Write-PngIcon -PngPath $png -IcoPath $ico
    if (Test-Path (Split-Path $binIco -Parent)) {
        Copy-Item $ico $binIco -Force
    }
    Write-Output "ICO regenere: $ico"
}
