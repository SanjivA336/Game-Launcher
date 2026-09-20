<#
Regenerates the app's icon files from the SVG artwork in "Game Launcher\Resources\Branding".

    nexus.svg (has a background)  ->  nexus.ico       the .exe / taskbar / window icon (16 to 256 px in one file)
    nexusTransparent.svg          ->  nexus-logo.png  the logo drawn inside the app (256 px, transparent)

Why this exists: Windows can't use an SVG as an .exe icon (it needs an .ico), and WPF can't draw SVG directly.
The SVGs stay the source of truth; run this after changing them:

    powershell -ExecutionPolicy Bypass -File Tools\Generate-Icons.ps1

Needs Microsoft Edge (already part of Windows 11); it is only used as an SVG renderer.
#>
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$repo = Split-Path -Parent $PSScriptRoot
$brand = Join-Path $repo "Game Launcher\Resources\Branding"
$edge = @("C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", "C:\Program Files\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge was not found (it is used to render the SVGs)." }

$work = Join-Path ([IO.Path]::GetTempPath()) ("nexus-icons-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Force $work | Out-Null

# Renders an SVG to a 1000x1000 PNG with a transparent background (the SVGs are 1000x1000 to begin with)
function Render-Svg([string]$svgPath, [string]$outPng) {
    $uri = ([System.Uri]$svgPath).AbsoluteUri

    # Edge prints a status line ("... bytes written") to its error stream, which PowerShell would treat as a failure,
    # so its output is sent to throwaway files instead.
    $edgeArgs = @("--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000",
                  "--window-size=1000,1000", "--screenshot=`"$outPng`"", "`"$uri`"")
    Start-Process -FilePath $edge -ArgumentList $edgeArgs -Wait -NoNewWindow `
        -RedirectStandardOutput (Join-Path $work "edge-out.log") -RedirectStandardError (Join-Path $work "edge-err.log")
    for ($i = 0; $i -lt 30 -and -not (Test-Path $outPng); $i++) { Start-Sleep -Milliseconds 500 }
    if (-not (Test-Path $outPng)) { throw "Rendering $svgPath failed." }
    Start-Sleep -Milliseconds 300
}

# Scales an image down with high-quality filtering, keeping transparency
function Resize-Png([System.Drawing.Image]$source, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $wrap = New-Object System.Drawing.Imaging.ImageAttributes
    $wrap.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY) # stops a faint halo at the edges
    $g.DrawImage($source, (New-Object System.Drawing.Rectangle 0, 0, $size, $size), 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $wrap)
    $g.Dispose()
    return $bmp
}

function Get-PngBytes([System.Drawing.Image]$image) {
    $ms = New-Object System.IO.MemoryStream
    $image.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

try {
    # ---- the .ico: several sizes in one file, so Windows can pick the sharpest one for each place it shows the icon ----
    $iconPng = Join-Path $work "icon-1000.png"
    Render-Svg (Join-Path $brand "nexus.svg") $iconPng
    $master = [System.Drawing.Image]::FromFile($iconPng)

    $sizes = 16, 24, 32, 48, 64, 128, 256
    $frames = foreach ($size in $sizes) {
        $small = Resize-Png $master $size
        $bytes = Get-PngBytes $small
        $small.Dispose()
        [pscustomobject]@{ Size = $size; Bytes = $bytes }
    }
    $master.Dispose()

    $icoPath = Join-Path $brand "nexus.ico"
    $stream = [IO.File]::Create($icoPath)
    $w = New-Object System.IO.BinaryWriter $stream
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)   # ICO header: reserved, type 1 = icon, image count
    $offset = 6 + 16 * $frames.Count
    foreach ($f in $frames) {                                                    # one 16-byte directory entry per image
        $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }                       # 0 means 256 in this format
        $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)                                # 1 plane, 32 bits per pixel
        $w.Write([uint32]$f.Bytes.Length); $w.Write([uint32]$offset)
        $offset += $f.Bytes.Length
    }
    foreach ($f in $frames) { $w.Write($f.Bytes) }                               # the images themselves (PNG-compressed)
    $w.Dispose(); $stream.Dispose()
    "wrote nexus.ico ({0:N0} bytes, sizes: {1})" -f (Get-Item $icoPath).Length, ($sizes -join ", ")

    # ---- the in-app logo: transparent PNG ----
    $logoPng = Join-Path $work "logo-1000.png"
    Render-Svg (Join-Path $brand "nexusTransparent.svg") $logoPng
    $logoMaster = [System.Drawing.Image]::FromFile($logoPng)
    $logo = Resize-Png $logoMaster 256
    $logo.Save((Join-Path $brand "nexus-logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $logo.Dispose(); $logoMaster.Dispose()
    "wrote nexus-logo.png ({0:N0} bytes)" -f (Get-Item (Join-Path $brand "nexus-logo.png")).Length
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
