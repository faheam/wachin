# Converts icon.png into a multi-size .ico and copies the png into Assets/
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$proj = 'C:\Users\Facu\ProgramandoConIA\proyecto06'
$src = Join-Path $proj 'publish\win-x64\icon.png'
$assets = Join-Path $proj 'Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

# 1) Copy png as embedded-resource source
Copy-Item $src (Join-Path $assets 'icon.png') -Force

# 2) Build a multi-size ICO (Windows Vista+ supports PNG entries)
$img = [System.Drawing.Image]::FromFile($src)
$sizes = @(16, 32, 48, 64, 128, 256)
$count = $sizes.Count

# --- Generate all image blobs first ---
$images = New-Object System.Collections.ArrayList
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($img, $s, $s)
    $png = New-Object System.IO.MemoryStream
    $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $png.ToArray()
    [void]$images.Add($bytes)
    $png.Dispose()
    $bmp.Dispose()
}

# --- Directory entries with correct running offsets ---
$offset = 6 + 16 * $count
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$bw.Write([uint16]0)                  # reserved
$bw.Write([uint16]1)                  # type: icon
$bw.Write([uint16]$count)             # image count

for ($i = 0; $i -lt $count; $i++) {
    $bytes = $images[$i]
    $s = $sizes[$i]
    $w = if ($s -ge 256) { 0 } else { $s }

    $bw.Write([byte]$w)               # width
    $bw.Write([byte]$w)               # height
    $bw.Write([byte]0)                # palette
    $bw.Write([byte]0)                # reserved
    $bw.Write([uint16]1)              # planes
    $bw.Write([uint16]32)             # bpp
    $bw.Write([uint32]$bytes.Length)  # size of image data
    $bw.Write([uint32]$offset)        # offset of image data
    $offset += $bytes.Length
}

# --- Then all image data ---
foreach ($bytes in $images) {
    $bw.Write($bytes, 0, $bytes.Length)
}

$bw.Flush()
$dst = Join-Path $assets 'icon.ico'
[System.IO.File]::WriteAllBytes($dst, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
$img.Dispose()

Write-Host "ICO created: $((Get-Item $dst).Length) bytes -> $dst"
