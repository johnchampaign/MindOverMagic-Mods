# Draws icon.png for each official-mod port in mods/ (256x256, the Mods screen draws up to 96).
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/make_icons.ps1

param([string]$Repo = (Split-Path -Parent $PSScriptRoot))

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
$Size = 256
$Glyph = [System.Drawing.Color]::FromArgb(250, 246, 236)

function New-Canvas([System.Drawing.Color]$top, [System.Drawing.Color]$bottom) {
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)
    $path = Rounded 4 4 ($Size - 8) ($Size - 8) 44
    $rect = New-Object System.Drawing.RectangleF 0, 0, $Size, $Size
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $top, $bottom, 90
    $g.FillPath($brush, $path)
    return @($bmp, $g, $path)
}

function Rounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Pt([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }

function Save($bmp, $g, [string]$modId) {
    $g.Dispose()
    $out = Join-Path $Repo "mods\$modId\icon.png"
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "wrote $out"
}

$fill = New-Object System.Drawing.SolidBrush $Glyph
function Pen([float]$w) {
    $p = New-Object System.Drawing.Pen $Glyph, $w
    $p.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $p.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $p.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $p
}

# Faction Balance: balance scales.
$bmp, $g, $null = New-Canvas ([System.Drawing.Color]::FromArgb(44, 120, 128)) ([System.Drawing.Color]::FromArgb(18, 58, 70))
$pen = Pen 12
$g.DrawLine($pen, 128, 56, 128, 196)            # post
$g.DrawLine($pen, 60, 84, 196, 84)              # beam
$g.FillEllipse($fill, 116, 44, 24, 24)          # finial
$g.DrawLine($pen, 90, 204, 166, 204)            # base
$thin = Pen 6
foreach ($cx in 70, 186) {
    $g.DrawLine($thin, $cx, 84, $cx - 26, 146)
    $g.DrawLine($thin, $cx, 84, $cx + 26, 146)
    $g.FillPie($fill, $cx - 34, 118, 68, 56, 0, 180)  # pan
}
Save $bmp $g "johnc.factionbalance"

# Archmage Progression: a star above a rank chevron.
$bmp, $g, $null = New-Canvas ([System.Drawing.Color]::FromArgb(70, 64, 160)) ([System.Drawing.Color]::FromArgb(26, 22, 74))
$star = @()
for ($i = 0; $i -lt 10; $i++) {
    $r = if ($i % 2 -eq 0) { 64 } else { 26 }
    $a = -[Math]::PI / 2 + $i * [Math]::PI / 5
    $star += Pt (128 + $r * [Math]::Cos($a)) (100 + $r * [Math]::Sin($a))
}
$g.FillPolygon($fill, [System.Drawing.PointF[]]$star)
$chev = Pen 18
$g.DrawLines($chev, [System.Drawing.PointF[]]@((Pt 66 172), (Pt 128 208), (Pt 190 172)))
Save $bmp $g "johnc.archmageprogression"

# Character Level Relics: a cut gem with a rising arrow above it.
$bmp, $g, $null = New-Canvas ([System.Drawing.Color]::FromArgb(206, 146, 52)) ([System.Drawing.Color]::FromArgb(112, 64, 18))
$gem = [System.Drawing.PointF[]]@((Pt 66 134), (Pt 94 104), (Pt 162 104), (Pt 190 134), (Pt 128 214))
$g.FillPolygon($fill, $gem)
$facet = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(160, 104, 34)), 5
$g.DrawLine($facet, 66, 134, 190, 134)
$g.DrawLine($facet, 112, 104, 104, 134)
$g.DrawLine($facet, 144, 104, 152, 134)
$up = Pen 14
$g.DrawLine($up, 128, 84, 128, 40)
$g.DrawLines($up, [System.Drawing.PointF[]]@((Pt 104 62), (Pt 128 38), (Pt 152 62)))
Save $bmp $g "johnc.characterlevelrelics"

# Sacrificial Altar: the altar artwork on a dark crimson tile.
$bmp, $g, $path = New-Canvas ([System.Drawing.Color]::FromArgb(92, 18, 30)) ([System.Drawing.Color]::FromArgb(28, 6, 12))
$art = [System.Drawing.Image]::FromFile((Join-Path $Repo "src\SacrificialAltar\Assets\sacrificial-altar.png"))
$g.SetClip($path)
$g.DrawImage($art, (New-Object System.Drawing.Rectangle 6, 10, 244, 244))
$art.Dispose()
Save $bmp $g "johnc.sacrificialaltar"
