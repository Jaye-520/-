$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/character-editing-harness.exe'
$codeDirectory = Join-Path $projectDirectory 'ModProject/ModCode/ModMain/Services'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe /main:CharacterEditingHarness "/out:$output" (Join-Path $PSScriptRoot 'CharacterEditingHarness.cs') (Join-Path $PSScriptRoot 'CharacterReaderHarness.cs') (Join-Path $PSScriptRoot 'CharacterActionsStubs.cs') (Join-Path $codeDirectory 'CharacterReader.cs') (Join-Path $codeDirectory 'CharacterEditSession.cs') (Join-Path $codeDirectory 'CharacterEditData.cs')
if ($LASTEXITCODE -ne 0) { throw '属性编辑检查编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '属性编辑检查失败。' }
