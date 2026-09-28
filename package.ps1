# VarNamer 打包脚本：一次产出【便携版 zip】和【安装版 exe】
#   用法： powershell -ExecutionPolicy Bypass -File package.ps1 [-SkipSelfTest]
param([switch]$SkipSelfTest)
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$out  = Split-Path $root -Parent
$ver  = '5.3'
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "未找到 csc.exe，需要 .NET Framework 4.x" }
$refs = @('/reference:System.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')

Write-Host '== 1/5 编译主程序 =='
if ($SkipSelfTest) { & "$root\build.ps1" -SkipSelfTest } else { & "$root\build.ps1" }

Write-Host '== 2/5 生成在线翻译使用说明 =='
& "$root\bin\VarNamer.exe" --trhelp | Out-Null
if (-not (Test-Path "$root\bin\在线翻译使用说明.txt")) { throw '生成在线翻译使用说明失败' }

Write-Host '== 3/5 编译卸载器 =='
$u = @('/nologo','/optimize+','/target:winexe','/platform:anycpu', ('/out:' + (Join-Path $root 'build\uninstall.exe')), ('/win32icon:' + (Join-Path $root 'assets\app.ico'))) + $refs + @((Join-Path $root 'tools\Uninstall.cs'))
& $csc @u
if ($LASTEXITCODE -ne 0) { throw '卸载器编译失败' }

Write-Host '== 4/5 编译安装版 =='
$setupOut = Join-Path $out ("VarNamer-Setup-v" + $ver + ".exe")
$g = @('/nologo','/optimize+','/target:winexe','/platform:anycpu', ('/out:' + $setupOut), ('/win32icon:' + (Join-Path $root 'assets\app.ico'))) + $refs + @(
    ('/resource:' + (Join-Path $root 'bin\VarNamer.exe') + ',p_main'),
    ('/resource:' + (Join-Path $root 'dict\VarNamer-Dict-CN-EN.txt') + ',p_dict'),
    ('/resource:' + (Join-Path $root '使用说明.txt') + ',p_readme'),
    ('/resource:' + (Join-Path $root 'bin\在线翻译使用说明.txt') + ',p_trhelp'),
    ('/resource:' + (Join-Path $root 'build\uninstall.exe') + ',p_uninst'),
    (Join-Path $root 'tools\Setup.cs'))
& $csc @g
if ($LASTEXITCODE -ne 0) { throw '安装版编译失败' }

Write-Host '== 5/5 组装便携版 =='
$pv = Join-Path $out ("VarNamer-Portable-v" + $ver)
if (Test-Path $pv) {
    try { Remove-Item -LiteralPath $pv -Recurse -Force -ErrorAction Stop }
    catch { Write-Host ('  注意：旧目录被占用，已跳过清理（' + $pv + '）') }
}
New-Item -ItemType Directory -Force -Path (Join-Path $pv 'dict') | Out-Null
Copy-Item -LiteralPath "$root\bin\VarNamer.exe"               -Destination $pv
Copy-Item -LiteralPath "$root\使用说明.txt"                     -Destination $pv
Copy-Item -LiteralPath "$root\bin\在线翻译使用说明.txt"        -Destination $pv
Copy-Item -LiteralPath "$root\dict\VarNamer-Dict-CN-EN.txt"        -Destination (Join-Path $pv 'dict')
@'
便携版说明：本目录可以直接复制到任意位置（含 U 盘）使用，双击 VarNamer.exe 即可。
本文件的存在表示“数据跟着程序走”：配置、词库、记录都会写在同目录的 VarNamerData 文件夹里，
不写系统盘、不写注册表；删除整个文件夹就是彻底卸载。
如需改为把数据放到 %APPDATA%\VarNamer（安装版的行为），删掉本文件即可。
'@ | Set-Content -LiteralPath (Join-Path $pv 'portable.txt') -Encoding UTF8

$zip = Join-Path $out ("VarNamer-Portable-v" + $ver + ".zip")
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
# 发布前加固：万一之前在便携目录里跑过程序，会留下 VarNamerData（含你的配置与词库）
# —— 那是用户数据，绝对不能进发布包
$stale = Join-Path $pv 'VarNamerData'
if (Test-Path $stale) { Remove-Item -LiteralPath $stale -Recurse -Force; Write-Host '  已清理便携目录里的 VarNamerData（用户数据不入包）' }

Compress-Archive -Path (Join-Path $pv '*') -DestinationPath $zip -CompressionLevel Optimal

# 发布前自检：确认包里没有用户数据
Add-Type -AssemblyName System.IO.Compression.FileSystem
$za = [System.IO.Compression.ZipFile]::OpenRead($zip)
$bad = @($za.Entries | Where-Object { $_.FullName -match 'VarNamerData|config\.ini|userdict|unknowns|history\.txt|\.vnbak' })
$n = $za.Entries.Count
$za.Dispose()
if ($bad.Count -gt 0) { throw ('发布包自检失败：疑似夹带用户数据 → ' + ($bad | ForEach-Object { $_.FullName } | Select-Object -First 5) -join ', ') }
Write-Host ('  发布包自检通过：' + $n + ' 个文件，未夹带用户数据')

Write-Host ''
Write-Host '产物：'
Get-Item $zip, $setupOut | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host '打包完成。'
