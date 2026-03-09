Add-Type -AssemblyName System.Drawing

$pngPath = "$PSScriptRoot\..\assets\DesktopHub_logo.png"
$icoPath = "$PSScriptRoot\..\assets\DesktopHub_logo.ico"

$png = [System.Drawing.Image]::FromFile((Resolve-Path $pngPath))
Write-Host "Source PNG: $($png.Width)x$($png.Height)"

# Create multi-size ICO (256, 48, 32, 16)
$sizes = @(256, 48, 32, 16)
$ms = New-Object System.IO.MemoryStream

$writer = New-Object System.IO.BinaryWriter($ms)

# ICO header
$writer.Write([UInt16]0)       # reserved
$writer.Write([UInt16]1)       # type: icon
$writer.Write([UInt16]$sizes.Count) # number of images

# Calculate offsets
$headerSize = 6 + ($sizes.Count * 16)
$imageDataList = @()

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($png, $size, $size)
    $pngStream = New-Object System.IO.MemoryStream
    $bmp.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageDataList += ,($pngStream.ToArray())
    $pngStream.Dispose()
    $bmp.Dispose()
}

$offset = $headerSize
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $data = $imageDataList[$i]
    
    $w = if ($size -ge 256) { 0 } else { $size }
    $h = if ($size -ge 256) { 0 } else { $size }
    
    $writer.Write([byte]$w)           # width
    $writer.Write([byte]$h)           # height
    $writer.Write([byte]0)            # color palette
    $writer.Write([byte]0)            # reserved
    $writer.Write([UInt16]1)          # color planes
    $writer.Write([UInt16]32)         # bits per pixel
    $writer.Write([UInt32]$data.Length) # size of image data
    $writer.Write([UInt32]$offset)     # offset
    
    $offset += $data.Length
}

foreach ($data in $imageDataList) {
    $writer.Write($data)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes((Join-Path (Split-Path $pngPath) "DesktopHub_logo.ico"), $ms.ToArray())

$writer.Dispose()
$ms.Dispose()
$png.Dispose()

Write-Host "Created ICO at: $icoPath"
Write-Host "Sizes: $($sizes -join ', ')px"
