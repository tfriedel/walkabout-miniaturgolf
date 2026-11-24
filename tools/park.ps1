# Restart the game (build + deploy), load a course, wait until it is ready and build the minigolf park.
param([string]$Course = 'Tiki_Easy', [switch]$NoRestart)
$wg = Join-Path $PSScriptRoot 'wg.ps1'
if (-not $NoRestart) { & $wg restart 2>&1 | Select-String 'Fehler|error|started|exited' | Select-Object -Last 3 }
& $wg cmd "load $Course" | Out-Null
foreach ($i in 1..40) {
    Start-Sleep 3
    $s = & $wg cmd 'state' | Select-String 'state:' | Select-Object -Last 1
    if ("$s" -match "scene=$Course loading=False") { break }
    if ($i % 5 -eq 0 -and "$s" -match 'scene=MainMenu loading=False') { & $wg cmd "load $Course" | Out-Null }
}
Start-Sleep 5
& $wg cmd 'park' 'hud off' | Select-String 'Park built|xception' | Select-Object -Last 2
