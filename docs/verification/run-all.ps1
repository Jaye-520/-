param([switch]$Build)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$verificationDirectory = $PSScriptRoot
$started = Get-Date

# 脚本卫生：受维护的 .ps1 必须是带 BOM 的 UTF-8，且能被本机 PowerShell 解析。
# 本机只有 PowerShell 5.1，按系统 ANSI 读取无 BOM 的中文脚本会直接语法失败。
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
    Where-Object { $_.Name -ne 'run-all.ps1' } | Sort-Object Name
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
Write-Host ("检查脚本 {0} 个；通过 {1} 项；失败 {2} 项；非零退出 {3} 个；脚本卫生问题 {4} 个；耗时 {5:N1} 秒" -f `
    $results.Count, $totalPassed, $totalFailed, $brokenExit, $hygieneFailures, ((Get-Date) - $started).TotalSeconds)

$problems = $totalFailed + $brokenExit + $hygieneFailures
if ($buildFailure) { $problems++ }
if ($problems -gt 0) { Write-Host 'RESULT: FAIL'; exit 1 }
Write-Host 'RESULT: PASS'
exit 0
