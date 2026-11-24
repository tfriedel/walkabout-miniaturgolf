# Trace one test shot: prints t, x, y, z (lane coords of the shot's lane), speed.
#   trace.ps1 <lane> <speed> <aim> [-Sec 4] [-Dt 0.03] [-Z0 0] [-Z1 99]
param([int]$Lane, [string]$Speed, [string]$Aim = '0', [float]$Sec = 4, [string]$Dt = '0.03', [float]$Z0 = 0, [float]$Z1 = 99, [float]$XOff = 0)
$wg = Join-Path $PSScriptRoot 'wg.ps1'
$logFile = Join-Path $(if ($env:WALKABOUT_GAME) { $env:WALKABOUT_GAME } elseif ([Environment]::GetEnvironmentVariable('WALKABOUT_GAME', 'User')) { [Environment]::GetEnvironmentVariable('WALKABOUT_GAME', 'User') } else { 'C:\Program Files (x86)\Steam\steamapps\common\Walkabout Mini Golf' }) 'BepInEx\LogOutput.log'
$from = (Get-Content $logFile).Count
& $wg cmd "trylane $Lane $Speed $Aim $Sec" "track $Sec $Dt" | Out-Null
Start-Sleep -Seconds ($Sec + 1.5)
$inv = [Globalization.CultureInfo]::InvariantCulture
Get-Content $logFile | Select-Object -Skip $from | Where-Object { $_ -match '\[TRACK\]|\[TRY\]' } | ForEach-Object { $_ -replace '.*\[(TRACK|TRY)\] ', '' } | ForEach-Object {
    $p = $_ -split '\s+'
    if ($p.Length -gt 5 -and $p[0] -match '^[\d.]+$') {
        $z = [double]::Parse($p[3], $inv)
        if ($z -ge $Z0 -and $z -le $Z1) { '{0} x={1:F3} y={2} z={3} v={4}' -f $p[0], ([double]::Parse($p[1], $inv) - $XOff), $p[2], $p[3], $p[4] }
    } elseif ($_ -match '^lane') { $_ }
}
