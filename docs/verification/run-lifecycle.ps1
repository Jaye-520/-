$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/lifecycle-harness.exe'
$codeDirectory = Join-Path $projectDirectory 'ModProject/ModCode/ModMain'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe "/out:$output" (Join-Path $PSScriptRoot 'LifecycleHarness.cs') (Join-Path $codeDirectory 'UI/WorkshopWindow.cs') (Join-Path $codeDirectory 'ModMain.cs') (Join-Path $codeDirectory 'ModVersion.cs') (Join-Path $codeDirectory 'Services/WorkshopPerformance.cs')
if ($LASTEXITCODE -ne 0) { throw '生命周期测试编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '生命周期测试失败。' }
