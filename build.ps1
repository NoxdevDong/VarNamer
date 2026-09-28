# VarNamer 构建脚本（仅需 Windows 自带 .NET Framework 4.x，无需联网、无需 SDK）
#
#   产物：bin\VarNamer.exe            主程序（发布用）
#         build\VarNamer.selftest.exe 引擎自检（仅本机验证用，不随包发布）
#
# 用法： powershell -ExecutionPolicy Bypass -File build.ps1 [-SkipSelfTest]
param([switch]$SkipSelfTest)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "未找到 csc.exe，需要 .NET Framework 4.x（Win10/11 默认自带）" }

New-Item -ItemType Directory -Force -Path "$root\bin"   | Out-Null
New-Item -ItemType Directory -Force -Path "$root\build" | Out-Null

$resLex = '/resource:' + (Join-Path $root 'data\lexicon.txt') + ',lexicon.txt'
$resIco = '/resource:' + (Join-Path $root 'assets\app.ico') + ',app.ico'
$refs   = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')

# 注意：下面两个源文件清单是手写的，新增/删除 src\*.cs 时必须同步修改。
$srcEngine = @('Lexicon.cs','Namer.cs','Settings.cs','EngineTest.cs','Translation.cs','Theme.cs','Anim.cs','TabStrip.cs','Hotkey.cs') | ForEach-Object { Join-Path $root "src\$_" }
$srcGui = @('Lexicon.cs','Namer.cs','Settings.cs','Util.cs','Theme.cs','Anim.cs','TabStrip.cs','SingleInstance.cs','ModernForm.cs','DarkCombo.cs','Program.cs','TrayApp.cs','MainForm.cs','FloatForm.cs','LexiconForm.cs','Guide.cs','LanguagePreset.cs','Translation.cs','TextInfoForm.cs','TranslateHelp.cs','UnknownLog.cs','TranslateForm.cs','Hotkey.cs','SettingsForm.cs','AutoDict.cs','ShotModel.cs','ShotGeom.cs','ShotForm.cs','ShotToolbar.cs') | ForEach-Object { Join-Path $root "src\$_" }

if (-not (Test-Path "$root\assets\app.ico")) {
    Write-Host '[1/4] 生成图标 ...'
    $ia = @('/nologo','/target:exe', ('/out:' + (Join-Path $root 'build\make_icon.exe')), '/reference:System.dll', '/reference:System.Drawing.dll', (Join-Path $root 'tools\make_icon.cs'))
    & $csc @ia | Out-Null
    & "$root\build\make_icon.exe" "$root\assets\app.ico"
}

Write-Host '[2/4] 编译引擎自检 exe ...'
$t = @('/nologo','/optimize+','/target:exe', ('/out:' + (Join-Path $root 'build\VarNamer.selftest.exe'))) + $refs + @($resLex) + $srcEngine + (Join-Path $root 'src\SelfTest.cs')
& $csc @t
if ($LASTEXITCODE -ne 0) { throw "自检版编译失败" }

Write-Host '[3/4] 编译主程序 ...'
$g = @('/nologo','/optimize+','/target:winexe','/platform:anycpu', ('/out:' + (Join-Path $root 'bin\VarNamer.exe')), ('/win32icon:' + (Join-Path $root 'assets\app.ico')), ('/win32manifest:' + (Join-Path $root 'src\app.manifest'))) + $refs + @($resLex, $resIco) + $srcGui
& $csc @g
if ($LASTEXITCODE -ne 0) { throw "主程序编译失败" }

if (-not $SkipSelfTest) {
    Write-Host '[4/4] 运行引擎自检 ...'
    & "$root\build\VarNamer.selftest.exe" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "引擎自检未通过" }
    Write-Host '    自检通过（RESULT: PASS）'
} else {
    Write-Host '[4/4] 已跳过引擎自检'
}

Get-ChildItem "$root\bin\VarNamer.exe" | Select-Object Name, Length, LastWriteTime | Format-Table
Write-Host "构建完成。"
