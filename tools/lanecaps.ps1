# Screenshots of park lanes from fixed viewpoints, for comparing with photos and the norm drawings.
#   lanecaps.ps1 -Out <dir> [-Lanes 1,2,...]      files: g<NN>_top.png (tee left, like the norm drawing), _tee, _side, _back
param([string]$Out, [int[]]$Lanes = (1..18))
$wg = Join-Path $PSScriptRoot 'wg.ps1'
New-Item -ItemType Directory -Force $Out | Out-Null
& $wg cmd 'hud off' | Out-Null
foreach ($l in $Lanes) {
    $n = '{0:D2}' -f $l
    $views = @{ top = "lanecam $l 0 3.1 -90 89 6.0"; tee = "lanecam $l 0 -0.9 0 30 2.0"; side = "lanecam $l 2.6 3.3 -90 28 1.6"; back = "lanecam $l 0.5 7.6 -176 28 1.9" }
    foreach ($k in $views.Keys) {
        & $wg cmd $views[$k] | Out-Null; Start-Sleep -Milliseconds 700
        & $wg shot (Join-Path $Out "g${n}_$k.png") | Out-Null
    }
}
& $wg cmd 'lanecam 1 0 0 0 30 1.6' | Out-Null
"done"
