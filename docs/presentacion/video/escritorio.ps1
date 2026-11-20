<#
  Parte de escritorio del video: maneja FlowDesk Capture (WPF) con UI Automation y captura SOLO su ventana.
  Deja en -OutDir los cuadros JPG y frames.txt (archivo|ms|escena). Lo invoca grabar.js.
#>
param(
    [Parameter(Mandatory)] [string] $OutDir,
    [Parameter(Mandatory)] [string] $Exe,
    [Parameter(Mandatory)] [string] $Title,
    [string] $Applicant = 'Andes Ltda.',
    [string] $HotFolder
)

$ErrorActionPreference = 'Stop'
if (-not $HotFolder) { $HotFolder = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'entrada' }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Win {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
}
"@
$A = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]; $CT = [Windows.Automation.ControlType]

# ---------- Archivos de ejemplo ----------
$files = Join-Path $OutDir 'archivos'
New-Item -ItemType Directory -Force $files | Out-Null
$pdfText = 'Contrato de prestacion de servicios - Andes Ltda.'
$stream = "BT /F1 22 Tf 72 720 Td ($pdfText) Tj 0 -36 Td /F1 14 Tf (Vigencia: 12 meses - Valor: 1.250.000) Tj ET"
@"
%PDF-1.4
1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj
3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj
4 0 obj<</Length $($stream.Length)>>stream
$stream
endstream endobj
5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj
trailer<</Root 1 0 R>>
%%EOF
"@ | Set-Content (Join-Path $files 'contrato-servicios.pdf') -Encoding ascii -NoNewline

$bmp = New-Object Drawing.Bitmap 850, 1100
$g = [Drawing.Graphics]::FromImage($bmp); $g.Clear([Drawing.Color]::White)
$g.TextRenderingHint = 'AntiAlias'
$g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(27, 37, 51))), 0, 0, 850, 110)
$g.DrawString('FACTURA N.º 000123', ([Drawing.Font]::new('Segoe UI', [single]26, [Drawing.FontStyle]::Bold)), [Drawing.Brushes]::White, 40, 30)
$g.DrawString("Proveedor: $Applicant", ([Drawing.Font]::new('Segoe UI', [single]16)), [Drawing.Brushes]::Black, 40, 150)
$pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(213, 217, 224))
for ($y = 230; $y -lt 960; $y += 40) { $g.DrawLine($pen, 40, $y, 810, $y) }
$g.DrawString('Total: $ 1.250.000', ([Drawing.Font]::new('Segoe UI', [single]22, [Drawing.FontStyle]::Bold)), (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(217, 115, 78))), 40, 990)
$bmp.Save((Join-Path $files 'factura-proveedor.png'), [Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
Set-Content (Join-Path $files 'cedula.pdf') 'MZ este archivo no es un PDF' -Encoding ascii
Remove-Item $HotFolder -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $HotFolder | Out-Null

# ---------- Utilidades de UI Automation ----------
$caption = Join-Path $OutDir 'escena.txt'
function Scene([string] $id) { Set-Content $caption $id -Encoding utf8 -NoNewline }

$proc = $null
function AppWindows {
    $cond = New-Object Windows.Automation.PropertyCondition($A::ProcessIdProperty, $proc.Id)
    $A::RootElement.FindAll($TS::Children, $cond)
}
function WaitWindow([string] $name, [int] $timeout = 30) {
    for ($i = 0; $i -lt $timeout * 4; $i++) {
        foreach ($w in AppWindows) { if ($w.Current.Name -eq $name -and $w.Current.BoundingRectangle.Width -gt 400) { return $w } }
        Start-Sleep -Milliseconds 250
    }
    throw "No apareció la ventana '$name'"
}
function Front($w) {
    $h = [IntPtr]$w.Current.NativeWindowHandle
    [Win]::ShowWindow($h, 9) | Out-Null
    [Win]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0003) | Out-Null   # HWND_TOPMOST, sin mover ni redimensionar
    [Win]::SetForegroundWindow($h) | Out-Null
}
function All($el) { $el.FindAll($TS::Descendants, [Windows.Automation.Condition]::TrueCondition) }
function FindButton($root, [string] $text) {
    $cond = New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, $CT::Button)
    foreach ($b in $root.FindAll($TS::Descendants, $cond)) {
        if ($b.Current.Name -like "*$text*") { return $b }
        foreach ($t in (All $b)) { if ($t.Current.Name -like "*$text*") { return $b } }
    }
}
function FindButtonAnywhere([string] $text) {
    foreach ($w in AppWindows) { $b = FindButton $w $text; if ($b) { return @($w, $b) } }
    throw "No encontré el botón '$text'"
}
function Click($button) { $button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
function TypeText($edit, [string] $text, [int] $delay = 45) {
    $vp = $edit.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
    for ($i = 1; $i -le $text.Length; $i++) { $vp.SetValue($text.Substring(0, $i)); Start-Sleep -Milliseconds $delay }
}
function SelectItem($item) { $item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }

# ---------- Captura de la ventana ----------
$stop = Join-Path $OutDir 'detener.txt'
Remove-Item $stop -ErrorAction SilentlyContinue
Scene 'E1'
$capturer = Start-Process powershell -WindowStyle Hidden -PassThru -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'capturar-ventana.ps1'),
    '-OutDir', $OutDir, '-StopFile', $stop, '-CaptionFile', $caption, '-IntervalMs', '50')

try {
    # E1 · Inicio de sesión
    # Solo para esta ejecución: simulador de escáner y carpeta vigilada.
    $env:Capture__Twain__Driver = 'Simulated'
    $env:Capture__HotFolder__Path = $HotFolder
    $proc = Start-Process $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
    $env:Capture__Twain__Driver = $null
    $env:Capture__HotFolder__Path = $null
    $login = WaitWindow 'FlowDesk Capture · Iniciar sesión'
    Front $login; Start-Sleep -Milliseconds 2500
    $pwd = (All $login) | Where-Object { $_.Current.ClassName -eq 'PasswordBox' } | Select-Object -First 1
    TypeText $pwd 'Operador123!' 70
    Start-Sleep -Milliseconds 600
    Click (FindButton $login 'Ingresar')

    $main = WaitWindow 'FlowDesk Capture'
    Front $main; Start-Sleep -Milliseconds 2500

    # E2 · Nuevo expediente
    Scene 'E2'
    Click (FindButton $main 'Nuevo expediente'); Start-Sleep -Milliseconds 1500
    $dialogWin, $createBtn = FindButtonAnywhere 'Crear expediente'
    $edits = (All $dialogWin) | Where-Object { $_.Current.ControlType -eq $CT::Edit -and $_.Current.BoundingRectangle.Width -gt 200 } |
        Sort-Object { $_.Current.BoundingRectangle.Top }
    # Los tres campos del diálogo están debajo del buscador de la barra lateral.
    $dialogEdits = $edits | Where-Object { $_.Current.BoundingRectangle.Left -gt ($main.Current.BoundingRectangle.Left + 400) }
    TypeText $dialogEdits[0] $Title
    TypeText $dialogEdits[1] $Applicant
    TypeText $dialogEdits[2] 'Registro de nuevo proveedor: contrato y factura inicial.' 25
    try {
        $combos = (All $dialogWin) | Where-Object { $_.Current.ControlType -eq $CT::ComboBox } | Sort-Object { $_.Current.BoundingRectangle.Left }
        $assignee = $combos[-1]
        $assignee.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 900
        $items = $assignee.FindAll($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, $CT::ListItem)))
        SelectItem $items[$items.Count - 1]; Start-Sleep -Milliseconds 300
        $assignee.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    } catch { }
    Start-Sleep -Milliseconds 1200
    Click $createBtn; Start-Sleep -Milliseconds 3500

    # E3 · Cargar PDF e imágenes (incluye un archivo con firma falsa)
    # Se depositan en la carpeta vigilada (equivale a arrastrarlos o a que un escáner de red los deje ahí);
    # así el video no muestra el cuadro de diálogo de Windows con carpetas personales.
    Scene 'E3'
    foreach ($name in 'contrato-servicios.pdf', 'factura-proveedor.png', 'cedula.pdf') {
        Copy-Item (Join-Path $files $name) (Join-Path $HotFolder $name)
        Start-Sleep -Milliseconds 1800
    }
    Start-Sleep -Milliseconds 3000

    # E4 · Revisar antes de enviar
    Scene 'E4'
    $tray = (All $main) | Where-Object { $_.Current.ControlType -eq $CT::ListItem -and $_.Current.Name -like '*CapturedItem*' }
    SelectItem $tray[0]; Start-Sleep -Milliseconds 3500
    SelectItem $tray[1]; Start-Sleep -Milliseconds 3000

    # E5 · Escáner (simulador TWAIN)
    Scene 'E5'
    Click (FindButton $main 'Escanear'); Start-Sleep -Milliseconds 5000

    # E6 · Enviar
    Scene 'E6'
    Click (FindButton $main 'documento(s)'); Start-Sleep -Milliseconds 4000

    # E7 · Resultado del procesamiento
    Scene 'E7'
    Start-Sleep -Milliseconds 6000
}
finally {
    Set-Content $stop 'ok'
    $capturer.WaitForExit(10000) | Out-Null
    if ($proc -and -not $proc.HasExited) { $proc.CloseMainWindow() | Out-Null; Start-Sleep 1; if (-not $proc.HasExited) { $proc.Kill() } }
}
