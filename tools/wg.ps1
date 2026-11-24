# Walkabout game driver for automated tests.
#   wg.ps1 start                 start the game (if not running) and wait until the plugin is loaded
#   wg.ps1 stop                  close the game
#   wg.ps1 restart               stop, deploy (build), start
#   wg.ps1 cmd "<line>" ...      send commands to the plugin (one per argument)
#   wg.ps1 shot <file.png>       screenshot of the game window
#   wg.ps1 log [n] [regex]       last n plugin log lines (optionally filtered)
#   wg.ps1 wait <regex> [sec]    wait until a new log line matches
param([Parameter(Position = 0)][string]$Action, [Parameter(Position = 1, ValueFromRemainingArguments = $true)][string[]]$Rest)

# game folder: environment variable WALKABOUT_GAME, else the default Steam location
$Game = $(if ($env:WALKABOUT_GAME) { $env:WALKABOUT_GAME } elseif ([Environment]::GetEnvironmentVariable('WALKABOUT_GAME', 'User')) { [Environment]::GetEnvironmentVariable('WALKABOUT_GAME', 'User') } else { 'C:\Program Files (x86)\Steam\steamapps\common\Walkabout Mini Golf' })
$Log = Join-Path $Game 'BepInEx\LogOutput.log'
$CmdFile = Join-Path $Game 'BepInEx\walkabout_cmd.txt'
$Proj = Join-Path $PSScriptRoot '..\WalkaboutDesktop'

function Get-LogLines { if (Test-Path $Log) { Get-Content $Log -ErrorAction SilentlyContinue } else { @() } }

function Wait-Log([string]$Regex, [int]$Sec = 60, [int]$From = -1) {
    if ($From -lt 0) { $From = (Get-LogLines).Count }
    $t = Get-Date
    while (((Get-Date) - $t).TotalSeconds -lt $Sec) {
        $l = Get-LogLines
        if ($l.Count -gt $From) {
            $hit = $l[$From..($l.Count - 1)] | Where-Object { $_ -match $Regex } | Select-Object -First 1
            if ($hit) { return $hit }
        }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

function Start-Game {
    if (Get-Process WalkaboutMiniGolf -ErrorAction SilentlyContinue) { "already running"; return }
    $p = Start-Process (Join-Path $Game 'WalkaboutMiniGolf.exe') -WorkingDirectory $Game -PassThru
    $t = Get-Date
    # The log is recreated on start; wait for our plugin, then for the menu scene to settle.
    while (((Get-Date) - $t).TotalSeconds -lt 180) {
        Start-Sleep 2
        if ($p.HasExited) { "game exited ($($p.ExitCode))"; return }
        if ((Get-LogLines) -match 'Walkabout Desktop Test\] Loaded') { break }
    }
    Start-Sleep 12
    "started pid $($p.Id) after $([int]((Get-Date) - $t).TotalSeconds)s"
}

function Stop-Game {
    $p = Get-Process WalkaboutMiniGolf -ErrorAction SilentlyContinue
    if (-not $p) { "not running"; return }
    $p.CloseMainWindow() | Out-Null
    if (-not $p.WaitForExit(8000)) { $p | Stop-Process -Force; $p.WaitForExit(5000) | Out-Null }
    "stopped"
}

function Send-Cmd([string[]]$Lines) {
    $from = (Get-LogLines).Count
    Set-Content -Path $CmdFile -Value $Lines -Encoding UTF8
    $t = Get-Date
    while ((Test-Path $CmdFile) -and ((Get-Date) - $t).TotalSeconds -lt 10) { Start-Sleep -Milliseconds 200 }
    if (Test-Path $CmdFile) { "command file not picked up (game not running / plugin not loaded?)"; return }
    Start-Sleep -Milliseconds 600
    $l = Get-LogLines
    if ($l.Count -gt $from) { $l[$from..($l.Count - 1)] | Where-Object { $_ -notmatch '\[TRACK\]' } }
}

function Save-Shot([string]$File) {
    Add-Type -AssemblyName System.Drawing
    if (-not ('WgWin' -as [type])) {
        Add-Type @'
using System; using System.Runtime.InteropServices;
public class WgWin { [StructLayout(LayoutKind.Sequential)] public struct R { public int L,T,Ri,B; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
 [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title); }
'@
    }
    # Per-monitor DPI aware (-4), otherwise a 150 % scaled display reports a smaller window and the capture is cropped.
    [WgWin]::SetThreadDpiAwarenessContext([IntPtr]-4) | Out-Null
    # The game process also owns the BepInEx console, so pick the Unity window by class.
    $h = [WgWin]::FindWindow('UnityWndClass', [NullString]::Value)
    if ($h -eq [IntPtr]::Zero) { throw "game window not found" }
    $r = New-Object WgWin+R; [WgWin]::GetWindowRect($h, [ref]$r) | Out-Null
    $bmp = New-Object Drawing.Bitmap ($r.Ri - $r.L), ($r.B - $r.T)
    $g = [Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc(); [WgWin]::PrintWindow($h, $dc, 2) | Out-Null; $g.ReleaseHdc($dc)
    # Downscale to ~1280 px wide to keep screenshots light.
    $f = [Math]::Max(1, $bmp.Width / 1280.0)
    $w = [int]($bmp.Width / $f); $hh = [int]($bmp.Height / $f)
    $small = New-Object Drawing.Bitmap $w, $hh
    $g2 = [Drawing.Graphics]::FromImage($small); $g2.InterpolationMode = 'HighQualityBicubic'; $g2.DrawImage($bmp, 0, 0, $w, $hh)
    $dir = Split-Path $File; if ($dir) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $small.Save($File, [Drawing.Imaging.ImageFormat]::Png)
    "saved $File"
}

function Load-Course([string]$Name) {
    for ($try = 1; $try -le 3; $try++) {
        Send-Cmd @("load $Name") | Out-Null
        $t = Get-Date
        while (((Get-Date) - $t).TotalSeconds -lt 45) {
            Start-Sleep 3
            $s = Send-Cmd @('state') | Select-Object -Last 1
            if ($s -match "scene=$Name loading=False hole=#\d") { "loaded $Name (try $try)"; Start-Sleep 3; return }
        }
    }
    "FAILED to load $Name"
}

switch ($Action) {
    'load' { Load-Course $Rest[0] }
    'start' { Start-Game }
    'stop' { Stop-Game }
    'restart' {
        Stop-Game
        Push-Location $Proj
        dotnet build -c Release -nologo -v m "-p:GameDir=$Game" 2>&1 | Where-Object { $_ -match 'error|Fehler|Deployed' -and $_ -notmatch 'CS1668' }
        Pop-Location
        Start-Game
    }
    'cmd' { Send-Cmd $Rest }
    'shot' { Save-Shot $Rest[0] }
    'log' {
        $n = if ($Rest.Count -gt 0) { [int]$Rest[0] } else { 30 }
        $l = Get-LogLines | Where-Object { $_ -match 'Walkabout Desktop|Error|Exception' }
        if ($Rest.Count -gt 1) { $l = $l | Where-Object { $_ -match $Rest[1] } }
        $l | Select-Object -Last $n
    }
    'wait' { $s = if ($Rest.Count -gt 1) { [int]$Rest[1] } else { 60 }; $x = Wait-Log $Rest[0] $s; if ($x) { $x } else { "timeout" } }
    default { "usage: wg.ps1 start|stop|restart|cmd|shot|log|wait" }
}

