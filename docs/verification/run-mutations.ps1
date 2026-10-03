# 变异测试：故意向产品源码植入真实缺陷，确认“应该抓到它的那个harness”确实会失败。
# 全绿只说明“当前没坏”，不说明“坏了会被发现”；本脚本用来验证安全网本身有牙齿。
# 安全约定：要求目标文件在git中干净；每个探针应用后必定还原（finally）；结束时再次核对工作区。
# 本脚本会临时修改产品源码，因此**不纳入run-all.ps1的常规检查**，只在需要时手动运行。
param([switch]$KeepGoing)
# 刻意用Continue而非Stop：子harness失败时会向stderr抛出异常文本，PowerShell 5.1会把原生命令的stderr
# 当作终止错误（NativeCommandError）从而中断整个脚本。这里的权威判据是子进程退出码，不是错误流。
$ErrorActionPreference = 'Continue'
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$codeDirectory = Join-Path $projectDirectory 'ModProject\ModCode\ModMain'
# 探针里的File是相对ModCode/ModMain的；git需要仓库根相对路径。
$repoPrefix = 'ModProject/ModCode/ModMain/'

$probes = @(
    @{ Name = '满状态目标值 -1'; File = 'Services\CharacterActions.cs'
       From = 'targets[i] = checked(f.baseValue + maxima[i].value - f.value);'
       To   = 'targets[i] = checked(f.baseValue + maxima[i].value - 1 - f.value);'
       Harness = 'run-character-save.ps1' },
    @{ Name = '保存失败也清 Pending'; File = 'Services\CharacterSave.cs'
       From = 'if (success) Pending = Math.Max(0, Pending - dirty);'
       To   = 'Pending = Math.Max(0, Pending - dirty);'
       Harness = 'run-character-save.ps1' },
    @{ Name = '入口 MapOffset 40→41'; File = 'UI\WorkshopEntrances.cs'
       From = 'private const float MapOffset = 40f, MapSize = 56f;'
       To   = 'private const float MapOffset = 41f, MapSize = 56f;'
       Harness = 'run-entrances.ps1' },
    @{ Name = '正文顶压住卷目区（分带重叠）'; File = 'UI\BookView.cs'
       From = 'private const float BodyTop = 124;'
       To   = 'private const float BodyTop = 100;'
       Harness = 'run-book-layout.ps1' },
    @{ Name = '不再拦截无效焦点目标'; File = 'UI\WindowInputScope.cs'
       From = 'return !invalid;'
       To   = 'return true;'
       Harness = 'run-input-scope.ps1' },
    @{ Name = '年龄/寿命换算除数 12→24'; File = 'Services\CharacterReader.cs'
       From = 'return (Read(value) / 12)'
       To   = 'return (Read(value) / 24)'
       Harness = 'run-character-reader.ps1' },
    @{ Name = '当前值/上限显示顺序互换'; File = 'Services\CharacterReader.cs'
       From = 'return Number(value) + " / " + Number(maximum);'
       To   = 'return Number(maximum) + " / " + Number(value);'
       Harness = 'run-character-reader.ps1' },
    @{ Name = '气运 type=3 不再归入逆天'; File = 'Services\CharacterFates.cs'
       From = 'category = item.type == 3 ? 2 : source;'
       To   = 'category = source;'
       Harness = 'run-character-fates.ps1' },
    @{ Name = '草稿键丢掉槽位 Slot'; File = 'Services\CharacterEditSession.cs'
       From = 'return Group + ":" + Index + ":" + Slot;'
       To   = 'return Group + ":" + Index;'
       Harness = 'run-character-editing.ps1' },
    @{ Name = '取消四纹理缓存'; File = 'UI\PaperSkin.cs'
       From = 'if (shared == null) shared = new PaperSkin();'
       To   = 'shared = new PaperSkin();'
       Harness = 'run-paper-skin.ps1' },
    @{ Name = '关闭时节流 0.5s→0s'; File = 'Core\WorkshopController.cs'
       From = 'nextIdleCheck = Time.unscaledTime + .5f;'
       To   = 'nextIdleCheck = Time.unscaledTime + 0f;'
       Harness = 'run-controller-idle.ps1' },
    @{ Name = '窗口释放不再解绑输入补丁'; File = 'UI\WorkshopWindow.cs'
       From = 'try { WindowInputScope.Detach(); }'
       To   = 'try { }'
       Harness = 'run-lifecycle.ps1' }
)

$started = Get-Date
Write-Host '== 变异测试（每组应被对应harness抓到）=='
$results = @()
foreach ($probe in $probes) {
    $relative = $repoPrefix + ($probe.File -replace '\\', '/')
    $path = Join-Path $codeDirectory $probe.File
    $dirty = (& git -C $projectDirectory status --porcelain -- $relative 2>$null)
    if ($dirty) {
        $results += [pscustomobject]@{ Probe = $probe.Name; Harness = $probe.Harness; Result = '跳过：目标文件有未提交改动'; Exit = '-'; Pass = '-' }
        continue
    }
    $original = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
    if (-not $original.Contains($probe.From)) {
        $results += [pscustomobject]@{ Probe = $probe.Name; Harness = $probe.Harness; Result = '植入点未匹配（源码已变，请更新探针）'; Exit = '-'; Pass = '-' }
        continue
    }
    [IO.File]::WriteAllText($path, $original.Replace($probe.From, $probe.To), [Text.UTF8Encoding]::new($false))
    try {
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $probe.Harness) 2>&1
        $exitCode = $LASTEXITCODE
        $passed = @($output | Select-String -Pattern '^PASS[:\s]').Count
    } finally {
        git -C $projectDirectory checkout -- $relative 2>&1 | Out-Null
    }
    $caught = $exitCode -ne 0
    $results += [pscustomobject]@{
        Probe = $probe.Name; Harness = $probe.Harness
        Result = if ($caught) { '已抓到' } else { '未抓到 ← 覆盖缺口' }
        Exit = $exitCode; Pass = $passed
    }
    if (-not $caught -and -not $KeepGoing) { Write-Host ("  出现未抓到的探针：{0}；继续检查其余探针。" -f $probe.Name) }
}

Write-Host ''
$results | Format-Table -AutoSize
$gaps = @($results | Where-Object { $_.Result -like '未抓到*' }).Count
$invalid = @($results | Where-Object { $_.Result -like '跳过*' -or $_.Result -like '植入点未匹配*' }).Count
$caughtCount = @($results | Where-Object { $_.Result -eq '已抓到' }).Count
Write-Host ("探针 {0} 组：已抓到 {1}；覆盖缺口 {2}；无法执行 {3}；耗时 {4:N1} 秒" -f `
    $results.Count, $caughtCount, $gaps, $invalid, ((Get-Date) - $started).TotalSeconds)

Write-Host ''
Write-Host '== 工作区核对 =='
$leftover = @($probes | ForEach-Object { & git -C $projectDirectory status --porcelain -- ($repoPrefix + ($_.File -replace '\\', '/')) 2>$null } | Where-Object { $_ })
if ($leftover) { Write-Host '  探针目标仍有未还原改动，请手动检查：'; $leftover | ForEach-Object { Write-Host ("    " + $_) } }
else { Write-Host '  OK    所有探针目标均已还原' }

if ($gaps -gt 0 -or $invalid -gt 0 -or $leftover) { Write-Host 'RESULT: FAIL'; exit 1 }
Write-Host 'RESULT: PASS'
exit 0
