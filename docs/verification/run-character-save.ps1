$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/character-save-harness.exe'
$codeDirectory = Join-Path $projectDirectory 'ModProject/ModCode/ModMain/Services'
$sources = @('CharacterReader.cs','CharacterFates.cs','CharacterEditSession.cs','CharacterEditData.cs','CharacterFateCatalog.cs','CharacterFateOperations.cs','CharacterSave.cs','CharacterRealmChange.cs','CharacterActions.cs') | ForEach-Object { Join-Path $codeDirectory $_ }
& $compiler /nologo /warn:4 /warnaserror+ /target:exe /define:ACTIONS_ONLY /main:CharacterSaveHarness "/out:$output" (Join-Path $PSScriptRoot 'CharacterSaveHarness.cs') (Join-Path $PSScriptRoot 'CharacterReaderHarness.cs') (Join-Path $PSScriptRoot 'CharacterActionsStubs.cs') @sources
if ($LASTEXITCODE -ne 0) { throw '保存适配检查编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '保存适配检查失败。' }
