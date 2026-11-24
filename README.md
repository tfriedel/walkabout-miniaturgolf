# Walkabout Minigolf – standard lanes mod

**Making-of with pictures: https://tfriedel.github.io/walkabout-miniaturgolf/**

A mod for the PC version of *Walkabout Mini Golf* that builds the 18 standard lanes of German/European tournament
Miniaturgolf ("Eternit") at real size, in a small city-garden course, playable in VR. The idea is to practise at home
for real courses. Unofficial fan project, not affiliated with Mighty Coconut. You need to own the game; no game files
are included here.

Status: all 18 lanes are playable and can be holed out; lane dimensions follow the official norm drawings, several
obstacle heights are estimates from photos (each lane file says which). Ball speed is not calibrated against a real
putter yet. There is no packaged release; build it yourself as described below.

Goal: practise German/European standard minigolf (Eternit / Miniaturgolf and Beton / Minigolf) in VR by building the standard lanes inside *Walkabout Mini Golf* (Unity 2022.3.62f2, IL2CPP).

## Layout

- `WalkaboutDesktop/` – BepInEx 6 (IL2CPP) plugin
  - desktop controls without a headset (mouse look, WASD, aim line, putting)
  - builds lanes at runtime from JSON and re-targets the game's current hole (tee + cup) onto them
  - follows the game's hole changes (next lane file, or repeat the same lane)
  - command file `BepInEx\walkabout_cmd.txt` for automated tests
  - park mode (`Park.cs`): own minigolf park on top of a loaded course (scenery hidden, sky kept)
  - ball physics on our lanes (`BallPhysics.cs`): braking a = rollDecel + rollDrag·v replaces the game's angular drag;
    border/obstacle bounce via physics materials. Current values are estimates, not yet calibrated.
- `lanes/` – lane definitions (JSON, lane coordinates: x lateral, y up, z along the lane, metres)
- `docs/lane-specs.md` – research summary of the official lane norms with sources
- `tools/wg.ps1` – test driver: start/stop/restart the game, send commands, load courses, screenshots, log

## Build

```
cd WalkaboutDesktop
dotnet build -c Release
```

Builds against the game's BepInEx interop assemblies (set the environment variable `WALKABOUT_GAME` to your game folder, or pass `-p:GameDir=...`; the default is the Steam location) and deploys the DLL plus `lanes/` into `BepInEx\plugins\WalkaboutDesktop`. The DLL can only be replaced while the game is closed; lane files hot-reload (F5).

## Keys (in game)

| Key | Action |
|---|---|
| Right mouse + move | look |
| WASD (Shift) | walk (faster) |
| Q / E (Shift) | turn aim line (fine) |
| V / Z | aim = view direction / straight down the lane |
| G (hold, release) | putt |
| B | stand behind the ball |
| R / Backspace | reset ball to tee |
| F2 | build the park: hides the loaded course, lays out all lane files on a lawn, hole n = lane n |
| F4 / F5 | pick / build a single lane (above the current hole, or replace the current park lane) |
| P | lane physics: custom / game default |
| 1/2, 3/4, 5/6 | constant braking −/+, speed braking −/+, border bounce −/+ |
| M / N | music / ambient sound on/off (both off by default) |
| F3 | after holing out: sequence / repeat / off |
| F9 / F10 | pick / load course |
| F6 | hit method |
| C | desktop camera on/off |
| H | HUD |

## Licence

MIT, see `LICENSE`. This covers the code and lane files in this repository. The reference photos and norm drawings shown on the making-of page belong to their owners and are not covered.
