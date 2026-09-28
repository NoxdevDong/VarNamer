# VarNamer 回归套件一键运行
#   用法： powershell -ExecutionPolicy Bypass -File tools\check.ps1
#   覆盖：取名引擎 / 选项 / 词库覆盖语义 / 配置往返 / 各窗体按钮接线 / 托盘菜单 /
#         悬浮窗状态机与风格组 / 动效曲线 / 布局重叠检测 / 控件裁切检测
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw '未找到 csc.exe，需要 .NET Framework 4.x' }

$outDir = Join-Path $root 'build'
$dataDir = Join-Path $outDir 'testdata'
New-Item -ItemType Directory -Force -Path $outDir, $dataDir | Out-Null

$srcList = @('Lexicon.cs','Namer.cs','Settings.cs','Util.cs','Theme.cs','Anim.cs','TabStrip.cs','ModernForm.cs',
  'DarkCombo.cs','TrayApp.cs','MainForm.cs','FloatForm.cs','LexiconForm.cs','Guide.cs','LanguagePreset.cs',
  'Translation.cs','TextInfoForm.cs','TranslateHelp.cs','UnknownLog.cs','TranslateForm.cs','Hotkey.cs',
  'SettingsForm.cs','AutoDict.cs','ShotModel.cs','ShotGeom.cs','ShotForm.cs','ShotToolbar.cs') |
  ForEach-Object { Join-Path $root "src\$_" }

$cscArgs = @('/nologo','/target:exe','/platform:anycpu',
  ('/out:' + (Join-Path $outDir 'suite.exe')),
  '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll',
  ('/resource:' + (Join-Path $root 'data\lexicon.txt') + ',lexicon.txt'),
  ('/resource:' + (Join-Path $root 'assets\app.ico') + ',app.ico')) + $srcList + @((Join-Path $root 'tools\suite.cs'))

Write-Host '编译回归套件 ...'
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw '套件编译失败' }

# 隔离数据目录 + 关掉新手引导，避免弹窗卡住测试
$cfgFile = Join-Path $dataDir 'config.ini'
[IO.File]::WriteAllText($cfgFile, "guide=1" + [Environment]::NewLine + "theme=dark")
$env:VARNAMER_DATA_DIR = $dataDir
$report = Join-Path $outDir 'suite-report.txt'
& (Join-Path $outDir 'suite.exe') $report | Out-Null
Remove-Item Env:\VARNAMER_DATA_DIR -ErrorAction SilentlyContinue

Write-Host ''
Get-Content -LiteralPath $report -Encoding UTF8
$fail = (Select-String -LiteralPath $report -Pattern '\[FAIL\]' -Encoding UTF8 | Measure-Object).Count
Write-Host ''
if ($fail -gt 0) { Write-Host "回归未通过：$fail 项失败"; exit 1 }
Write-Host '回归通过（无 FAIL 项）'
