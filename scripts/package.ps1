param([string]$OutputPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $projectRoot 'artifacts/LightCapture-win-x64'
if (!(Test-Path -LiteralPath (Join-Path $publish 'LightCapture.exe'))) { throw '请先运行 scripts/build.ps1 -Task Publish。' }
if (!$OutputPath) { $OutputPath = Join-Path $projectRoot 'artifacts/LightCapture-win-x64.zip' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) { throw '输出文件已存在，请指定新的 OutputPath。' }
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($OutputPath)),(Join-Path $publish 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $publish 'README.md') -Force
foreach ($name in @('implementation-plan.md','verification.md','review.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "docs/$name") -Destination (Join-Path $publish "docs/$name") -Force
}
$report = Join-Path $projectRoot 'artifacts/native-checks/native-checks.json'
if (Test-Path -LiteralPath $report) { Copy-Item -LiteralPath $report -Destination (Join-Path $publish 'docs/native-checks.json') -Force }
$commit = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw '无法读取发布提交。' }
[ordered]@{
    version = '0.1.0'
    sourceCommit = $commit
    target = 'win-x64'
    selfContained = $true
    offlineTranslation = $true
    guiAcceptance = '未完成；按用户要求未继续前台自动化'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publish 'release-info.json') -Encoding utf8
$temporary = $OutputPath + '.partial'
try {
    [IO.Compression.ZipFile]::CreateFromDirectory($publish, $temporary, [IO.Compression.CompressionLevel]::Optimal, $true)
    Move-Item -LiteralPath $temporary -Destination $OutputPath
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
$hash = Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($OutputPath))" | Set-Content -LiteralPath ($OutputPath + '.sha256') -Encoding ascii
Get-Item -LiteralPath $OutputPath | Select-Object FullName,Length
