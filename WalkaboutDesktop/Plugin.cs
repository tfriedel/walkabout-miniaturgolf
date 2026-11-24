using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Mighty.Balls;
using Mighty.Modifiers.ToolModifiers;
using UnityEngine;
using UnityEngine.InputSystem;
using WPlayer = FarBridge.Worlds.Player;

namespace WalkaboutDesktop;

[BepInPlugin(Guid, "Walkabout Desktop Test", "0.2.0")]
public class Plugin : BasePlugin
{
    public const string Guid = "local.walkabout.desktoptest";

    internal static ManualLogSource L;
    internal static ConfigEntry<float> MaxSpeed;
    internal static ConfigEntry<float> ChargeSeconds;
    internal static ConfigEntry<float> WarpBackDistance;
    internal static ConfigEntry<string> HitMethod;
    internal static ConfigEntry<float> LaneHeight;
    internal static ConfigEntry<string> LanesDir;
    internal static ConfigEntry<float> EyeHeight;
    internal static ConfigEntry<float> MouseSensitivity;
    internal static ConfigEntry<float> WalkSpeed;
    internal static ConfigEntry<bool> DesktopCamera;
    internal static ConfigEntry<string> LaneMode;
    internal static ConfigEntry<bool> MuteMusic;
    internal static ConfigEntry<bool> MuteAmbience;
    internal static ConfigEntry<bool> MuteAll;
    internal static ConfigEntry<bool> AutoPark;
    internal static ConfigEntry<bool> Shadows;
    internal static ConfigEntry<bool> GardenLayout;
    internal static ConfigEntry<bool> ForceDesktop;
    internal static ConfigEntry<bool> SimulateHeadset;

    public override void Load()
    {
        L = Log;
        MaxSpeed = Config.Bind("Putting", "MaxSpeed", 12f, "Ball speed (m/s) at full power. The power bar is progressive: half charge gives about a third of this.");
        ChargeSeconds = Config.Bind("Putting", "ChargeSeconds", 1.5f, "Seconds holding the putt key to reach full power.");
        HitMethod = Config.Bind("Putting", "HitMethod", "Putter",
            "How the ball is hit: Putter (game's putter, counts strokes) | Network (BallNetworkController.HitBall) | Rigidbody (set velocity directly). F6 cycles in-game.");
        WarpBackDistance = Config.Bind("Player", "WarpBackDistance", 0.6f, "How far behind the ball (m) the player is placed.");
        LaneHeight = Config.Bind("Lanes", "HeightAboveTee", 20f, "Custom lanes are built this many metres above the current hole's tee (keeps them clear of the course).");
        LanesDir = Config.Bind("Lanes", "Folder", "",
            "Folder with lane .json files. Empty = 'lanes' next to this plugin's DLL.");

        EyeHeight = Config.Bind("Camera", "EyeHeight", 1.6f, "Desktop camera eye height above the player's feet (m).");
        MouseSensitivity = Config.Bind("Camera", "MouseSensitivity", 0.15f, "Degrees per pixel of mouse movement while holding the right mouse button.");
        WalkSpeed = Config.Bind("Camera", "WalkSpeed", 1.5f, "WASD walking speed (m/s); hold Shift for 3x.");
        DesktopCamera = Config.Bind("Camera", "Enabled", true, "Drive the player camera with mouse/keyboard when no headset is tracking. C toggles in-game.");

        LaneMode = Config.Bind("Lanes", "Mode", "sequence",
            "What happens when the game moves on to the next hole after you built a lane: sequence (next lane file) | repeat (same lane again) | off. F3 cycles.");

        MuteMusic = Config.Bind("Audio", "MuteMusic", true, "Mute the game's background music (sound effects stay on). M toggles in-game.");
        AutoPark = Config.Bind("Park", "AutoPark", true, "Build the minigolf park automatically whenever a course has finished loading (no key needed - for VR). F2 still rebuilds it.");
        Shadows = Config.Bind("Park", "Shadows", true, "Switch on real-time sun shadows in the park (costs some performance)");
        GardenLayout = Config.Bind("Park", "GardenLayout", true, "Lay the 18 lanes out as an irregular garden instead of three rows");
        ForceDesktop = Config.Bind("Camera", "ForceDesktop", false, "Treat the session as desktop even if a headset is detected (for development with a headset plugged in).");
        SimulateHeadset = Config.Bind("Camera", "SimulateHeadset", false, "Testing only: behave as if a headset were in use (the mod leaves camera and sound to the game).");
        MuteAll = Config.Bind("Audio", "MuteAll", true, "Silence the whole game (everything, from the first frame). For development.");
        MuteAmbience = Config.Bind("Audio", "MuteAmbience", true, "Mute the game's ambient sound loops (waves, wind, ...). N toggles in-game.");

        AddComponent<DesktopController>();
        L.LogInfo("Loaded. G = putt, B = warp to ball, R = reset ball, F4/F5 = pick/build lane, F6 = hit method, F8 = DesktopPlayer, F9/F10 = pick/load course, H = HUD.");
    }

    internal static string LanesFolder =>
        !string.IsNullOrWhiteSpace(LanesDir.Value)
            ? LanesDir.Value
            : Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "lanes");

    internal static string CommandFile => Path.Combine(Paths.BepInExRootPath, "walkabout_cmd.txt");
}

public class DesktopController : MonoBehaviour
{
    public DesktopController(IntPtr ptr) : base(ptr) { }

    static readonly string[] Methods = { "Putter", "Network", "Rigidbody" };
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    float charge;
    bool charging;
    bool hud = true;
    string status = "";
    float statusUntil;
    List<string> scenes = new();
    int sceneIdx = -1;
    string[] laneFiles = Array.Empty<string>();
    int laneIdx = -1;
    string lanePath;
    float nextPoll;

    float trackUntil, trackInterval = 0.05f, nextTrack;
    LaneInstance tryLane; string tryInfo; float tryCheckAt = -1f;
    readonly List<(InPlaySurface ips, Hole hole)> tryIps = new();

    void CheckTry()
    {
        if (tryCheckAt < 0 || Time.unscaledTime < tryCheckAt) return;
        tryCheckAt = -1f;
        foreach (var (ips, h) in tryIps) if (ips != null) ips.hole = h;
        tryIps.Clear();
        var ball = Ball.instance; var l = tryLane;
        if (ball == null || l == null) return;
        var lp = l.ToLocal(ball.transform.position);
        var d = l.Def;
        float cupY = d.cupHeight ?? LaneGeometry.Height(d, d.cup[0], d.cup[1]);
        float dist = new Vector2(lp.x - d.cup[0], lp.z - d.cup[1]).magnitude;
        bool inCup = d.cupHole ? dist < 0.06f && lp.y < cupY - 0.01f : dist < d.targetRadius && Mathf.Abs(lp.y - cupY) < 0.15f;
        bool onLane = LaneGeometry.Inside(d, lp.x, lp.z);
        Plugin.L.LogInfo(string.Format(Inv, "[TRY] {0} -> end x={1:F2} y={2:F2} z={3:F2} distCup={4:F2} speed={5:F2} {6}",
            tryInfo, lp.x, lp.y, lp.z, dist, ball.rb.velocity.magnitude, inCup ? "IN" : onLane ? "on lane" : "OFF LANE"));
    }

    void TrackBall()
    {
        if (Time.unscaledTime > trackUntil || Time.unscaledTime < nextTrack) return;
        nextTrack = Time.unscaledTime + trackInterval;
        var b = Ball.instance;
        if (b == null) { Plugin.L.LogInfo("[TRACK] no ball"); return; }
        var w = b.transform.position;
        var l = LaneBuilder.Active ? Quaternion.Inverse(LaneBuilder.Rotation) * (w - LaneBuilder.Origin) : w;
        var h = Hole.activeHole;
        string hs = h == null ? "-" : $"{h.holeComplete} {h.strokes}";
        Plugin.L.LogInfo(string.Format(Inv, "[TRACK] {0:F2} {1:F3} {2:F3} {3:F3} {4:F2} {5} {6} {7} drag={8:F3} ang={9:F3} w={10:F1} surf={11}",
            Time.unscaledTime, l.x, l.y, l.z, b.rb.velocity.magnitude, b.gameObject.layer, b.inHole, hs,
            b.rb.drag, b.rb.angularDrag, b.rb.angularVelocity.magnitude, b.rollingSurfaceTag));
    }

    void Update()
    {
        bool mute = Plugin.MuteAll.Value && !HeadsetInUse();     // silence is for desktop development only
        if (mute && AudioListener.volume != 0f) AudioListener.volume = 0f;
        else if (!mute && AudioListener.volume == 0f) AudioListener.volume = 1f;
        Try("autopark", AutoParkCheck);
        Try("track", TrackBall);
        Try("try", CheckTry);
        Try("follow", FollowHoles);
        Try("target", CheckTargetLane);
        Try("vrreset", VrResetCheck);
        if (Time.unscaledTime >= nextMusicCheck)
        {
            nextMusicCheck = Time.unscaledTime + 1f;
            Try("music", ApplyMusicMute);
        }
        if (Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + 0.25f;
            Try("commands", PollCommands);
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        Try("F2", () => { if (kb.f2Key.wasPressedThisFrame) BuildPark(); });
        Try("F3", () => { if (kb.f3Key.wasPressedThisFrame) CycleLaneMode(); });
        Try("PageDown", () => { if (kb.pageDownKey.wasPressedThisFrame) GoToHole((Hole.activeHole?.holeNumber ?? 0) + 1); });
        Try("PageUp", () => { if (kb.pageUpKey.wasPressedThisFrame) GoToHole((Hole.activeHole?.holeNumber ?? 2) - 1); });
        Try("F4", () => { if (kb.f4Key.wasPressedThisFrame) NextLane(); });
        Try("F5", () => { if (kb.f5Key.wasPressedThisFrame) BuildLane(); });
        Try("F6", () => { if (kb.f6Key.wasPressedThisFrame) CycleMethod(); });
        Try("F8", () => { if (kb.f8Key.wasPressedThisFrame) ToggleDesktopPlayer(); });
        Try("F9", () => { if (kb.f9Key.wasPressedThisFrame) NextScene(); });
        Try("F10", () => { if (kb.f10Key.wasPressedThisFrame) LoadScene(null); });
        Try("B", () => { if (kb.bKey.wasPressedThisFrame) WarpToBall(); });
        Try("R", () =>
        {
            if (kb.rKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            {
                if (!LaneBuilder.Active) { Msg("Reset only works on a custom lane (F4/F5)"); return; }
                LaneBuilder.ResetBall();
                Msg("Ball reset to tee");
            }
        });
        Try("aim", () => UpdateAimKeys(kb));
        Try("phys", () => UpdatePhysicsKeys(kb));
        if (kb.hKey.wasPressedThisFrame) hud = !hud;
        if (kb.mKey.wasPressedThisFrame) { Plugin.MuteMusic.Value = !Plugin.MuteMusic.Value; Try("music", ApplyMusicMute); Msg("Music " + (Plugin.MuteMusic.Value ? "off" : "on")); }
        if (kb.nKey.wasPressedThisFrame) { Plugin.MuteAmbience.Value = !Plugin.MuteAmbience.Value; Try("music", ApplyMusicMute); Msg("Ambience " + (Plugin.MuteAmbience.Value ? "off" : "on")); }
        if (kb.cKey.wasPressedThisFrame) { Plugin.DesktopCamera.Value = !Plugin.DesktopCamera.Value; Msg("Desktop camera: " + (Plugin.DesktopCamera.Value ? "on" : "off")); }

        if (kb.gKey.wasPressedThisFrame) { charging = true; charge = 0f; }
        if (charging && kb.gKey.isPressed)
            charge = Mathf.Min(1f, charge + Time.unscaledDeltaTime / Mathf.Max(0.05f, Plugin.ChargeSeconds.Value));
        if (charging && kb.gKey.wasReleasedThisFrame)
        {
            charging = false;
            float p = charge;
            Try("Hit", () => Hit(PuttSpeed(p), AimDir(), null));
        }
    }

    // ---------- aiming ----------
    // Aim = base direction (lane forward on a custom lane, otherwise ball->cup) turned by aimYaw degrees.

    static float aimYaw;
    static LineRenderer aimLine;

    internal static void ResetAim() => aimYaw = 0f;

    static Vector3 AimBase()
    {
        if (LaneBuilder.Active) return LaneBuilder.Forward;
        var ball = Ball.instance; var h = Hole.activeHole;
        if (ball != null && h?.cupPosition != null)
        {
            var d = Vector3.ProjectOnPlane(h.cupPosition.position - ball.transform.position, Vector3.up);
            if (d.sqrMagnitude > 1e-4f) return d.normalized;
        }
        return CameraDirection();
    }

    static Vector3 AimDir() => Quaternion.AngleAxis(aimYaw, Vector3.up) * AimBase();

    void UpdateAimKeys(Keyboard kb)
    {
        float rate = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? 2f : 20f; // deg/s
        float dt = Time.unscaledDeltaTime;
        if (kb.qKey.isPressed || kb.leftArrowKey.isPressed) aimYaw -= rate * dt;
        if (kb.eKey.isPressed || kb.rightArrowKey.isPressed) aimYaw += rate * dt;
        if (kb.vKey.wasPressedThisFrame)
        {
            aimYaw = Vector3.SignedAngle(AimBase(), CameraDirection(), Vector3.up);
            Msg($"Aim set to view direction ({aimYaw:F1} deg)");
        }
        if (kb.zKey.wasPressedThisFrame || kb.yKey.wasPressedThisFrame) { ResetAim(); Msg("Aim reset"); }
        DrawAimLine();
    }

    void DrawAimLine()
    {
        var ball = Ball.instance;
        bool show = ball != null; // independent of the HUD (tests hide the HUD for screenshots)
        if (show)
        {
            try { show = ball.rb.velocity.magnitude < 0.05f; } catch { }
        }
        if (aimLine == null)
        {
            if (!show) return;
            var go = new GameObject("DesktopAimLine");
            UnityEngine.Object.DontDestroyOnLoad(go);
            aimLine = go.AddComponent<LineRenderer>();
            aimLine.positionCount = 2;
            aimLine.startWidth = aimLine.endWidth = 0.006f;
            aimLine.material = LaneBuilder.MakeMaterial(new[] { 1f, 0.85f, 0.1f });
            aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        aimLine.enabled = show;
        if (!show) return;
        var p = ball.transform.position + Vector3.up * 0.003f;
        aimLine.SetPosition(0, p);
        aimLine.SetPosition(1, p + AimDir() * 1.5f);
    }

    // ---------- desktop camera (no headset) ----------

    /// Progressive power curve: fine control for normal putts, full power for the jump ramp.
    static float PuttSpeed(float p) => Plugin.MaxSpeed.Value * Mathf.Pow(Mathf.Clamp01(p), 1.6f);
    static float yaw, pitch = 20f;
    static float flyHeight;   // extra camera height above eye height (flying)
    static bool flyMode;
    static bool camDiagnosed;

    static Vector3? pendingHeading;

    /// After a warp: look along the given world heading (default: lane / aim direction), slightly down at the ball.
    /// Applied in the next camera update, relative to whatever rotation the rig ended up with.
    internal static void ResetView(Vector3? heading = null)
    {
        pitch = 40f;
        pendingHeading = heading ?? (LaneBuilder.Active ? LaneBuilder.Forward : AimDir());
        pendingUntil = Time.unscaledTime + 0.6f;
    }

    static float pendingUntil;
    static float putterGain = 1.7f;
    static bool exactPending;

    void LateUpdate() => Try("camera", UpdateDesktopCamera);

    void FixedUpdate()
    {
        try { BallPhysics.FixedStep(); }
        catch (Exception e) { if (!physErrLogged) { physErrLogged = true; Plugin.L.LogError("physics: " + e); } }
        if (Time.unscaledTime - lastHitTime < 0.4f)
        {
            // The game's own gain on putter hits is not constant (1.7 and 1.5 were both measured), so the keyboard putt
            // sets the exact requested speed as soon as the ball has left, and remembers the gain it saw.
            try
            {
                var rb = Ball.instance.rb; float sp = rb.velocity.magnitude;
                if (exactPending && sp > 0.2f && lastRequested > 0.2f)
                {
                    exactPending = false;
                    float seen = sp / lastRequested * putterGain;
                    if (seen > 0.8f && seen < 3f) putterGain = seen;
                    rb.velocity = rb.velocity / sp * lastRequested;
                }
            }
            catch { }
            try { launchSpeed = Mathf.Max(launchSpeed, Ball.instance.rb.velocity.magnitude); } catch { }
            if (!launchLogged && Time.unscaledTime - lastHitTime > 0.25f) { launchLogged = true; Plugin.L.LogInfo(string.Format(Inv, "[LAUNCH] requested {0:F2} m/s -> ball {1:F2} m/s (game putter gain {2:F2})", lastRequested, launchSpeed, putterGain)); }
        }
    }

    bool physErrLogged;
    float lastHitTime = -10f, launchSpeed, lastRequested; bool launchLogged = true;

    void UpdatePhysicsKeys(Keyboard kb)
    {
        bool changed = false;
        if (kb.pKey.wasPressedThisFrame) { BallPhysics.Enabled = !BallPhysics.Enabled; Msg("Lane physics: " + (BallPhysics.Enabled ? "custom" : "game default")); }
        if (kb.digit1Key.wasPressedThisFrame) { BallPhysics.RollDecel = Mathf.Max(0, BallPhysics.RollDecel - 0.05f); changed = true; }
        if (kb.digit2Key.wasPressedThisFrame) { BallPhysics.RollDecel += 0.05f; changed = true; }
        if (kb.digit3Key.wasPressedThisFrame) { BallPhysics.RollDrag = Mathf.Max(0, BallPhysics.RollDrag - 0.05f); changed = true; }
        if (kb.digit4Key.wasPressedThisFrame) { BallPhysics.RollDrag += 0.05f; changed = true; }
        if (kb.digit5Key.wasPressedThisFrame) { LaneBuilder.SetBounce(Mathf.Max(0, LaneBuilder.BandBounce - 0.05f), Mathf.Max(0, LaneBuilder.ObstacleBounce - 0.05f)); changed = true; }
        if (kb.digit6Key.wasPressedThisFrame) { LaneBuilder.SetBounce(Mathf.Min(1, LaneBuilder.BandBounce + 0.05f), Mathf.Min(1, LaneBuilder.ObstacleBounce + 0.05f)); changed = true; }
        if (changed) Msg(PhysicsSummary());
    }

    static string PhysicsSummary() =>
        $"braking {BallPhysics.RollDecel:F2} m/s² + {BallPhysics.RollDrag:F2}·v   bounce {LaneBuilder.BandBounce:F2}";

    // ---------- headset ----------
    static readonly List<Behaviour> disabledPoseDrivers = new();
    static bool vrMode, vrLogged;
    static float nextVrCheck;

    /// True while a headset is running and worn (or cannot tell us whether it is worn). Then the mod keeps its hands off
    /// the camera and the sound: the game's own VR rig is in charge.
    static Transform desktopCam; static Vector3 desktopCamPos; static Quaternion desktopCamRot;   // eye camera pose before the desktop camera took over
    static float lastWorn; static bool everWorn;

    internal static bool HeadsetInUse()
    {
        if (Time.unscaledTime < nextVrCheck) return vrMode;
        nextVrCheck = Time.unscaledTime + 1f;
        bool now = false;
        try
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive)
            {
                now = true;
                // not worn (headset lying on the desk): stay in desktop mode, if the runtime can tell
                var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
                // (only after 10 s without the headset: the proximity sensor flickers, and before the first time it was worn
                // nothing needs to wait)
                bool off = head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out bool worn) && !worn;
                if (!off) { lastWorn = Time.unscaledTime; everWorn = true; }
                else if (!everWorn || Time.unscaledTime - lastWorn > 10f) now = false;
            }
        }
        catch { }
        if (Plugin.ForceDesktop.Value) now = false;
        if (Plugin.SimulateHeadset.Value) now = true;
        if (now != vrMode || !vrLogged)
        {
            vrLogged = true; vrMode = now;
            Plugin.L.LogInfo(now ? "Headset in use: desktop camera, keyboard view control and MuteAll are off" : "No headset in use: desktop camera active");
            if (now)
            {
                foreach (var b in disabledPoseDrivers) { try { if (b != null) b.enabled = true; } catch { } }
                disabledPoseDrivers.Clear();
                camDiagnosed = false;
                // undo what the desktop camera did to the eye camera, or the view stays tilted and offset from the head
                try { if (desktopCam != null) { desktopCam.localPosition = desktopCamPos; desktopCam.localRotation = desktopCamRot; } } catch { }
                desktopCam = null;
            }
        }
        return vrMode;
    }

    void UpdateDesktopCamera()
    {
        if (!Plugin.DesktopCamera.Value) return;
        if (HeadsetInUse()) return;
        var player = WPlayer.Instance;
        var cam = Camera.main;
        if (player == null || cam == null) return;
        var dp = player.GetComponent<FarBridge.Worlds.DesktopPlayer>();
        if (dp != null && dp.enabled) return; // the game's own desktop mode is driving

        if (!camDiagnosed) { camDiagnosed = true; DiagnoseCamera(player, cam); }

        if (pendingHeading.HasValue)
        {
            var parentFwd = cam.transform.parent != null ? cam.transform.parent.forward : Vector3.forward;
            var pf = Vector3.ProjectOnPlane(parentFwd, Vector3.up);
            var h = Vector3.ProjectOnPlane(pendingHeading.Value, Vector3.up);
            if (pf.sqrMagnitude > 1e-6f && h.sqrMagnitude > 1e-6f) yaw = Vector3.SignedAngle(pf, h, Vector3.up);
            // keep re-aligning for a moment in case the rig is rotated by a delayed warp
            if (Time.unscaledTime >= pendingUntil) pendingHeading = null;
        }

        float dt = Time.unscaledDeltaTime;
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            var d = mouse.delta.ReadValue();
            yaw += d.x * Plugin.MouseSensitivity.Value;
            pitch = Mathf.Clamp(pitch - d.y * Plugin.MouseSensitivity.Value, -85f, 85f);
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            var parentRot = cam.transform.parent != null ? cam.transform.parent.rotation : Quaternion.identity;
            var look = parentRot * Quaternion.Euler(0, yaw, 0);
            var fwd = Vector3.ProjectOnPlane(look * Vector3.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, fwd);
            var move = Vector3.zero;
            if (kb.wKey.isPressed) move += fwd;
            if (kb.sKey.isPressed) move -= fwd;
            if (kb.dKey.isPressed) move += right;
            if (kb.aKey.isPressed) move -= right;
            float speed = Plugin.WalkSpeed.Value * (kb.leftShiftKey.isPressed ? 3f : 1f);
            // Flying: F toggles fly mode (W/S then follow the view direction up and down as well);
            // Space / Left Ctrl raise and lower the camera at any time; T puts it back at eye height.
            if (kb.fKey.wasPressedThisFrame) { flyMode = !flyMode; Msg("Fly mode " + (flyMode ? "on (W/S follow the view, Space/Ctrl up/down, T = back to eye height)" : "off")); }
            if (kb.tKey.wasPressedThisFrame) { flyHeight = 0f; flyMode = false; Msg("Camera back at eye height"); }
            float vertical = 0f;
            if (kb.spaceKey.isPressed) vertical += 1f;
            if (kb.leftCtrlKey.isPressed) vertical -= 1f;
            if (flyMode)
            {
                float climb = -Mathf.Sin(pitch * Mathf.Deg2Rad);   // looking down = negative
                if (kb.wKey.isPressed) vertical += climb;
                if (kb.sKey.isPressed) vertical -= climb;
                speed *= 2f;
            }
            flyHeight = Mathf.Clamp(flyHeight + vertical * speed * dt, -Plugin.EyeHeight.Value + 0.05f, 60f);
            if (move != Vector3.zero)
                player.MoveFeetToPosition(player.FeetPositionApprox + move.normalized * speed * dt);
        }

        var t = cam.transform;
        if (desktopCam != t) { desktopCam = t; desktopCamPos = t.localPosition; desktopCamRot = t.localRotation; }
        t.localPosition = new Vector3(0f, Plugin.EyeHeight.Value + flyHeight, 0f);
        t.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void DiagnoseCamera(WPlayer player, Camera cam)
    {
        string Path(Transform x) { var s = x.name; while ((x = x.parent) != null) s = x.name + "/" + s; return s; }
        Plugin.L.LogInfo("[cam] main camera: " + Path(cam.transform));
        try { if (player.Head != null) Plugin.L.LogInfo("[cam] player head: " + Path(player.Head.transform)); } catch { }
        Plugin.L.LogInfo("[cam] player: " + Path(player.transform));
        foreach (var c in cam.GetComponents<Component>())
        {
            var n = c.GetIl2CppType().FullName;
            Plugin.L.LogInfo("[cam]   component " + n);
            if (n.Contains("TrackedPoseDriver"))
            {
                var b = c.TryCast<Behaviour>();
                if (b != null) { b.enabled = false; disabledPoseDrivers.Add(b); Plugin.L.LogInfo("[cam]   -> disabled (no headset)"); }
            }
        }
    }

    // ---------- command file (lets external tools drive tests) ----------
    // One command per line in BepInEx\walkabout_cmd.txt; the file is deleted once read. Results go to the log with prefix [CMD].
    //   load <SceneName> | lane [file.json] | reset | warp | state | method <Putter|Network|Rigidbody> | desktopplayer
    //   hit <speed m/s> [yawDeg]     yaw relative to lane forward (or camera if no lane)
    //   hitcup <speed m/s>           aim straight at the cup

    void PollCommands()
    {
        var f = Plugin.CommandFile;
        if (!File.Exists(f)) return;
        string[] lines;
        try { lines = File.ReadAllLines(f); File.Delete(f); }
        catch (IOException) { return; } // still being written
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            Plugin.L.LogInfo("[CMD] > " + line);
            Try("[CMD] " + line, () => Exec(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        }
    }

    void Exec(string[] a)
    {
        float F(int i, float def) => a.Length > i ? float.Parse(a[i], Inv) : def;
        switch (a[0].ToLowerInvariant())
        {
            case "load": LoadScene(a.Length > 1 ? a[1] : null); break;
            case "lane":
                if (a.Length > 1) lanePath = Path.IsPathRooted(a[1]) ? a[1] : Path.Combine(Plugin.LanesFolder, a[1]);
                BuildLane();
                break;
            case "reset": LaneBuilder.ResetBall(); Msg("Ball reset"); break;
            case "warp": WarpToBall(); break;
            case "method": Plugin.HitMethod.Value = a[1]; Msg("Hit method: " + a[1]); break;
            case "desktopplayer": ToggleDesktopPlayer(); break;
            case "hit":
            {
                var basis = AimBase();
                var dir = Quaternion.AngleAxis(F(2, 0f), Vector3.up) * basis;
                Hit(F(1, 2f), dir, null);
                break;
            }
            case "hitcup":
            {
                var ball = Ball.instance; var h = Hole.activeHole;
                if (ball == null || h?.cupPosition == null) { Msg("hitcup needs ball and hole"); break; }
                var d = Vector3.ProjectOnPlane(h.cupPosition.position - ball.transform.position, Vector3.up).normalized;
                Hit(F(1, 2f), d, null);
                break;
            }
            case "state": LogState(); break;
            case "track":
                trackUntil = Time.unscaledTime + F(1, 5f);
                trackInterval = F(2, 0.05f);
                nextTrack = 0f;
                Plugin.L.LogInfo("[TRACK] start  t  x  y  z (lane coords)  speed  layer  inHole  holeComplete  strokes");
                break;
            case "fly": flyHeight = F(1, 0f); Msg($"Camera height +{flyHeight} m"); break;
            case "view":
            {
                // view <x> <z> [yawDeg] [pitchDeg] [eyeHeight]   - stand at lane coords and look
                var p = WPlayer.Instance;
                if (p == null || !LaneBuilder.Active) { Msg("view needs a lane and a player"); break; }
                float yawDeg = F(3, 0f);
                var heading = Quaternion.AngleAxis(yawDeg, Vector3.up) * LaneBuilder.Forward;
                p.WarpToLocation(LaneBuilder.ToWorld(F(1, 0f), 0, F(2, 0f)), heading);
                ResetView(heading);
                pitch = F(4, 25f);
                if (a.Length > 5) Plugin.EyeHeight.Value = F(5, 1.6f);
                Msg($"View at ({F(1, 0f)}, {F(2, 0f)}) yaw {yawDeg} pitch {pitch}");
                break;
            }
            case "visible":
            {
                // Diagnostic: what is still being drawn that is not ours?
                string VPath(Transform x, int depth) { var parts = new List<string>(); for (; x != null; x = x.parent) parts.Insert(0, x.name); return string.Join("/", parts.Take(depth)); }
                var groups = new Dictionary<string, int>();
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Renderer>()))
                {
                    var r = o.TryCast<Renderer>();
                    if (r == null || !r.enabled || r.forceRenderingOff || Park.IsOurs(r.transform)) continue;
                    var key = $"{r.gameObject.scene.name}:{VPath(r.transform, 9)} @{r.transform.position} mesh={(r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null ? r.GetComponent<MeshFilter>().sharedMesh.name : "-")} [layer {r.gameObject.layer}] {r.GetIl2CppType().Name}";
                    groups[key] = groups.TryGetValue(key, out var n) ? n + 1 : 1;
                }
                foreach (var kv in groups.OrderByDescending(k => k.Value)) Plugin.L.LogInfo($"[CMD] vis {kv.Value} {kv.Key}");
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    Plugin.L.LogInfo($"[CMD] scene {sc.name} roots: " + string.Join(", ", sc.GetRootGameObjects().Select(g => g.name + (g.activeSelf ? "" : "(off)"))));
                }
                Plugin.L.LogInfo($"[CMD] skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")} fog={RenderSettings.fog} camMask={Camera.main.cullingMask} clear={Camera.main.clearFlags}");
                break;
            }
            case "cams":
                foreach (var c in Camera.allCameras)
                {
                    string Path(Transform x) { var s = x.name; while ((x = x.parent) != null) s = x.name + "/" + s; return s; }
                    var fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up);
                    float ang = LaneBuilder.Active ? Vector3.SignedAngle(LaneBuilder.Forward, fwd, Vector3.up) : float.NaN;
                    Plugin.L.LogInfo($"[CMD] cam {Path(c.transform)} enabled={c.enabled} depth={c.depth} target={(c.targetTexture != null ? c.targetTexture.name : "screen")} stereo={c.stereoTargetEye} pos={c.transform.position} yawVsLane={ang:F1} fov={c.fieldOfView:F0} mask={c.cullingMask}");
                    var pm = c.projectionMatrix;
                    Plugin.L.LogInfo($"[CMD]   proj m00={pm.m00:F3} m02={pm.m02:F3} m11={pm.m11:F3} m12={pm.m12:F3} physical={c.usePhysicalProperties} lensShift={c.lensShift} aspect={c.aspect:F3} rect={c.rect} pixel={c.pixelWidth}x{c.pixelHeight}");
                }
                break;
            case "fixcam":
            {
                var c = Camera.main;
                c.ResetProjectionMatrix();
                c.usePhysicalProperties = false;
                c.stereoTargetEye = StereoTargetEyeMask.None;
                c.ResetAspect();
                Msg("Camera projection reset");
                break;
            }
            case "simvr": Plugin.SimulateHeadset.Value = a.Length < 2 || a[1] == "on"; nextVrCheck = 0f; Msg("SimulateHeadset " + Plugin.SimulateHeadset.Value); break;
            case "vrhead":   // test aid for a virtual headset that reports the head at floor level: lifts the tracking origin
            {
                var c = Camera.main; var o = c != null ? c.transform.parent : null;
                if (o == null) { Msg("no camera parent"); break; }
                var lp = o.localPosition; lp.y = F(1, 1.65f); o.localPosition = lp;
                Msg($"Tracking origin '{o.name}' lifted to {lp.y:F2} m");
                break;
            }
            case "xr":
            {
                var sb = new System.Text.StringBuilder("[CMD] xr:");
                try { sb.Append($" isDeviceActive={UnityEngine.XR.XRSettings.isDeviceActive} enabled={UnityEngine.XR.XRSettings.enabled} device='{UnityEngine.XR.XRSettings.loadedDeviceName}'"); } catch (Exception e) { sb.Append(" XRSettings ERR " + e.Message); }
                try { var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
                      bool has = head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out bool worn);
                      sb.Append($" head.valid={head.isValid} head='{(head.isValid ? head.name : "")}' presenceKnown={has} worn={worn}"); } catch (Exception e) { sb.Append(" XRDevice ERR " + e.Message); }
                try { sb.Append($" eye={UnityEngine.XR.XRSettings.eyeTextureWidth}x{UnityEngine.XR.XRSettings.eyeTextureHeight} stereo={Camera.main.stereoEnabled}"); } catch (Exception e) { sb.Append(" eye ERR " + e.Message); }
                sb.Append($" headsetInUse={HeadsetInUse()} listenerVolume={AudioListener.volume:F1} fps={1f / Mathf.Max(1e-4f, Time.smoothDeltaTime):F0}");
                Plugin.L.LogInfo(sb.ToString());
                break;
            }
            case "audio":
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<AudioSource>()))
                {
                    var s = o.TryCast<AudioSource>();
                    if (s == null || !s.isPlaying) continue;
                    string Path(Transform x) { var r = x.name; while ((x = x.parent) != null) r = x.name + "/" + r; return r; }
                    Plugin.L.LogInfo($"[CMD] audio {Path(s.transform)} clip={(s.clip != null ? s.clip.name : "-")} loop={s.loop} vol={s.volume:F2} mute={s.mute} group={(s.outputAudioMixerGroup != null ? s.outputAudioMixerGroup.name : "-")} spatial={s.spatialBlend:F1}");
                }
                break;
            case "phys":
                // phys <rollDecel> <rollDrag> [bounce] [on|off]
                if (a.Length > 1) BallPhysics.RollDecel = F(1, BallPhysics.RollDecel);
                if (a.Length > 2) BallPhysics.RollDrag = F(2, BallPhysics.RollDrag);
                if (a.Length > 3) LaneBuilder.SetBounce(F(3, 0.6f), F(3, 0.6f));
                if (a.Length > 4) BallPhysics.Enabled = a[4] == "on";
                Msg(PhysicsSummary() + (BallPhysics.Enabled ? "" : "  (custom physics OFF)"));
                break;
            case "lanecam":
            {
                // lanecam <laneIndex 1..n> <x> <z> [yawDeg] [pitchDeg] [eyeHeight]  - camera on any lane of the park
                int li = (int)F(1, 1) - 1;
                if (li < 0 || li >= LaneBuilder.Lanes.Count) { Msg("no lane " + (li + 1)); break; }
                var l = LaneBuilder.Lanes[li];
                var heading = Quaternion.AngleAxis(F(4, 0f), Vector3.up) * l.Forward;
                WPlayer.Instance?.WarpToLocation(l.ToWorld(F(2, 0f), 0, F(3, 0f)), heading);
                ResetView(heading);
                pitch = F(5, 30f);
                if (a.Length > 6) Plugin.EyeHeight.Value = F(6, 1.6f);
                Msg($"Camera on lane {li + 1}: {l.Def.name}");
                break;
            }
            case "trylane":
            {
                // trylane <laneIndex> <speed m/s> [aimDeg] [waitSeconds] - put the ball on a lane's tee, launch it directly
                // (exact speed, no putter multiplier, no stroke), report where it ends up.
                int li = (int)F(1, 1) - 1;
                if (li < 0 || li >= LaneBuilder.Lanes.Count) { Msg("no lane " + (li + 1)); break; }
                var l = LaneBuilder.Lanes[li];
                var ball = Ball.instance;
                float r = ball.radius > 0 ? ball.radius : 0.021f;
                var p = LaneBuilder.TeeOf(l) + Vector3.up * (r + 0.002f);
                ball.rb.position = p; ball.transform.position = p;
                ball.rb.angularVelocity = Vector3.zero;
                // the game respawns the ball if it rests on another hole's surface: lend this lane to the active hole
                tryIps.Clear();
                if (Hole.activeHole != null)
                    foreach (var ips in l.Root.GetComponentsInChildren<InPlaySurface>())
                    {
                        tryIps.Add((ips, ips.hole));
                        ips.hole = Hole.activeHole;
                    }
                var dir = Quaternion.AngleAxis(F(3, 0f), Vector3.up) * l.Forward;
                ball.rb.velocity = dir * F(2, 2f);
                tryLane = l; tryInfo = $"lane {li + 1} speed {F(2, 2f):F2} aim {F(3, 0f):F1}"; tryCheckAt = Time.unscaledTime + F(4, 8f);
                break;
            }
            case "probe":
            {
                // probe <laneIndex> <x> <z0> <z1> [step] - raycast down along the lane and list what is hit
                int li = (int)F(1, 1) - 1;
                var l = LaneBuilder.Lanes[li];
                float x = F(2, 0f), z0 = F(3, 0f), z1 = F(4, z0), st = F(5, 0.05f);
                bool oldBack = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                for (float z = z0; z <= z1 + 1e-4f; z += st)
                {
                    var from = l.ToWorld(x, 1.0f, z);
                    var hits = Physics.RaycastAll(from, Vector3.down, 2f, -1, QueryTriggerInteraction.Ignore);
                    var sb = new System.Text.StringBuilder(string.Format(Inv, "[CMD] probe z={0:F2}:", z));
                    foreach (var h in hits.Where(h => h.collider.gameObject.layer == 0).OrderByDescending(h => h.point.y).Take(5))
                        sb.Append(string.Format(Inv, " {0} y={1:F3}", h.collider.name, l.ToLocal(h.point).y));
                    Plugin.L.LogInfo(sb.ToString());
                }
                Physics.queriesHitBackfaces = oldBack;
                break;
            }
            case "timescale": Time.timeScale = F(1, 1f); Msg("timeScale " + Time.timeScale); break;
            case "hud": hud = a.Length < 2 || a[1] == "on"; break;
            case "lanemode": Plugin.LaneMode.Value = a[1]; Msg("Lane mode: " + a[1]); break;
            case "park": BuildPark(); break;
            case "hole": GoToHole((int)F(1, 1)); break;
            case "near":
            {
                // list visible renderers within <radius> m of the camera
                float rad = F(1, 3f);
                var cam = Camera.main;
                Plugin.L.LogInfo($"[CMD] skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")} clear={cam.clearFlags} bg={cam.backgroundColor} fog={RenderSettings.fog}");
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Renderer>()))
                {
                    var r = o.TryCast<Renderer>();
                    if (r == null || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
                    if (Vector3.Distance(r.bounds.center, cam.transform.position) > rad) continue;
                    string Path(Transform x) { var s = x.name; while ((x = x.parent) != null) s = x.name + "/" + s; return s; }
                    Plugin.L.LogInfo($"[CMD] near {Path(r.transform)} type={r.GetIl2CppType().Name} layer={r.gameObject.layer} dist={Vector3.Distance(r.bounds.center, cam.transform.position):F2}");
                }
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Canvas>()))
                {
                    var c = o.TryCast<Canvas>();
                    if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.renderMode != RenderMode.WorldSpace) continue;
                    float dist = Vector3.Distance(c.transform.position, cam.transform.position);
                    if (dist > rad * 2) continue;
                    string Path(Transform x) { var s = x.name; while ((x = x.parent) != null) s = x.name + "/" + s; return s; }
                    Plugin.L.LogInfo($"[CMD] canvas {Path(c.transform)} dist={dist:F2}");
                }
                break;
            }
            case "aim": aimYaw = F(1, 0f); Msg($"Aim {aimYaw} deg"); break;
            case "putt": Hit(F(1, 2f), AimDir(), null); break;
            default: Msg("Unknown command " + a[0]); break;
        }
    }

    void LogState()
    {
        var sb = new System.Text.StringBuilder("[CMD] state:");
        try { sb.Append(" scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name); } catch { }
        try
        {
            var gm = GameManager.instance;
            sb.Append(gm == null ? " gm=null" : $" loading={gm.loading}");
        }
        catch { }
        try
        {
            var h = Hole.activeHole;
            sb.Append(h == null ? " hole=null" : $" hole=#{h.holeNumber} par={h.par} strokes={h.strokes} complete={h.holeComplete} cup={h.cupPosition?.position}");
        }
        catch (Exception e) { sb.Append(" hole=ERR " + e.Message); }
        try
        {
            var b = Ball.instance;
            sb.Append(b == null ? " ball=null" : $" ball={b.transform.position} v={b.rb.velocity.magnitude:F3} layer={b.gameObject.layer} inHole={b.inHole} surface={b.rollingSurfaceTag}");
            if (b != null)
            {
                var rb = b.rb;
                sb.Append($" rb[mass={rb.mass:F4} drag={rb.drag:F3} angDrag={rb.angularDrag:F3} maxAngV={rb.maxAngularVelocity:F1} sleep={rb.sleepThreshold:F4} interp={rb.interpolation} cd={rb.collisionDetectionMode} radius={b.radius:F4}]");
                foreach (var c in b.GetComponentsInChildren<SphereCollider>())
                    if (c.sharedMaterial != null)
                        sb.Append($" col[{c.name} r={c.radius:F4} mat={c.sharedMaterial.name} dynF={c.sharedMaterial.dynamicFriction:F2} bounce={c.sharedMaterial.bounciness:F2}]");
                foreach (var cf in b.GetComponentsInChildren<ConstantForce>())
                    sb.Append($" constForce[{cf.force} rel={cf.relativeForce}]");
            }
        }
        catch (Exception e) { sb.Append(" ball=ERR " + e.Message); }
        try { var p = WPlayer.Instance; sb.Append(p == null ? " player=null" : $" player={p.transform.position}"); } catch { }
        try { var c = ActiveCamera(); sb.Append(c == null ? " cam=null" : $" cam={c.name}@{c.transform.position}"); } catch { }
        try
        {
            var c = Camera.main;
            if (c != null && LaneBuilder.Active)
            {
                float Ang(Vector3 v) => Vector3.SignedAngle(LaneBuilder.Forward, Vector3.ProjectOnPlane(v, Vector3.up), Vector3.up);
                sb.Append($" camYawVsLane={Ang(c.transform.forward):F1} parentYawVsLane={Ang(c.transform.parent.forward):F1} localYaw={yaw:F1} localEuler={c.transform.localEulerAngles}");
                var t = c.transform.parent; var chain = "";
                while (t != null) { chain += $" {t.name}:{t.localEulerAngles.y:F0}/{t.localPosition}"; t = t.parent; }
                sb.Append(" chain:" + chain);
            }
        }
        catch (Exception e) { sb.Append(" camdbg=ERR " + e.Message); }
        sb.Append(LaneBuilder.Active ? $" lane={LaneBuilder.Current.name}@{LaneBuilder.Origin}" : " lane=none");
        Plugin.L.LogInfo(sb.ToString());
    }

    // ---------- actions ----------

    void CycleMethod()
    {
        int i = Array.IndexOf(Methods, Plugin.HitMethod.Value);
        Plugin.HitMethod.Value = Methods[(i + 1) % Methods.Length];
        Msg("Hit method: " + Plugin.HitMethod.Value);
    }

    void Hit(float speed, Vector3 dir, string methodOverride)
    {
        var ball = Ball.instance;
        if (ball == null) { Msg("No ball (Ball.instance is null) - are you in a course?"); return; }

        var v = dir.normalized * speed;
        string method = methodOverride ?? Plugin.HitMethod.Value;

        switch (method)
        {
            case "Putter":
            {
                var putter = FindFirst<Putter>();
                if (putter == null) { Msg("No Putter object found - try F6 for another hit method"); return; }
                exactPending = true;
                putter.ApplyHit(v / putterGain); // the game multiplies putter hits (by about 1.5-1.7); corrected to the exact speed in FixedUpdate
                break;
            }
            case "Network":
            {
                var net = ball.GetComponent<BallNetworkController>()
                          ?? ball.GetComponentInParent<BallNetworkController>()
                          ?? FindFirst<BallNetworkController>();
                if (net == null) { Msg("No BallNetworkController found"); return; }
                net.HitBall(v, false, default, default);
                break;
            }
            default:
                ball.rb.velocity = v;
                break;
        }
        lastHitTime = Time.unscaledTime;
        launchSpeed = 0f; lastRequested = speed; launchLogged = false;
        Msg($"{method} hit: {speed:F2} m/s requested");
    }

    void WarpToBall()
    {
        var ball = Ball.instance;
        var player = WPlayer.Instance;
        if (ball == null || player == null) { Msg("Need a ball and a player"); return; }
        var dir = AimDir();
        var feet = ball.transform.position - dir * Plugin.WarpBackDistance.Value;
        player.WarpToLocation(feet, dir);
        ResetView(dir);
        Msg("Warped behind ball");
    }

    void ToggleDesktopPlayer()
    {
        var player = WPlayer.Instance;
        if (player == null) { Msg("No Player.Instance"); return; }
        var dp = player.GetComponent<FarBridge.Worlds.DesktopPlayer>();
        if (dp == null)
        {
            player.gameObject.AddComponent<FarBridge.Worlds.DesktopPlayer>();
            Msg("Added DesktopPlayer to " + player.gameObject.name);
        }
        else
        {
            dp.enabled = !dp.enabled;
            Msg("DesktopPlayer enabled = " + dp.enabled);
        }
    }

    // ---------- lanes ----------

    void RefreshLaneFiles()
    {
        var dir = Plugin.LanesFolder;
        laneFiles = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").OrderBy(x => x).ToArray() : Array.Empty<string>();
        if (lanePath != null) laneIdx = Array.IndexOf(laneFiles, lanePath);
    }

    void NextLane()
    {
        RefreshLaneFiles();
        if (laneFiles.Length == 0) { Msg("No lane files in " + Plugin.LanesFolder); return; }
        laneIdx = (laneIdx + 1) % laneFiles.Length;
        lanePath = laneFiles[laneIdx];
        Msg($"Lane {laneIdx + 1}/{laneFiles.Length}: {Path.GetFileName(lanePath)}  (F5 builds)");
    }

    void BuildLane()
    {
        if (lanePath == null) NextLane();
        if (lanePath == null) return;
        var def = LaneDef.Load(lanePath);
        LaneBuilder.Build(def);
        laneSession = true;
        lastHolePtr = Hole.activeHole?.Pointer ?? IntPtr.Zero;
        Msg($"Built lane: {def.name}");
    }

    // ---------- music ----------

    float nextMusicCheck;
    string audioState = "";

    /// The game crossfades between newly created music sources, so re-apply regularly.
    void ApplyMusicMute()
    {
        var am = AudioManager.instance;
        if (am == null) return;
        int MuteList(Il2CppSystem.Collections.Generic.List<AudioSource> list, bool mute)
        {
            int n = 0;
            if (list == null) return 0;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null) continue;
                n++;
                if (s.mute != mute) s.mute = mute;
            }
            return n;
        }
        bool mm = Plugin.MuteMusic.Value, ma = Plugin.MuteAmbience.Value;
        int nm = MuteList(am.musicSources, mm);
        int na = MuteList(am.ambienceSources, ma);

        // Courses also place their own emitters (lava, fountains, animated characters, drums ...) under "SceneGroup".
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<AudioSource>()))
        {
            var s = o.TryCast<AudioSource>();
            if (s == null) continue;
            var t = s.transform;
            bool inScene = false, inHole = false;
            while (t != null)
            {
                if (t.name == "SceneGroup") inScene = true;
                if (t.name.StartsWith("Hole")) inHole = true;
                t = t.parent;
            }
            if (!inScene || inHole) continue;
            na++;
            if (s.mute != ma) s.mute = ma;
        }
        string key = $"{mm}{ma}{nm}/{na}";
        if (key != audioState)
        {
            audioState = key;
            Plugin.L.LogInfo($"Audio: music {(mm ? "muted" : "on")} ({nm} sources), ambience {(ma ? "muted" : "on")} ({na} sources)");
        }
    }

    // ---------- follow the game's hole changes ----------

    bool laneSession;
    IntPtr lastHolePtr;
    float rebuildAt;

    /// Jump straight to hole n (1-based) without playing the holes before it. PageDown / PageUp step through.
    void GoToHole(int n)
    {
        Hole target = null; int count = 0;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Hole>()))
        {
            var h = o.TryCast<Hole>();
            if (h == null) continue;
            count++;
            if (h.holeNumber == n) target = h;
        }
        if (target == null) { Msg($"No hole {n} (this course has {count})"); return; }
        try { Hole.activeHole?.StopLoadingNextHole(); } catch { }
        Hole.SetActiveHole(target);
        try { target.StartHole(); } catch (Exception e) { Plugin.L.LogWarning("StartHole: " + e.Message); }
        var l = LaneBuilder.CurrentLane;
        if (l != null) LaneBuilder.PlacePlayerAndBall(l);
        Msg($"Hole {n}" + (l != null ? ": " + l.Def.name : ""));
    }

    // Lanes whose target is not a hole (net, bowl, labyrinth channel): the game cannot see the ball arrive, so the
    // hole is completed here once the ball has come to rest inside the target area.
    float targetRestSince = -1f;
    IntPtr targetDoneFor = IntPtr.Zero;
    Vector3 targetRestPos;
    float stickHeldSince = -1f; bool stickFired;

    /// In the headset: hold a thumbstick pressed in for 0.7 s to put the ball back on the tee (and the player behind
    /// it, when further than 1.5 m away). A short haptic pulse confirms it.
    void VrResetCheck()
    {
        if (!HeadsetInUse() || LaneBuilder.CurrentLane == null) return;
        bool down = false; UnityEngine.XR.InputDevice which = default;
        foreach (var node in new[] { UnityEngine.XR.XRNode.RightHand, UnityEngine.XR.XRNode.LeftHand })
        {
            var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out bool c) && c) { down = true; which = dev; }
        }
        if (!down) { stickHeldSince = -1f; stickFired = false; return; }
        if (stickHeldSince < 0f) stickHeldSince = Time.unscaledTime;
        if (stickFired || Time.unscaledTime - stickHeldSince < 0.7f) return;
        stickFired = true;
        var l = LaneBuilder.CurrentLane;
        var player = WPlayer.Instance;
        var stand = l.Stand(LaneBuilder.TeeOf(l));
        if (player != null && Vector3.ProjectOnPlane(player.FeetPositionApprox - stand, Vector3.up).magnitude > 1.5f) LaneBuilder.PlacePlayerAndBall(l);
        else LaneBuilder.ResetBall();
        try { which.SendHapticImpulse(0, 0.6f, 0.15f); } catch { }
        Plugin.L.LogInfo("Ball reset to tee (thumbstick held)");
    }
    void CheckTargetLane()
    {
        var l = LaneBuilder.CurrentLane; var h = Hole.activeHole; var ball = Ball.instance;
        if (l == null || h == null || ball == null || ball.rb == null || l.Def.cupHole) { targetRestSince = -1f; return; }
        if (h.Pointer == targetDoneFor || h.holeComplete || h.strokes < 1 || tryCheckAt >= 0f) { targetRestSince = -1f; return; }
        var d = l.Def;
        var lp = l.ToLocal(ball.transform.position);
        float cupY = d.cupHeight ?? LaneGeometry.Height(d, d.cup[0], d.cup[1]);
        bool inside = new Vector2(lp.x - d.cup[0], lp.z - d.cup[1]).magnitude < d.targetRadius && Mathf.Abs(lp.y - cupY) < 0.15f;
        // "At rest" is judged by position, not speed: a ball lying in a moulded hole keeps trembling at up to 0.1 m/s.
        if (!inside || ball.rb.velocity.magnitude > 0.5f) { targetRestSince = -1f; return; }
        if (targetRestSince < 0f || (lp - targetRestPos).magnitude > 0.015f) { targetRestSince = Time.unscaledTime; targetRestPos = lp; return; }
        if (Time.unscaledTime - targetRestSince < 0.8f) return;
        targetDoneFor = h.Pointer; targetRestSince = -1f;
        Plugin.L.LogInfo($"Target reached on '{d.name}' with {h.strokes} stroke(s) - completing hole #{h.holeNumber}");
        var trig = h.holeCompleteTrigger;
        if (trig != null) trig.ForceHoleComplete(); else h.CompleteHole(false);
    }

    void FollowHoles()
    {
        Park.Tick();
        if (!laneSession) return;
        if (!Park.Active && Plugin.LaneMode.Value == "off") return;
        var h = Hole.activeHole;
        var ptr = h?.Pointer ?? IntPtr.Zero;
        if (ptr != IntPtr.Zero && ptr != lastHolePtr)
        {
            lastHolePtr = ptr;
            rebuildAt = Time.unscaledTime + 1.5f; // let the game finish warping to its next hole first
            Plugin.L.LogInfo($"Game moved to hole #{h.holeNumber}" + (Park.Active ? "" : $"; rebuilding lane ({Plugin.LaneMode.Value})"));
            Park.RequestHide(2f);
        }
        if (rebuildAt > 0 && Time.unscaledTime >= rebuildAt)
        {
            rebuildAt = 0;
            if (Park.Active)
            {
                // all lanes exist already; the game put the ball on the next lane's tee - bring the player there
                ResetAim();
                var l = LaneBuilder.CurrentLane;
                if (l != null)
                {
                    var tee = l.ToWorld(l.Def.tee[0], 0, l.Def.tee[1]);
                    WPlayer.Instance?.WarpToLocation(l.Stand(tee), l.Forward);
                    ResetView(l.Forward);
                }
                return;
            }
            if (Plugin.LaneMode.Value == "sequence")
            {
                RefreshLaneFiles();
                var playable = laneFiles.Where(f => !Path.GetFileName(f).StartsWith("00_")).ToArray();
                if (playable.Length > 0)
                {
                    int i = Array.IndexOf(playable, lanePath);
                    lanePath = playable[(i + 1) % playable.Length];
                }
            }
            LaneBuilder.Clear(); // place the lane above the new hole
            BuildLane();
        }
    }

    float autoParkAt = -1f;
    string autoParkScene = "";

    /// Build the park by itself a few seconds after a course has finished loading (once per loaded course).
    void AutoParkCheck()
    {
        if (!Plugin.AutoPark.Value) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var gm = GameManager.instance;
        if (scene.name == "MainMenu" || gm == null || gm.loading || Hole.activeHole == null) { autoParkAt = -1f; if (scene.name == "MainMenu") autoParkScene = ""; return; }
        string key = scene.name + "#" + scene.handle;
        if (key == autoParkScene || Park.Exists) return;
        if (autoParkAt < 0f) { autoParkAt = Time.unscaledTime + 5f; return; }
        if (Time.unscaledTime < autoParkAt) return;
        autoParkScene = key; autoParkAt = -1f;
        Plugin.L.LogInfo("AutoPark: building the park on " + scene.name);
        BuildPark();
    }

    void BuildPark()
    {
        RefreshLaneFiles();
        var files = laneFiles.Where(f => !Path.GetFileName(f).StartsWith("00_")).ToList();
        if (files.Count == 0) { Msg("No lane files in " + Plugin.LanesFolder); return; }
        var defs = files.Select(LaneDef.Load).ToList();
        Park.Build(defs);
        laneSession = true;
        lastHolePtr = Hole.activeHole?.Pointer ?? IntPtr.Zero;
        lanePath = files[0];
        Msg($"Park: {Math.Min(defs.Count, 18)} lanes");
    }

    void CycleLaneMode()
    {
        var modes = new[] { "sequence", "repeat", "off" };
        int i = Array.IndexOf(modes, Plugin.LaneMode.Value);
        Plugin.LaneMode.Value = modes[(i + 1) % modes.Length];
        Msg("After holing out: " + Plugin.LaneMode.Value);
    }

    // ---------- courses ----------

    void NextScene()
    {
        if (scenes.Count == 0) RefreshScenes();
        if (scenes.Count == 0) { Msg("No CourseInfoAssets loaded yet"); return; }
        sceneIdx = (sceneIdx + 1) % scenes.Count;
        Msg($"Selected course {sceneIdx + 1}/{scenes.Count}: {scenes[sceneIdx]}  (F10 loads)");
    }

    void RefreshScenes()
    {
        var found = Resources.FindObjectsOfTypeAll(Il2CppType.Of<CourseInfoAsset>());
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in found)
        {
            var info = o.TryCast<CourseInfoAsset>();
            if (info != null && !string.IsNullOrEmpty(info.SceneName)) set.Add(info.SceneName);
        }
        scenes = set.ToList();
        scenes.Insert(0, "MainMenu");
        Plugin.L.LogInfo("Scenes: " + string.Join(", ", scenes));
    }

    void LoadScene(string name)
    {
        if (name == null)
        {
            if (sceneIdx < 0) { Msg("Pick a course with F9 first"); return; }
            name = scenes[sceneIdx];
        }
        var gm = GameManager.instance;
        if (gm == null) { Msg("No GameManager.instance"); return; }
        Park.Clear();
        laneSession = false;
        gm.LoadScene(name, true);
        Msg("Loading " + name + " ...");
    }

    // ---------- helpers ----------

    static Camera ActiveCamera()
    {
        Camera best = null;
        foreach (var c in Camera.allCameras)
            if (c != null && c.isActiveAndEnabled && (best == null || c.depth > best.depth)) best = c;
        return best ?? Camera.main;
    }

    static Vector3 CameraDirection()
    {
        var cam = ActiveCamera();
        var fwd = cam != null ? cam.transform.forward : Vector3.forward;
        var flat = Vector3.ProjectOnPlane(fwd, Vector3.up);
        return flat.sqrMagnitude < 1e-6f ? Vector3.forward : flat.normalized;
    }

    static T FindFirst<T>() where T : UnityEngine.Object
    {
        var arr = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<T>(), true);
        return arr != null && arr.Length > 0 ? arr[0].TryCast<T>() : null;
    }

    void Msg(string s)
    {
        status = s;
        statusUntil = Time.unscaledTime + 5f;
        Plugin.L.LogInfo(s);
    }

    static void Try(string what, Action a)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { a(); }
        catch (Exception e) { Plugin.L.LogError($"{what}: {e}"); }
        double ms = sw.Elapsed.TotalMilliseconds;
        if (ms > 25) Plugin.L.LogInfo($"[PERF] {what} took {ms:F1} ms");
    }

    void OnGUI()
    {
        if (!hud) return;
        string hole = "-";
        try
        {
            var h = Hole.activeHole;
            if (h != null) hole = $"#{h.holeNumber}  par {h.par}  strokes {h.strokes}";
        }
        catch { }
        float speed = 0f;
        try { if (Ball.instance != null) speed = Ball.instance.rb.velocity.magnitude; } catch { }

        string sel = sceneIdx >= 0 && sceneIdx < scenes.Count ? scenes[sceneIdx] : "(F9)";
        string lane = lanePath != null ? Path.GetFileNameWithoutExtension(lanePath) : "(F4)";
        string text =
            $"Walkabout Desktop Test   [H] hide\n" +
            $"Hole {hole}    ball {speed:F2} m/s    last launch {launchSpeed:F2} m/s\n" +
            $"Physics [P]: {(BallPhysics.Enabled ? "custom" : "game")}{(BallPhysics.OnLane ? " (on lane)" : "")}   {PhysicsSummary()}   [1/2 3/4 5/6]\n" +
            $"Hit method [F6]: {Plugin.HitMethod.Value}    max {Plugin.MaxSpeed.Value:F1} m/s\n" +
            $"Course [F9/F10]: {sel}    Lane [F4/F5]: {lane}    after hole [F3]: {Plugin.LaneMode.Value}\n" +
            $"Aim {aimYaw:+0.0;-0.0;0.0} deg: Q/E turn (Shift = fine), V = view dir, Z = straight   G (hold) = putt\n" +
            $"Right mouse = look  WASD = walk  Space/Ctrl = up/down  F = fly  T = eye height  PgUp/PgDn = hole\n" +
            $"B = behind ball  R/Backspace = reset ball  C = camera\n" +
            (Time.unscaledTime < statusUntil ? "> " + status : "");

        GUI.Box(new Rect(10, 10, 660, 170), "");
        GUI.Label(new Rect(20, 16, 640, 160), text);
        if (charging)
        {
            GUI.Box(new Rect(20, 185, 560, 16), "");
            GUI.Box(new Rect(20, 185, 560 * charge, 16), "");
            GUI.Label(new Rect(590, 184, 120, 20), $"{PuttSpeed(charge):F2} m/s");
        }
    }
}
