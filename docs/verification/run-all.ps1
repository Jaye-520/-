param([switch]$Build)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$verificationDirectory = $PSScriptRoot
$started = Get-Date

# 脚本卫生：受维护的 .ps1 必须是带 BOM 的 UTF-8，且能被本机 PowerShell 解析。
# 兼容 Windows PowerShell 5.1：其默认 ANSI 读取无 BOM 中文脚本可能语法失败。
Write-Host '== 脚本卫生（BOM + 解析）=='
$maintained = @((Join-Path $projectDirectory 'build.ps1'))
$maintained += Get-ChildItem -LiteralPath $verificationDirectory -Filter '*.ps1' -File | ForEach-Object { $_.FullName }
$hygieneFailures = 0
foreach ($script in $maintained) {
    $name = $script.Substring($projectDirectory.Length + 1)
    $bytes = [IO.File]::ReadAllBytes($script)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $tokens = $null; $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$errors)
    if (-not $hasBom) { $hygieneFailures++; Write-Host ("  FAIL  {0}：缺少 UTF-8 BOM" -f $name) }
    if ($errors.Count -gt 0) { $hygieneFailures++; Write-Host ("  FAIL  {0}：{1}" -f $name, $errors[0].Message) }
}
if ($hygieneFailures -eq 0) { Write-Host ("  OK    {0} 个脚本均有 BOM 且可解析" -f $maintained.Count) }

# 工程一致性：手册要求“新增源码必须登记 csproj 并在代码导航表登记职责”，这里替它守门。
Write-Host ''
Write-Host '== 工程一致性（csproj ↔ 源码 ↔ 代码导航表）=='
$consistencyFailures = 0
$codeDirectory = Join-Path $projectDirectory 'ModProject\ModCode\ModMain'
try {
    $projectXml = [xml](Get-Content -LiteralPath (Join-Path $codeDirectory 'ModMain.csproj') -Raw)
    $ns = [Xml.XmlNamespaceManager]::new($projectXml.NameTable)
    $ns.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')
    $declared = @($projectXml.SelectNodes('//m:Compile', $ns) | ForEach-Object { $_.Include -replace '\\', '/' })
    $onDisk = @(Get-ChildItem -LiteralPath $codeDirectory -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } |
        ForEach-Object { $_.FullName.Substring($codeDirectory.Length + 1) -replace '\\', '/' })
    $documented = @(Select-String -LiteralPath (Join-Path $projectDirectory 'docs\ENGINEERING.md') `
        -Pattern '^\|\s*([A-Za-z][\w./]*\.cs)\s*\|' | ForEach-Object { $_.Matches[0].Groups[1].Value })
    if ($documented.Count -eq 0) { $consistencyFailures++; Write-Host '  FAIL  代码导航表未解析到任何源码条目' }
    foreach ($item in (Compare-Object $declared $onDisk | Where-Object { $_.SideIndicator -eq '<=' })) {
        $consistencyFailures++; Write-Host ("  FAIL  csproj 登记但磁盘不存在：{0}" -f $item.InputObject) }
    foreach ($item in (Compare-Object $declared $onDisk | Where-Object { $_.SideIndicator -eq '=>' })) {
        $consistencyFailures++; Write-Host ("  FAIL  磁盘源码未登记 csproj：{0}" -f $item.InputObject) }
    foreach ($file in $onDisk) {
        if (-not ($documented | Where-Object { $file -like "*$_" })) {
            $consistencyFailures++; Write-Host ("  FAIL  未在手册代码导航表登记：{0}" -f $file) } }
    foreach ($name in $documented) {
        if (-not ($onDisk | Where-Object { $_ -like "*$name" })) {
            $consistencyFailures++; Write-Host ("  FAIL  手册登记但源码不存在：{0}" -f $name) } }
    if ($consistencyFailures -eq 0) { Write-Host ("  OK    {0} 个源码文件在 csproj 与手册中一致" -f $onDisk.Count) }
    # 版本号唯一来源：当前版本字面量只允许出现在 ModVersion.cs，避免页脚与日志版本漂移。
    $versionText = Get-Content -LiteralPath (Join-Path $codeDirectory 'ModVersion.cs') -Raw
    $versionMatch = [regex]::Match($versionText, 'Current\s*=\s*"([^"]+)"')
    if (-not $versionMatch.Success) { $consistencyFailures++; Write-Host '  FAIL  未能从 ModVersion.cs 解析出版本号' }
    else {
        $version = $versionMatch.Groups[1].Value
        $stray = @(Get-ChildItem -LiteralPath $codeDirectory -Recurse -Filter '*.cs' -File |
            Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' -and $_.Name -ne 'ModVersion.cs' } |
            Select-String -Pattern ('"' + $version) -SimpleMatch)
        foreach ($hit in $stray) {
            $consistencyFailures++
            Write-Host ("  FAIL  版本号 {0} 应只在 ModVersion.cs 定义，实际还出现在：{1}:{2}" -f $version, (Split-Path $hit.Path -Leaf), $hit.LineNumber) }
        if ($stray.Count -eq 0) { Write-Host ("  OK    版本号 {0} 只在 ModVersion.cs 中定义" -f $version) }
    }
    # 死代码：产品源码里 private/internal 的方法与字段，若全项目（含harness）只出现一次，说明无人调用。
    # 只声明后接 ( = ; 的成员才作候选，故类/结构/枚举声明与harness入口类不会误报；确有正当例外时加入白名单并写明理由。
    $deadWhitelist = @()
    $declareFiles = @(Get-ChildItem -LiteralPath $codeDirectory -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|\\Example\\' })
    $corpusFiles = @(Get-ChildItem -LiteralPath $codeDirectory -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' })
    $corpusFiles += @(Get-ChildItem -LiteralPath $verificationDirectory -Filter '*.cs' -File)
    $corpus = ($corpusFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
    $declaration = [regex]'(?m)(?:^|[;{}])\s*(?:private|internal)\s+(?:static\s+|readonly\s+|sealed\s+|const\s+)*[A-Za-z_][\w<>\[\],\.\?]*\s+([A-Za-z_]\w*)\s*(?:\(|=|;)'
    $deadCode = @()
    foreach ($file in $declareFiles) {
        foreach ($match in $declaration.Matches([IO.File]::ReadAllText($file.FullName))) {
            $member = $match.Groups[1].Value
            if ($member -in @('get', 'set', 'value', 'return', 'new', 'var', 'this', 'base')) { continue }
            if ($deadWhitelist -contains $member) { continue }
            if (([regex]::Matches($corpus, '\b' + [regex]::Escape($member) + '\b')).Count -le 1) {
                $deadCode += ("{0}:{1}" -f (Split-Path $file.Name -Leaf), $member) }
        }
    }
    foreach ($item in $deadCode) {
        $consistencyFailures++
        Write-Host ("  FAIL  疑似死代码（仅声明、无人调用）：{0}（删除它，或加入白名单并说明理由）" -f $item) }
    if ($deadCode.Count -eq 0) { Write-Host '  OK    产品源码无未使用的 private/internal 成员' }
    # 行尾一致性：工作区行尾必须与 .gitattributes 声明一致（*.cs/*.md=lf、*.ps1/*.sln=crlf）。
    # 否则同一提交在不同检出状态下会编译出不同哈希——2026-10-03实测：同一提交因BookView.cs为CRLF或LF而得到两个DLL哈希。
    $eolProblems = 0
    foreach ($line in (& git -C $projectDirectory ls-files --eol 2>$null)) {
        if ($line -notmatch '^i/(\S+)\s+w/(\S+)\s+attr/(.*?)\s{2,}(.+)$') { continue }
        $workEol = $Matches[2]; $attribute = $Matches[3]; $file = $Matches[4].Trim()
        if ($workEol -eq 'mixed') { $consistencyFailures++; $eolProblems++; Write-Host ("  FAIL  行尾混合：{0}" -f $file); continue }
        if ($workEol -eq 'none') { continue }
        if ($attribute -match 'eol=lf' -and $workEol -ne 'lf') { $consistencyFailures++; $eolProblems++; Write-Host ("  FAIL  行尾应为LF：{0}（当前{1}）" -f $file, $workEol) }
        if ($attribute -match 'eol=crlf' -and $workEol -ne 'crlf') { $consistencyFailures++; $eolProblems++; Write-Host ("  FAIL  行尾应为CRLF：{0}（当前{1}）" -f $file, $workEol) }
    }
    if ($eolProblems -eq 0) { Write-Host '  OK    工作区行尾与 .gitattributes 一致' }
} catch {
    $consistencyFailures++
    Write-Host ("  FAIL  一致性检查无法执行：{0}" -f $_.Exception.Message)
}

$buildFailure = $null
if ($Build) {
    Write-Host ''
    Write-Host '== 编译与部署（build.ps1，含 Release→调试包同步）=='
    try {
        & (Join-Path $projectDirectory 'build.ps1') | Out-Null
        Write-Host '  OK    编译通过，零警告零错误'
    } catch {
        $buildFailure = $_.Exception.Message
        Write-Host ("  FAIL  {0}" -f $buildFailure)
    }
}

# 每个检查独立进程运行：退出码是唯一权威判据，输出用于统计与定位。
# 计数同时接受 "PASS xxx" 与 "PASS: xxx" 两种历史写法。
Write-Host ''
Write-Host '== 托管检查 =='
$harnesses = Get-ChildItem -LiteralPath $verificationDirectory -Filter 'run-*.ps1' -File |
    Where-Object { $_.Name -ne 'run-all.ps1' -and $_.Name -ne 'run-mutations.ps1' } | Sort-Object Name
$results = @()
foreach ($harness in $harnesses) {
    $name = [IO.Path]::GetFileNameWithoutExtension($harness.Name)
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $harness.FullName 2>&1
    $exitCode = $LASTEXITCODE
    $passed = @($output | Select-String -Pattern '^PASS[:\s]').Count
    $failed = @($output | Select-String -Pattern '^FAIL[:\s]').Count
    $summary = @($output | Where-Object { $_ -match 'passed|TOTAL:' } | Select-Object -Last 1)
    $results += [pscustomobject]@{
        Check   = $name
        Passed  = $passed
        Failed  = $failed
        Exit    = $exitCode
        Summary = ($summary -join '')
    }
    if ($exitCode -ne 0) {
        Write-Host ("  FAIL  {0}（退出码 {1}）" -f $name, $exitCode)
        $output | Select-String -Pattern '^FAIL|Exception|error' | Select-Object -First 3 | ForEach-Object { Write-Host ("        " + $_.Line) }
    } else {
        Write-Host ("  OK    {0}：{1} 项" -f $name, $passed)
    }
}

$totalPassed = ($results | Measure-Object -Property Passed -Sum).Sum
$totalFailed = ($results | Measure-Object -Property Failed -Sum).Sum
$brokenExit = @($results | Where-Object { $_.Exit -ne 0 }).Count

Write-Host ''
Write-Host '== 汇总 =='
$results | Format-Table -AutoSize | Out-String | Write-Host
Write-Host ("检查脚本 {0} 个；通过 {1} 项；失败 {2} 项；非零退出 {3} 个；脚本卫生问题 {4} 个；一致性问题 {5} 个；耗时 {6:N1} 秒" -f `
    $results.Count, $totalPassed, $totalFailed, $brokenExit, $hygieneFailures, $consistencyFailures, ((Get-Date) - $started).TotalSeconds)

$problems = $totalFailed + $brokenExit + $hygieneFailures + $consistencyFailures
if ($buildFailure) { $problems++ }
if ($problems -gt 0) { Write-Host 'RESULT: FAIL'; exit 1 }
Write-Host 'RESULT: PASS'
exit 0
