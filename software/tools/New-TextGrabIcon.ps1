# Rebuild the multi-resolution Windows icon from the same vector geometry as Assets/TextGrab.svg.
# No fonts, external renderers, or network calls are needed.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../TextGrab/Assets'))
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null

function New-IconFrame([int]$size) {
    $canvasSize = $size * 4
    $canvas = [System.Drawing.Bitmap]::new($canvasSize, $canvasSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($canvasSize / 64.0, $canvasSize / 64.0)
    $tile = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $tile.AddArc(2, 2, 28, 28, 180, 90)
    $tile.AddArc(34, 2, 28, 28, 270, 90)
    $tile.AddArc(34, 34, 28, 28, 0, 90)
    $tile.AddArc(2, 34, 28, 28, 90, 90)
    $tile.CloseFigure()
    $gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, 2), [System.Drawing.PointF]::new(0, 62),
        [System.Drawing.ColorTranslator]::FromHtml('#20395F'), [System.Drawing.ColorTranslator]::FromHtml('#0D1933'))
    $graphics.FillPath($gradient, $tile)
    $mint = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#5EEAD4'), 4)
    $amber = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#FFBD59'), 4)
    foreach ($pen in @($mint, $amber)) {
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    }
    $graphics.DrawLines($mint, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(23,13),[System.Drawing.PointF]::new(14,13),[System.Drawing.PointF]::new(14,23)))
    $graphics.DrawLines($mint, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(14,41),[System.Drawing.PointF]::new(14,51),[System.Drawing.PointF]::new(23,51)))
    $graphics.DrawLines($mint, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(41,51),[System.Drawing.PointF]::new(51,51),[System.Drawing.PointF]::new(51,41)))
    $graphics.DrawLines($amber, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(41,13),[System.Drawing.PointF]::new(51,13),[System.Drawing.PointF]::new(51,23)))
    $ink = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F5FAFF'))
    $graphics.FillPolygon($ink, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(22,23),[System.Drawing.PointF]::new(43,23),
        [System.Drawing.PointF]::new(43,29),[System.Drawing.PointF]::new(35.5,29),
        [System.Drawing.PointF]::new(35.5,45),[System.Drawing.PointF]::new(29.5,45),
        [System.Drawing.PointF]::new(29.5,29),[System.Drawing.PointF]::new(22,29)))
    $graphics.Dispose()
    foreach ($resource in @($tile,$gradient,$mint,$amber,$ink)) { $resource.Dispose() }
    $frame = [System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $scaled = [System.Drawing.Graphics]::FromImage($frame)
    $scaled.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $scaled.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $scaled.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $scaled.DrawImage($canvas, [System.Drawing.Rectangle]::new(0,0,$size,$size))
    $scaled.Dispose()
    $canvas.Dispose()
    return $frame
}

$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = [System.Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $frame = New-IconFrame $size
    if ($size -eq 256) { $frame.Save((Join-Path $assetDirectory 'TextGrab-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $stream = [System.IO.MemoryStream]::new()
    $frame.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
    $frames.Add($stream.ToArray())
    $stream.Dispose()
    $frame.Dispose()
}
$output = [System.IO.File]::Create((Join-Path $assetDirectory 'TextGrab.ico'))
$writer = [System.IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($bytes in $frames) { $writer.Write([byte[]]$bytes) }
} finally { $writer.Dispose(); $output.Dispose() }
Write-Output "Created TextGrab.ico with sizes $($sizes -join ', ') in $assetDirectory"
