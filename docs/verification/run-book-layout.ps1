$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $projectDirectory '.tools/microsoft.net.compilers.toolset.4.8.0/tasks/net472/csc.exe'
$output = Join-Path $projectDirectory '.tools/book-layout-harness.exe'
$codeDirectory = Join-Path $projectDirectory 'ModProject/ModCode/ModMain'
$previewDirectory = Join-Path $projectDirectory 'docs/screenshots'
& $compiler /nologo /warn:4 /warnaserror+ /target:exe /main:BookLayoutHarness /reference:System.Drawing.dll "/out:$output" (Join-Path $PSScriptRoot 'BookLayoutHarness.cs') (Join-Path $PSScriptRoot 'CharacterReaderHarness.cs') (Join-Path $codeDirectory 'Services/CharacterReader.cs') (Join-Path $codeDirectory 'Services/CharacterFates.cs') (Join-Path $codeDirectory 'Services/CharacterRoster.cs') (Join-Path $codeDirectory 'Services/CharacterPortrait.cs') (Join-Path $codeDirectory 'UI/BookView.cs')
if ($LASTEXITCODE -ne 0) { throw '宽版布局检查编译失败。' }
& $output (Join-Path $codeDirectory 'Art') $previewDirectory
if ($LASTEXITCODE -ne 0) { throw '宽版布局检查失败。' }
