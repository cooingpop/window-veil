param(
    [int]$Minutes = 0
)
# window-veil launcher. Runs until you quit from the tray icon (or for $Minutes if > 0).
# Keep this file ASCII only: Windows PowerShell 5.1 reads BOM-less scripts in the system code page.
$ErrorActionPreference = 'Stop'
$dataDir = Join-Path $env:APPDATA 'WindowVeil'
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$logPath = Join-Path $dataDir 'veil.log'
try {
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing
    $src = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'src\Veil.cs')
    Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Core -TypeDefinition $src
    # Add-Type cannot embed the icon, so tell the app where the file is.
    [VeilApp]::IconPath = Join-Path $PSScriptRoot 'assets\WindowVeil.ico'
    [VeilApp]::Run($Minutes, $logPath)
} catch {
    Add-Content -LiteralPath $logPath -Value ("{0}  failed to start: {1}" -f (Get-Date -Format 'HH:mm:ss.fff'), $_.Exception.Message) -Encoding UTF8
    exit 1
}
