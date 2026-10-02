param([string]$GameRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$ErrorActionPreference = 'Stop'
$toolsDirectory = Join-Path $PSScriptRoot '.tools'
New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(
    @{ Name = 'microsoft.netframework.referenceassemblies.net472'; Version = '1.0.3'; Marker = 'build\.NETFramework\v4.7.2\mscorlib.dll' },
    @{ Name = 'microsoft.net.compilers.toolset'; Version = '4.8.0'; Marker = 'tasks\net472\csc.exe' }
)
foreach ($package in $packages) {
    $identity = $package.Name + '.' + $package.Version
    $destination = Join-Path $toolsDirectory $identity
    if (!(Test-Path -LiteralPath (Join-Path $destination $package.Marker))) {
        if (Test-Path -LiteralPath $destination) { throw "依赖目录不完整，请检查后移走并重试：$destination" }
        $archive = Join-Path $toolsDirectory ($identity + '.nupkg')
        $url = 'https://api.nuget.org/v3-flatcontainer/' + $package.Name + '/' + $package.Version + '/' + $identity + '.nupkg'
        Invoke-WebRequest -Uri $url -OutFile $archive -TimeoutSec 120 -UseBasicParsing
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $destination)
    }
}
$codeDirectory = Join-Path $PSScriptRoot 'ModProject\ModCode\ModMain'
$referenceDirectory = Join-Path $toolsDirectory 'microsoft.netframework.referenceassemblies.net472.1.0.3\build\.NETFramework\v4.7.2'
$compiler = Join-Path $toolsDirectory 'microsoft.net.compilers.toolset.4.8.0\tasks\net472\csc.exe'
$outputDirectory = Join-Path $codeDirectory 'bin\Release'
$stagingDirectory = Join-Path $toolsDirectory 'build-output'
New-Item -ItemType Directory -Path $outputDirectory,$stagingDirectory -Force | Out-Null
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $codeDirectory 'ModMain.csproj') -Raw)
$ns = [Xml.XmlNamespaceManager]::new($projectXml.NameTable)
$ns.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')
$assemblyName = $projectXml.SelectSingleNode('//m:AssemblyName', $ns).InnerText
if ($assemblyName -ne 'MOD_YtxSQ1') { throw '程序集身份与本项目不符。' }
$frameworkVersion = $projectXml.SelectSingleNode('//m:TargetFrameworkVersion', $ns).InnerText
if ($frameworkVersion -ne 'v4.7.2') { throw '官方要求目标框架为 v4.7.2。' }
$lines = [Collections.Generic.List[string]]::new()
@('/nologo','/target:library','/nostdlib+','/langversion:7.3','/optimize+','/debug:portable','/deterministic+','/warn:4','/warnaserror+') | ForEach-Object { $lines.Add($_) }
$stagedDll = Join-Path $stagingDirectory ($assemblyName + '.dll')
$lines.Add('/out:"' + $stagedDll + '"')
$lines.Add('/reference:"' + (Join-Path $referenceDirectory 'mscorlib.dll') + '"')
foreach ($reference in $projectXml.SelectNodes('//m:Reference', $ns)) {
    $hint = $reference.SelectSingleNode('m:HintPath', $ns)
    $path = if ($hint) { $hint.InnerText.Replace('$(GameRoot)', $GameRoot) } else { Join-Path $referenceDirectory ($reference.Include + '.dll') }
    if (!(Test-Path -LiteralPath $path)) { throw "缺少引用：$path" }
    $lines.Add('/reference:"' + $path + '"')
}
foreach ($source in $projectXml.SelectNodes('//m:Compile', $ns)) {
    $sourcePath = Join-Path $codeDirectory $source.Include
    if (!(Test-Path -LiteralPath $sourcePath)) { throw "缺少源码：$sourcePath" }
    $lines.Add('"' + $sourcePath + '"')
}
$responseFile = Join-Path $toolsDirectory 'build.rsp'
foreach ($resource in $projectXml.SelectNodes('//m:EmbeddedResource', $ns)) {
    $resourcePath = Join-Path $codeDirectory $resource.Include
    if (!(Test-Path -LiteralPath $resourcePath)) { throw "缺少嵌入资源：$resourcePath" }
    $logicalName = $resource.SelectSingleNode('m:LogicalName', $ns)
    if (!$logicalName) { throw "资源缺少 LogicalName：$resourcePath" }
    $lines.Add('/resource:"' + $resourcePath + '",' + $logicalName.InnerText)
}
$frameworkSource = Join-Path $stagingDirectory 'TargetFramework.cs'
[IO.File]::WriteAllText($frameworkSource, '[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName=".NET Framework 4.7.2")]', [Text.UTF8Encoding]::new($false))
$lines.Add('"' + $frameworkSource + '"')
[IO.File]::WriteAllLines($responseFile, $lines, [Text.UTF8Encoding]::new($false))
& $compiler '/noconfig' ('@' + $responseFile)
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE；未覆盖上次成功产物。" }
$unexpected = Get-ChildItem -LiteralPath $outputDirectory -Filter '*.dll' | Where-Object Name -NE ($assemblyName + '.dll')
if ($unexpected) { throw ('Release 存在额外 DLL，请检查：' + ($unexpected.Name -join ', ')) }
Copy-Item -LiteralPath $stagedDll -Destination $outputDirectory -Force
Copy-Item -LiteralPath (Join-Path $stagingDirectory ($assemblyName + '.pdb')) -Destination $outputDirectory -Force
Get-FileHash -LiteralPath (Join-Path $outputDirectory ($assemblyName + '.dll')) -Algorithm SHA256
