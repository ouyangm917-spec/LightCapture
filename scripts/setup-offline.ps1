$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'assets/offline/dependencies.json') -Raw -Encoding utf8 | ConvertFrom-Json
$cache = Join-Path $projectRoot '.tools/downloads'
$destination = Join-Path $projectRoot '.tools/offline'
New-Item -ItemType Directory -Path $cache,$destination -Force | Out-Null

function Expand-CheckedArchive([string]$archivePath, [string]$target, [string]$prefix = '', [switch]$ModelOnly) {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $targetPrefix = [IO.Path]::GetFullPath($target).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $archive.Entries) {
            if (!$entry.FullName.StartsWith($prefix, [StringComparison]::Ordinal)) { continue }
            $relative = $entry.FullName.Substring($prefix.Length)
            if (!$relative -or $relative.EndsWith('/')) { continue }
            if ($ModelOnly -and !($relative.StartsWith('model/') -or $relative -in @('sentencepiece.model','metadata.json','README.md'))) { continue }
            $path = [IO.Path]::GetFullPath((Join-Path $target $relative))
            if (!$path.StartsWith($targetPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw '归档条目超出目标目录。' }
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($path)) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $path, $true)
        }
    } finally { $archive.Dispose() }
}

foreach ($dependency in $manifest.dependencies) {
    $path = Join-Path $cache $dependency.filename
    if (!(Test-Path -LiteralPath $path)) {
        Write-Host "下载 $($dependency.name) $($dependency.version)…"
        Invoke-WebRequest -Uri $dependency.url -OutFile $path
    }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $dependency.sha256) { throw "$($dependency.filename) 校验失败；请删除该下载文件后重试。" }
    switch ($dependency.kind) {
        'python' { Expand-CheckedArchive $path (Join-Path $destination 'python') }
        'wheel' { Expand-CheckedArchive $path (Join-Path $destination 'packages') }
        'model' { Expand-CheckedArchive $path (Join-Path $destination 'models/en-zh') 'translate-en_zh-1_9/' -ModelOnly }
    }
}
$pythonFolder = Join-Path $destination 'python'
# 使用固定搜索路径隔离用户 Python、环境变量和全局 site-packages。
@($manifest.pythonLibrary, '.', '../packages', 'import site') | Set-Content -LiteralPath (Join-Path $pythonFolder $manifest.pythonPathFile) -Encoding ascii
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets/offline/translate.py') -Destination (Join-Path $destination 'translate.py') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets/offline/dependencies.json') -Destination (Join-Path $destination 'dependencies.json') -Force
& (Join-Path $pythonFolder 'python.exe') -I -B -c 'import ctranslate2, sentencepiece, numpy, yaml; print("Offline runtime:", ctranslate2.__version__, sentencepiece.__version__)'
if ($LASTEXITCODE -ne 0) { throw '离线运行库导入失败。' }
Write-Host '离线运行库和简体中文模型已就绪。'
