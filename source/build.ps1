$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler was not found.' }
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Select-Object -ExpandProperty FullName
$arguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warn:4', '/utf8output',
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),
    ('/out:' + (Join-Path $destination 'EdgeRail.exe')),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Management.dll', '/reference:System.Xml.dll')
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'EdgeRail.exe.config') -Destination $destination -Force
Write-Output (Join-Path $destination 'EdgeRail.exe')
