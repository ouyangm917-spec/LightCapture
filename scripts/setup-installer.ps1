$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$archive = Join-Path $projectRoot '.tools/downloads/nsis-3.13.zip'
$compiler = Join-Path $projectRoot '.tools/nsis/nsis-3.13/makensis.exe'
$expected = 'ba63dffc4410ee89193e1cb5a41989991bd77c61068da17e3156d136b7b0b3d8'
New-Item -ItemType Directory -Path (Split-Path $archive) -Force | Out-Null
if (!(Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest 'https://downloads.sourceforge.net/project/nsis/NSIS%203/3.13/nsis-3.13.zip' -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'NSIS 下载文件校验失败。' }
if (!(Test-Path -LiteralPath $compiler)) { Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $projectRoot '.tools/nsis') }
Write-Output $compiler
