$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/character-reader-harness.exe'
$source = Join-Path $projectDirectory 'ModProject/ModCode/ModMain/Services/CharacterReader.cs'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe "/out:$output" (Join-Path $PSScriptRoot 'CharacterReaderHarness.cs') $source
if ($LASTEXITCODE -ne 0) { throw '角色读取测试编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '角色读取测试失败。' }
