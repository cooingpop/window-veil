param(
    [string]$OutputDir = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw "C# compiler not found: $compiler" }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$exe = Join-Path $OutputDir 'WindowVeil.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'src\Program.cs') (Join-Path $PSScriptRoot 'src\Veil.cs')
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }
Write-Output $exe
