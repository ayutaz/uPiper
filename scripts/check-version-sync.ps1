#!/usr/bin/env pwsh
# uPiper バージョン同期検証（検証のみ・自動書換なし）
# 正本: Assets/uPiper/Editor/uPiperSetup.cs の PACKAGE_VERSION
# 終了コード: 0=整合 / 1=ドリフト検出 / 2=正本が読めない
$ErrorActionPreference = 'Stop'
$repo = (& git rev-parse --show-toplevel 2>$null)
if (-not $repo) { $repo = (Get-Location).Path }

$problems = [System.Collections.Generic.List[string]]::new()
function Add-Problem([string]$m) { $script:problems.Add($m) }

function Find-First($relPath, $pattern) {
    $full = Join-Path $repo $relPath
    if (-not (Test-Path $full)) { return $null }
    $i = 0
    foreach ($line in [System.IO.File]::ReadLines($full)) {
        $i++
        $m = [regex]::Match($line, $pattern)
        if ($m.Success) { return [pscustomobject]@{ File=$relPath; Line=$i; Value=$m.Groups[1].Value } }
    }
    return $null
}
function Find-All($relPath, $pattern) {
    $full = Join-Path $repo $relPath
    $res = [System.Collections.Generic.List[object]]::new()
    if (-not (Test-Path $full)) { return $res }
    $i = 0
    foreach ($line in [System.IO.File]::ReadLines($full)) {
        $i++
        foreach ($m in [regex]::Matches($line, $pattern)) {
            $res.Add([pscustomobject]@{ File=$relPath; Line=$i; Value=$m.Groups[1].Value })
        }
    }
    return $res
}

$src = Find-First 'Assets/uPiper/Editor/uPiperSetup.cs' 'PACKAGE_VERSION\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"'
if (-not $src) {
    Write-Host "ERROR: 正本 PACKAGE_VERSION を Assets/uPiper/Editor/uPiperSetup.cs から抽出できません" -ForegroundColor Red
    exit 2
}
$canon = $src.Value
Write-Host ("CANONICAL  {0}:{1}  PACKAGE_VERSION = {2}" -f $src.File, $src.Line, $canon) -ForegroundColor Cyan

$checks = @(
    (Find-First 'Assets/uPiper/package.json' '"version"\s*:\s*"([0-9]+\.[0-9]+\.[0-9]+)"'),
    (Find-First 'CHANGELOG.md' '^##\s*\[([0-9]+\.[0-9]+\.[0-9]+)\]'),
    (Find-First 'README.md' '"com\.ayutaz\.upiper"\s*:\s*"([0-9]+\.[0-9]+\.[0-9]+)"'),
    (Find-First 'Assets/uPiper/Samples~/BasicTTSDemo/package.json' '"version"\s*:\s*"([0-9]+\.[0-9]+\.[0-9]+)"'),
    (Find-First 'Assets/uPiper/Samples~/BasicTTSDemo/package.json' '"com\.ayutaz\.upiper"\s*:\s*"([0-9]+\.[0-9]+\.[0-9]+)"')
)
foreach ($c in $checks) {
    if ($null -eq $c) { continue }
    $tag = if ($c.Value -eq $canon) { 'OK      ' } else { 'MISMATCH' }
    $line = "{0}  {1}:{2}  {3}" -f $tag, $c.File, $c.Line, $c.Value
    if ($c.Value -eq $canon) { Write-Host $line -ForegroundColor Green }
    else {
        Write-Host ("{0}  (expected {1})" -f $line, $canon) -ForegroundColor Yellow
        Add-Problem ("{0}:{1} = {2} (expected {3})" -f $c.File, $c.Line, $c.Value, $canon)
    }
}

$manifestTags = Find-All 'Packages/manifest.json' 'dot-net-g2p\.git\?path=[^#"]+#v([0-9]+\.[0-9]+\.[0-9]+)'
$readmeTags   = Find-All 'README.md'             'dot-net-g2p\.git\?path=[^#"]+#v([0-9]+\.[0-9]+\.[0-9]+)'
$manifestSet = $manifestTags | Select-Object -Expand Value -Unique
$readmeSet   = $readmeTags   | Select-Object -Expand Value -Unique
if ($manifestSet.Count -gt 1) {
    Add-Problem ("Packages/manifest.json 内の dot-net-g2p タグが不一致: {0}" -f ($manifestSet -join ', '))
    Write-Host ("MISMATCH  Packages/manifest.json dot-net-g2p tags: {0}" -f ($manifestSet -join ', ')) -ForegroundColor Yellow
}
if ($readmeSet.Count -gt 1) {
    Add-Problem ("README.md 内の dot-net-g2p タグが不一致: {0}" -f ($readmeSet -join ', '))
}
if ($manifestSet.Count -ge 1 -and $readmeSet.Count -ge 1) {
    $mv = ($manifestSet | Sort-Object | Select-Object -First 1)
    $rv = ($readmeSet   | Sort-Object | Select-Object -First 1)
    if ($mv -ne $rv) {
        Add-Problem ("dot-net-g2p タグが manifest(#v{0}) と README(#v{1}) で乖離（README のインストール手順が古い可能性）" -f $mv, $rv)
        Write-Host ("MISMATCH  dot-net-g2p: manifest=#v{0} README=#v{1}" -f $mv, $rv) -ForegroundColor Yellow
    } else {
        Write-Host ("OK        dot-net-g2p tags consistent: #v{0}" -f $mv) -ForegroundColor Green
    }
}

Write-Host ""
if ($problems.Count -eq 0) {
    Write-Host "[version-check] 全バージョン整合 (canonical $canon)" -ForegroundColor Green
    exit 0
}
Write-Host ("[version-check] ドリフト {0} 件:" -f $problems.Count) -ForegroundColor Red
foreach ($p in $problems) { Write-Host ("  - {0}" -f $p) -ForegroundColor Red }
Write-Host "正本は Assets/uPiper/Editor/uPiperSetup.cs:22 PACKAGE_VERSION。/version-check で修正候補を確認してください。" -ForegroundColor Red
exit 1