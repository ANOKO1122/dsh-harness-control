# Build DeepSeekHarnessControl.exe using the .NET Framework C# compiler.
$ErrorActionPreference = 'Stop'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    throw "csc.exe not found at $csc"
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root 'bin\DeepSeekHarnessControl.exe'
$src = Join-Path $root 'harness-app\Program.cs'

New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null

& $csc /nologo /target:winexe /out:$out `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll `
    $src

if (Test-Path $out) {
    Write-Host "Built: $out"
} else {
    throw 'Build failed: output exe not found.'
}
