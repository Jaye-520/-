$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$codeDirectory = Join-Path $projectDirectory 'ModProject/ModCode/ModMain'
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/paper-skin-harness.exe'
$landscape = Join-Path $codeDirectory 'Art/InkLandscape-v1.png'
$title = Join-Path $codeDirectory 'Art/TitleBrush-v1.png'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe "/out:$output" "/resource:$landscape,MOD_YtxSQ1.Art.InkLandscape.png" "/resource:$title,MOD_YtxSQ1.Art.TitleBrush.png" (Join-Path $PSScriptRoot 'PaperSkinHarness.cs') (Join-Path $codeDirectory 'UI/PaperSkin.cs')
if ($LASTEXITCODE -ne 0) { throw '纸墨缓存检查编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '纸墨缓存检查失败。' }
