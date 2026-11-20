<#
  Captura cuadros SOLO de la ventana de FlowDesk Capture (no del escritorio completo) hasta que
  aparezca el archivo -StopFile. Cada cuadro guarda el subtítulo vigente leído de -CaptionFile.
#>
param(
    [Parameter(Mandatory)] [string] $OutDir,
    [Parameter(Mandatory)] [string] $StopFile,
    [Parameter(Mandatory)] [string] $CaptionFile,
    [int] $IntervalMs = 120
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Cap {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  public struct RECT { public int L, T, R, B; }
}
"@
[Cap]::SetProcessDPIAware() | Out-Null
New-Item -ItemType Directory -Force $OutDir | Out-Null

$jpeg = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$params = New-Object Drawing.Imaging.EncoderParameters 1
$params.Param[0] = New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality), 82L

$index = 0
$clock = [Diagnostics.Stopwatch]::StartNew()
$log = Join-Path $OutDir 'frames.txt'
Set-Content $log '' -Encoding utf8

while (-not (Test-Path $StopFile)) {
    $proc = Get-Process FlowDesk.Capture -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc) {
        $proc.Refresh()
        $h = $proc.MainWindowHandle
        $r = New-Object Cap+RECT
        if ($h -ne [IntPtr]::Zero -and -not [Cap]::IsIconic($h) -and [Cap]::GetWindowRect($h, [ref]$r) -and ($r.R - $r.L) -gt 400) {
            # Recorte dentro del borde de la ventana (sin sombra).
            $x = $r.L + 8; $y = $r.T; $w = $r.R - $r.L - 16; $hgt = $r.B - $r.T - 8
            $bmp = New-Object Drawing.Bitmap $w, $hgt
            $g = [Drawing.Graphics]::FromImage($bmp)
            $g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
            $file = '{0:D5}.jpg' -f $index
            $bmp.Save((Join-Path $OutDir $file), $jpeg, $params)
            $g.Dispose(); $bmp.Dispose()
            $caption = if (Test-Path $CaptionFile) { (Get-Content $CaptionFile -Raw -Encoding utf8).Trim() } else { '' }
            Add-Content $log "$file|$($clock.ElapsedMilliseconds)|$caption" -Encoding utf8
            $index++
        }
    }
    Start-Sleep -Milliseconds $IntervalMs
}
