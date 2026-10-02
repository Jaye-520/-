$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/input-scope-harness.exe'
$source = Join-Path $projectDirectory 'ModProject/ModCode/ModMain/UI/WindowInputScope.cs'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe "/out:$output" (Join-Path $PSScriptRoot 'WindowInputScopeHarness.cs') $source
if ($LASTEXITCODE -ne 0) { throw '输入隔离检查编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '输入隔离检查失败。' }
