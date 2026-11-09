param(
    [ValidateSet('Build','Test','Publish')][string]$Task = 'Build',
    [string]$Configuration = 'Release',
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $localDotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
    $dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli-home'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
    $env:MSBuildEnableWorkloadResolver = 'false'
    if (Test-Path -LiteralPath $localDotnet) {
        $env:DOTNET_ROOT = Split-Path -Parent $localDotnet
        $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    }
    $common = @('-m:1', '-nr:false', '-p:BuildInParallel=false', '-p:UseSharedCompilation=false')
    if ($NoRestore) { $common += '--no-restore' }
    switch ($Task) {
        'Build' { & $dotnet build LightCapture.sln -c $Configuration @common }
        'Test' { & $dotnet test tests/LightCapture.Core.Tests/LightCapture.Core.Tests.csproj -c $Configuration --logger 'trx;LogFileName=core.trx' @common }
        'Publish' {
            if (!(Test-Path -LiteralPath '.tools/offline/models/en-zh/model/model.bin')) { throw '请先运行 scripts/setup-offline.ps1 准备离线翻译包。' }
            & $dotnet publish src/LightCapture.App/LightCapture.App.csproj -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/LightCapture-win-x64 @common
        }
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet $Task 失败（退出码 $LASTEXITCODE）。" }
} finally { Pop-Location }
