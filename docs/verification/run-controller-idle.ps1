$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/controller-idle-harness.exe'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe "/out:$output" (Join-Path $PSScriptRoot 'ControllerIdleHarness.cs') (Join-Path $projectDirectory 'ModProject/ModCode/ModMain/Core/WorkshopController.cs')
if ($LASTEXITCODE -ne 0) { throw '控制器检查编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '控制器检查失败。' }
