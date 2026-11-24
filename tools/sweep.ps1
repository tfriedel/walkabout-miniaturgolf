# Find a successful shot for each park lane with the plugin's "trylane" command.
#   sweep.ps1 [-Lanes 1,2,3] [-Out file]
# Phase 1: straight, speeds 2.5..7.5. Phase 2: aim -15..15 deg at a few speeds. Stops at the first "IN" per lane.
param([int[]]$Lanes = (1..18), [string]$Out = "$PSScriptRoot\..\docs\sweep-results.txt", [float]$Wait = 7)

$wg = Join-Path $PSScriptRoot 'wg.ps1'
function Try-Shot([int]$lane, [string]$speed, [string]$aim) {
    & $wg cmd "trylane $lane $speed $aim $Wait" | Out-Null
    Start-Sleep -Seconds ($Wait + 1)
    $r = & $wg log 2 '\[TRY\]' | Select-Object -Last 1
    return ($r -replace '.*\[TRY\] ', '')
}

$lines = @("# lane sweep $(Get-Date -Format s)")
foreach ($lane in $Lanes) {
    $found = $null; $tries = 0; $best = $null
    $plan = @()
    foreach ($s in '2.5','3.0','3.5','4.0','4.5','5.0','5.5','6.0','7.0','8.0','9.0','10.0') { $plan += ,@($s, '0') }
    foreach ($a in '3','-3','6','-6','10','-10','15','-15') { foreach ($s in '3.0','4.0','5.0','6.5') { $plan += ,@($s, $a) } }
    foreach ($p in $plan) {
        $r = Try-Shot $lane $p[0] $p[1]; $tries++
        if ($r -match ' IN$') { $found = $r; break }
        if ($r -match 'distCup=([\d.,]+)') { $d = [double]($matches[1] -replace ',', '.'); if (-not $best -or $d -lt $best[0]) { $best = @($d, $r) } }
    }
    $line = if ($found) { "OK    $found  ($tries tries)" } else { "FAIL  lane ${lane}: no hit in $tries tries; closest: $($best[1])" }
    $line; $lines += $line
}
& $wg cmd "hud on" | Out-Null
$lines | Set-Content $Out -Encoding UTF8
