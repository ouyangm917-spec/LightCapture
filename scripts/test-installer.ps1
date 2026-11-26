param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$Installer = [IO.Path]::GetFullPath($Installer)
$metadata = Get-Content -LiteralPath ($Installer + '.json') -Raw | ConvertFrom-Json
$root = [IO.Path]::GetFullPath($metadata.testRoot)
$boundary = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (!$metadata.testRoot -or !$root.StartsWith($boundary,[StringComparison]::OrdinalIgnoreCase) -or $metadata.testId -notmatch '^[0-9a-f]{32}$') { throw '只允许运行有独立目录和注册表根的测试安装包。' }
if ((Get-FileHash $Installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $metadata.sha256) { throw '测试安装包哈希不匹配。' }
$keyRoot = 'Software\LightCapture\InstallerTests\' + $metadata.testId
$reg = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry64)
$existing = $reg.OpenSubKey($keyRoot)
if ($existing) { $existing.Dispose(); throw '测试注册表根已存在，请使用新的隔离构建。' }
$appDir = Join-Path $root 'App'
$publish = Join-Path $projectRoot "artifacts/LightCapture-$($metadata.version)-win-x64"
$reportPath = Join-Path (Split-Path $Installer) 'installer-checks.json'
$results = [Collections.Generic.List[object]]::new()
function Check([string]$name,[bool]$success) {
    $results.Add([pscustomobject]@{name=$name;passed=$success})
    $results | ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding utf8
    if (!$success) { throw "安装验证失败：$name" }
}
function ReadValue([string]$key,[string]$name) {
    $opened = $reg.OpenSubKey($key)
    try { if ($opened) { $opened.GetValue($name) } } finally { if ($opened) { $opened.Dispose() } }
}
function RunHidden([string]$file,[string[]]$arguments) {
    $process = Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -PassThru
    try { $process.PriorityClass = 'BelowNormal' } catch [InvalidOperationException] { }
    if (!$process.WaitForExit(120000)) { throw '隔离安装超时，未终止任何应用。' }
    return $process.ExitCode
}
function UninstallTest {
    $code = RunHidden (Join-Path $appDir 'Uninstall.exe') @('/S')
    Check '卸载启动成功' ($code -eq 0)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ((Test-Path (Join-Path $appDir 'Uninstall.exe')) -and $watch.Elapsed.TotalSeconds -lt 30) { Start-Sleep -Milliseconds 100 }
    Check '卸载完成并清理卸载程序' (!(Test-Path (Join-Path $appDir 'Uninstall.exe')))
}

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class InstallerManifestReader {
 [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
 [DllImport("kernel32", SetLastError=true)] static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
 [DllImport("kernel32")] static extern uint SizeofResource(IntPtr module, IntPtr resource);
 [DllImport("kernel32")] static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
 [DllImport("kernel32")] static extern IntPtr LockResource(IntPtr resource);
 [DllImport("kernel32")] static extern bool FreeLibrary(IntPtr module);
 public static string Read(string path) {
   var module=LoadLibraryEx(path,IntPtr.Zero,2);
   if(module==IntPtr.Zero) throw new Win32Exception();
   try { var resource=FindResource(module,(IntPtr)1,(IntPtr)24); if(resource==IntPtr.Zero) throw new Win32Exception();
     var bytes=new byte[SizeofResource(module,resource)]; Marshal.Copy(LockResource(LoadResource(module,resource)),bytes,0,bytes.Length); return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
   } finally { FreeLibrary(module); }
 }
}
'@
function CheckManifest([string]$path,[string]$name) {
    [xml]$manifest = [InstallerManifestReader]::Read($path)
    $level = $manifest.SelectSingleNode("//*[local-name()='requestedExecutionLevel']").GetAttribute('level')
    Check "$name 使用 asInvoker" ($level -eq 'asInvoker')
}
$realRunKey = 'Software\Microsoft\Windows\CurrentVersion\Run'
$realBefore = ReadValue $realRunKey 'LightCapture'
try {
    CheckManifest $Installer '测试安装器'
    Check '首次安装成功' ((RunHidden $Installer @('/S','/AUTOSTART=1','/DESKTOP=1')) -eq 0)
    $expectedCommand = '"' + (Join-Path $appDir 'LightCapture.exe') + '" --autostart'
    Check '自启动命令引用固定目录' ((ReadValue ($keyRoot+'\Run') 'LightCapture') -eq $expectedCommand)
    Check '卸载登记位置正确' ((ReadValue ($keyRoot+'\Uninstall') 'InstallLocation') -eq $appDir)
    Check '开始菜单和桌面快捷方式创建' ((Test-Path (Join-Path $root 'Shortcuts/轻截.lnk')) -and (Test-Path (Join-Path $root 'Desktop/轻截.lnk')))
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut((Join-Path $root 'Shortcuts/轻截.lnk'))
    Check '开始菜单指向安装的程序' ($link.TargetPath -eq (Join-Path $appDir 'LightCapture.exe'))
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    CheckManifest (Join-Path $appDir 'LightCapture.exe') '应用'
    CheckManifest (Join-Path $appDir 'Uninstall.exe') '卸载器'
    $files = @(Get-ChildItem $publish -File -Recurse)
    foreach ($file in $files) {
        $installed = Join-Path $appDir ([IO.Path]::GetRelativePath($publish,$file.FullName))
        if (!(Test-Path -LiteralPath $installed) -or (Get-FileHash $installed -Algorithm SHA256).Hash -ne (Get-FileHash $file.FullName -Algorithm SHA256).Hash) { throw "安装文件不一致：$installed" }
    }
    Check "$($files.Count) 个安装文件逐一校验一致" $true
    Check '离线模型完整' (Test-Path (Join-Path $appDir 'offline/models/en-zh/model/model.bin'))
    $unknown = Join-Path $appDir '用户保留.txt'
    Set-Content -LiteralPath $unknown -Value 'keep' -Encoding utf8
    $lock = [IO.File]::Open((Join-Path $appDir 'LightCapture.exe'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try { Check '文件占用时拒绝升级且不结束进程' ((RunHidden $Installer @('/S','/AUTOSTART=0')) -eq 10) } finally { $lock.Dispose() }
    Check '升级被拒后保留原启动项' ((ReadValue ($keyRoot+'\Run') 'LightCapture') -eq $expectedCommand)
    Check '升级关闭选项成功' ((RunHidden $Installer @('/S','/AUTOSTART=0','/DESKTOP=0')) -eq 0)
    Check '关闭自启动与桌面选项生效' (!(ReadValue ($keyRoot+'\Run') 'LightCapture') -and !(Test-Path (Join-Path $root 'Desktop/轻截.lnk')))
    Check '升级默认保留关闭状态' (((RunHidden $Installer @('/S')) -eq 0) -and !(ReadValue ($keyRoot+'\Run') 'LightCapture'))
    Check '升级保留未知文件' ((Get-Content $unknown -Raw).Trim() -eq 'keep')
    Check '重新启用成功' ((RunHidden $Installer @('/S','/AUTOSTART=1')) -eq 0)
    UninstallTest
    Check '卸载移除本安装启动项和卸载登记' (!(ReadValue ($keyRoot+'\Run') 'LightCapture') -and !(ReadValue ($keyRoot+'\Uninstall') 'InstallLocation'))
    Check '卸载清理快捷方式并保留未知文件' (!(Test-Path (Join-Path $root 'Shortcuts/轻截.lnk')) -and (Test-Path $unknown))
    Check '卸载清理发布文件' (@($files | Where-Object { Test-Path -LiteralPath (Join-Path $appDir ([IO.Path]::GetRelativePath($publish,$_.FullName))) }).Count -eq 0)
    Check '卸载后重装成功' ((RunHidden $Installer @('/S','/AUTOSTART=1')) -eq 0)
    $otherCommand = '"C:\Other\LightCapture.exe" --autostart'
    $key = $reg.CreateSubKey($keyRoot+'\Run'); try { $key.SetValue('LightCapture',$otherCommand) } finally { $key.Dispose() }
    UninstallTest
    Check '卸载不删除其他位置的启动命令' ((ReadValue ($keyRoot+'\Run') 'LightCapture') -eq $otherCommand)
    Check '真实启动项未改变' ((ReadValue $realRunKey 'LightCapture') -eq $realBefore)
} finally {
    # 根目录已在入口限制到 artifacts，注册表路径含本次随机 ID。
    $reg.DeleteSubKeyTree($keyRoot,$false)
    $reg.Dispose()
}
$results | Format-Table
