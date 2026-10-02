$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/character-fates-harness.exe'
$codeDirectory = Join-Path $projectDirectory 'ModProject/ModCode/ModMain/Services'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe /main:CharacterFatesHarness "/out:$output" (Join-Path $PSScriptRoot 'CharacterFatesHarness.cs') (Join-Path $PSScriptRoot 'CharacterReaderHarness.cs') (Join-Path $PSScriptRoot 'CharacterActionsStubs.cs') (Join-Path $codeDirectory 'CharacterReader.cs') (Join-Path $codeDirectory 'CharacterFates.cs')
if ($LASTEXITCODE -ne 0) { throw '气运读取测试编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '气运读取测试失败。' }
