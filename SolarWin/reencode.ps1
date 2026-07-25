Add-Type -AssemblyName System.Drawing
$base = "D:\sn winui\SolarWin"
foreach ($n in @("before","after")) {
    $p = Join-Path $base ".$n.png"
    Write-Output "Reading: $p"
    $img = [System.Drawing.Image]::FromFile($p)
    $bmp = New-Object System.Drawing.Bitmap($img.Width, $img.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.DrawImage($img, 0, 0, $img.Width, $img.Height)
    $out = Join-Path $base "$n-re.png"
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $img.Dispose()
    Write-Output "Wrote: $out"
}
