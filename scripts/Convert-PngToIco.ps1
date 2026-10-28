[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing.Common

$resolvedInputPath = (Resolve-Path -LiteralPath $InputPath).Path
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutputPath)

if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}

$iconSizes = @(16, 24, 32, 48, 64, 128, 256)
$imageStreams = [System.Collections.Generic.List[System.IO.MemoryStream]]::new()
$sourceImage = $null

try {
    $sourceImage = [System.Drawing.Image]::FromFile($resolvedInputPath)

    foreach ($iconSize in $iconSizes) {
        $bitmap = [System.Drawing.Bitmap]::new(
            $iconSize,
            $iconSize,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)

            $scale = [Math]::Min(
                $iconSize / $sourceImage.Width,
                $iconSize / $sourceImage.Height)
            $drawWidth = [Math]::Max(1, [int][Math]::Round($sourceImage.Width * $scale))
            $drawHeight = [Math]::Max(1, [int][Math]::Round($sourceImage.Height * $scale))
            $drawLeft = [int][Math]::Floor(($iconSize - $drawWidth) / 2)
            $drawTop = [int][Math]::Floor(($iconSize - $drawHeight) / 2)

            $graphics.DrawImage(
                $sourceImage,
                [System.Drawing.Rectangle]::new($drawLeft, $drawTop, $drawWidth, $drawHeight))

            $imageStream = [System.IO.MemoryStream]::new()
            $bitmap.Save($imageStream, [System.Drawing.Imaging.ImageFormat]::Png)
            $imageStream.Position = 0
            $imageStreams.Add($imageStream)
        }
        finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }

    $outputStream = [System.IO.File]::Create($resolvedOutputPath)
    $writer = [System.IO.BinaryWriter]::new($outputStream)

    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$imageStreams.Count)

        $imageOffset = 6 + (16 * $imageStreams.Count)
        for ($index = 0; $index -lt $imageStreams.Count; $index++) {
            $iconSize = $iconSizes[$index]
            $dimensionByte = if ($iconSize -eq 256) { 0 } else { $iconSize }
            $imageStream = $imageStreams[$index]

            $writer.Write([byte]$dimensionByte)
            $writer.Write([byte]$dimensionByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$imageStream.Length)
            $writer.Write([uint32]$imageOffset)

            $imageOffset += [int]$imageStream.Length
        }

        foreach ($imageStream in $imageStreams) {
            $imageStream.WriteTo($outputStream)
        }
    }
    finally {
        $writer.Dispose()
        $outputStream.Dispose()
    }
}
finally {
    foreach ($imageStream in $imageStreams) {
        $imageStream.Dispose()
    }

    if ($null -ne $sourceImage) {
        $sourceImage.Dispose()
    }
}
