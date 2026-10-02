param(
    [string]$OutputDir = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$version = (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'VERSION')).Trim()
if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw "VERSION must be MAJOR.MINOR.PATCH: $version" }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw "C# compiler not found: $compiler" }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$exe = Join-Path $OutputDir 'WindowVeil.exe'
$versionSource = Join-Path $OutputDir 'VersionInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("Window Veil")]
[assembly: AssemblyProduct("Window Veil")]
[assembly: AssemblyCompany("Window Veil contributors")]
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
[assembly: AssemblyInformationalVersion("$version")]
"@ | Set-Content -LiteralPath $versionSource -Encoding ASCII
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'src\Program.cs') (Join-Path $PSScriptRoot 'src\Veil.cs') $versionSource
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }
Write-Output $exe
