param([string]$OutputPath = '', [string]$TestRoot = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content (Join-Path $projectRoot 'src/LightCapture.App/LightCapture.App.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
$publish = Join-Path $projectRoot "artifacts/LightCapture-$version-win-x64"
$compiler = Join-Path $projectRoot '.tools/nsis/nsis-3.13/Bin/makensis.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '请先运行 scripts/setup-installer.ps1。' }
if (!$OutputPath) { $OutputPath = Join-Path $projectRoot "artifacts/LightCapture-$version-Setup-x64.exe" }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) { throw '输出文件已存在，请指定新的 OutputPath。' }
$testId = ''
if ($TestRoot) {
    $TestRoot = [IO.Path]::GetFullPath($TestRoot)
    $boundary = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (!$TestRoot.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw '隔离测试目录必须位于项目 artifacts 下。' }
    $testId = [Guid]::NewGuid().ToString('N')
}
& (Join-Path $PSScriptRoot 'package.ps1') -PrepareOnly | Out-Null
$info = Get-Content (Join-Path $publish 'release-info.json') -Raw | ConvertFrom-Json
$product = (Get-Item (Join-Path $publish 'LightCapture.exe')).VersionInfo.ProductVersion
if ($product -notlike "*$($info.sourceCommit)*") { throw 'EXE 源码版本不匹配，请重新 Publish。' }
$generated = Join-Path $projectRoot ('artifacts/installer-' + $version + $(if ($TestRoot) { '-test-' + $testId } else { '' }))
New-Item -ItemType Directory -Path $generated,(Split-Path $OutputPath) -Force | Out-Null
function NsisLiteral([string]$value) { $value.Replace('$','$$').Replace('"','$\"') }
$files = @(Get-ChildItem -LiteralPath $publish -File -Recurse)
$deleteLines = foreach ($file in $files) { 'Delete "$INSTDIR\' + (NsisLiteral ([IO.Path]::GetRelativePath($publish,$file.FullName))) + '"' }
$deleteLines += @(Get-ChildItem -LiteralPath $publish -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object { 'RMDir "$INSTDIR\' + (NsisLiteral ([IO.Path]::GetRelativePath($publish,$_.FullName))) + '"' })
$deleteLines | Set-Content -LiteralPath (Join-Path $generated 'uninstall-files.nsh') -Encoding utf8BOM
$definitions = [ordered]@{
    APP_VERSION=$version; OUTPUT_FILE=$OutputPath; PUBLISH_DIR=$publish; GENERATED_DIR=$generated
    APP_ICON=(Join-Path $projectRoot 'assets/icon/LightCapture.ico'); SIZE_KB=[string][int][Math]::Ceiling(($files | Measure-Object Length -Sum).Sum / 1KB)
}
if ($TestRoot) { $definitions.TEST_ROOT = $TestRoot; $definitions.TEST_ID = $testId }
$lines = @($definitions.GetEnumerator() | ForEach-Object { '!define ' + $_.Key + ' "' + (NsisLiteral $_.Value) + '"' })
$lines += '!include "' + (NsisLiteral (Join-Path $projectRoot 'installer/LightCapture.nsi')) + '"'
$script = Join-Path $generated 'build.nsi'
$lines | Set-Content -LiteralPath $script -Encoding utf8BOM
$process = Start-Process -FilePath $compiler -ArgumentList @('/V2','/WX','/INPUTCHARSET','UTF8',('"' + $script + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $generated 'compiler.log') -RedirectStandardError (Join-Path $generated 'compiler-error.log')
try { $process.PriorityClass = 'BelowNormal' } catch [InvalidOperationException] { }
$process.WaitForExit()
Get-Content (Join-Path $generated 'compiler.log'),(Join-Path $generated 'compiler-error.log')
if ($process.ExitCode -ne 0) { throw "安装包编译失败：$($process.ExitCode)" }
$hash = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($OutputPath))" | Set-Content -LiteralPath ($OutputPath + '.sha256') -Encoding ascii
[ordered]@{ version=$version; sourceCommit=$info.sourceCommit; installer=$OutputPath; sha256=$hash; fileCount=$files.Count; testRoot=$TestRoot; testId=$testId } | ConvertTo-Json | Set-Content -LiteralPath ($OutputPath + '.json') -Encoding utf8
Get-Item -LiteralPath $OutputPath | Select-Object FullName,Length
