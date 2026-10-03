$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$codeDirectory = Join-Path $projectDirectory 'ModProject/ModCode/ModMain'
$output = Join-Path $projectDirectory '.tools/entrances-harness.exe'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe "/out:$output" "/resource:$codeDirectory/Art/EntranceJade-v2.png,MOD_YtxSQ1.Art.EntranceJade.png" (Join-Path $PSScriptRoot 'EntrancesHarness.cs') (Join-Path $codeDirectory 'UI/WorkshopEntrances.cs') (Join-Path $codeDirectory 'ModVersion.cs')
if ($LASTEXITCODE -ne 0) { throw '入口检查编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '入口检查失败。' }
