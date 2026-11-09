$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdkRoot = Join-Path $projectRoot '.tools/dotnet'
$sdkVersion = '10.0.403'
if (Test-Path -LiteralPath (Join-Path $sdkRoot 'sdk/10.0.403')) { Write-Host "项目 SDK $sdkVersion 已存在。"; return }
$cache = Join-Path $projectRoot '.tools/downloads'
New-Item -ItemType Directory -Path $cache,$sdkRoot -Force | Out-Null
$archivePath = Join-Path $cache "dotnet-sdk-$sdkVersion-win-x64.zip"
$expected = '7a114c318fa7f1d9822991b534b2814d0e78386ceda816b371f5141acefe741d87ccb606ede06cadac8f9ac5070e32b708a67bbd1619ff5e5a7858a27874ef48'
if (!(Test-Path -LiteralPath $archivePath)) {
    Invoke-WebRequest -Uri "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdkVersion/dotnet-sdk-$sdkVersion-win-x64.zip" -OutFile $archivePath
}
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA512).Hash -ne $expected) { throw 'SDK 的 SHA-512 与微软发布元数据不一致。' }
[IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $sdkRoot, $true)
& (Join-Path $sdkRoot 'dotnet.exe') --version
if ($LASTEXITCODE -ne 0) { throw '项目 SDK 启动失败。' }
