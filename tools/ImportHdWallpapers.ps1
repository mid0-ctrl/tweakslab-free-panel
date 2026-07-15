# Importe les 5 fonds gaming en haute qualité (1920x1080) dans le panel.
# Usage :
#   1. Place tes images sources dans  imports\hd\  (voir noms ci-dessous)
#   2. Lance :  .\tools\ImportHdWallpapers.ps1
#
# Noms dans le dossier source (ex. Desktop\WALLPAPER ART) :
#   BAAAALANCOIRE.png -> balancoire
#   GTA.png           -> gta5
#   FORTNITE.png      -> fortnite
#   GOOD1.png         -> neon
#   GEKKOVALO.png     -> valorant (wallpaper_9)
#   MONTAGNE.png      -> montagne (wallpaper_10)
#   JAPAN.png         -> japon (wallpaper_11)
#
# Ou noms simples dans imports\hd\ :
#   balancoire.png | gta5.png | fortnite.png | neon.png | klombo.png
# (jpg/jpeg/webp acceptés aussi)

param(
    [string]$SourceDir = (Join-Path $PSScriptRoot "..\imports\hd"),
    [int]$TargetWidth = 1920,
    [int]$TargetHeight = 1080
)

Add-Type -AssemblyName System.Drawing

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$bgDir = Join-Path $root "TweaksLabPanel\Assets\Backgrounds"
$wpDir = Join-Path $root "TweaksLabPanel\Assets\Wallpapers"

$map = [ordered]@{
    balancoire = @{ Bg = "bg-balancoire.png"; Wp = "wallpaper_5.png" }
    gta5       = @{ Bg = "bg-gta5.png";       Wp = "wallpaper_6.png" }
    fortnite   = @{ Bg = "bg-fortnite-bus.png"; Wp = "wallpaper_7.png" }
    neon       = @{ Bg = "bg-neon-wave.png";  Wp = "wallpaper_8.png" }
    klombo     = @{ Bg = "bg-klombo.png";     Wp = "wallpaper_9.png" }
}

function Find-SourceFile([string]$BaseName) {
    $exts = @(".png", ".jpg", ".jpeg", ".webp", ".PNG", ".JPG", ".JPEG", ".WEBP")
    foreach ($ext in $exts) {
        $path = Join-Path $SourceDir ($BaseName + $ext)
        if (Test-Path $path) { return $path }
    }
    return $null
}

function Export-1920x1080([string]$SourcePath, [string]$DestPath) {
    $src = [System.Drawing.Image]::FromFile($SourcePath)
    try {
        $srcW = $src.Width
        $srcH = $src.Height
        if ($srcW -lt 1600) {
            Write-Warning "  Source faible résolution (${srcW}x${srcH}) — fournis une image >= 1920x1080 pour une vraie qualité HD."
        }

        $targetRatio = $TargetWidth / $TargetHeight
        $srcRatio = $srcW / $srcH

        $cropW = $srcW
        $cropH = $srcH
        $cropX = 0
        $cropY = 0

        if ($srcRatio -gt $targetRatio) {
            $cropW = [int]($srcH * $targetRatio)
            $cropX = [int](($srcW - $cropW) / 2)
        }
        elseif ($srcRatio -lt $targetRatio) {
            $cropH = [int]($srcW / $targetRatio)
            $cropY = [int](($srcH - $cropH) / 2)
        }

        $dest = New-Object System.Drawing.Bitmap $TargetWidth, $TargetHeight
        $gfx = [System.Drawing.Graphics]::FromImage($dest)
        try {
            $gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $gfx.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $gfx.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $srcRect = New-Object System.Drawing.Rectangle $cropX, $cropY, $cropW, $cropH
            $destRect = New-Object System.Drawing.Rectangle 0, 0, $TargetWidth, $TargetHeight
            $gfx.DrawImage($src, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally { $gfx.Dispose() }

        $dest.Save($DestPath, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "  -> $DestPath (${TargetWidth}x${TargetHeight})" -ForegroundColor Green
    }
    finally {
        $src.Dispose()
    }
}

if (-not (Test-Path $SourceDir)) {
    New-Item -ItemType Directory -Path $SourceDir -Force | Out-Null
    Write-Host "Dossier créé : $SourceDir"
    Write-Host "Dépose-y tes 5 images sources (balancoire, gta5, fortnite, neon, klombo) puis relance ce script."
    exit 0
}

Write-Host "Import HD depuis : $SourceDir`n"

$ok = 0
foreach ($key in $map.Keys) {
    $src = Find-SourceFile $key
    if (-not $src) {
        Write-Warning "Manquant : $key (png/jpg/webp)"
        continue
    }

    $info = $map[$key]
    Write-Host "Traitement $key ..."
    Export-1920x1080 $src (Join-Path $bgDir $info.Bg)
    Export-1920x1080 $src (Join-Path $wpDir $info.Wp)
    $ok++
}

Write-Host "`nTerminé : $ok / $($map.Count) fonds importés."
if ($ok -lt $map.Count) {
    Write-Host "Fichiers attendus dans imports\hd\ : balancoire, gta5, fortnite, neon, klombo"
}
