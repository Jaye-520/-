$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/features-harness.exe'
$codeDirectory = Join-Path $projectDirectory 'ModProject/ModCode/ModMain'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe "/out:$output" (Join-Path $PSScriptRoot 'FeatureHarness.cs') (Join-Path $codeDirectory 'Services/WorkshopFeatures.cs') (Join-Path $codeDirectory 'Services/FeatureOperations.cs') (Join-Path $codeDirectory 'Services/FeatureRuntime.cs') (Join-Path $codeDirectory 'Services/CapacityRuntime.cs')
if ($LASTEXITCODE -ne 0) { throw '功能服务检查编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '功能服务检查失败。' }
