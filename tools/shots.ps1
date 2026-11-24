# Fire test shots on a park lane and print where the ball ends up.
#   shots.ps1 <lane> "speed:aimDeg" ... [-Wait 9]
param([int]$Lane, [Parameter(ValueFromRemainingArguments = $true)][string[]]$Shots, [float]$Wait = 9)
$wg = Join-Path $PSScriptRoot 'wg.ps1'
foreach ($s in $Shots) {
    $p = $s.Split(':'); $aim = if ($p.Length -gt 1) { $p[1] } else { '0' }
    & $wg cmd "trylane $Lane $($p[0]) $aim $Wait" | Out-Null
    Start-Sleep -Seconds ($Wait + 1)
    (& $wg log 3 '\[TRY\]' | Select-Object -Last 1) -replace '.*\[TRY\] ', ''
}
