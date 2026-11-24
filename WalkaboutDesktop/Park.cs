using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WalkaboutDesktop;

/// A minigolf park of our own: hides the loaded course's scenery and lays out our lanes in rows on a lawn,
/// mapping the game's hole 1..n to lane 1..n so scoring and hole progression keep working.
public static class Park
{
    public static bool Active { get; private set; }
    public static float GroundY { get; private set; }
    static GameObject root;
    public static bool Exists => root != null;
    public static bool IsOurs(Transform t) => root != null && t.IsChildOf(root.transform);
    static float nextHide = -1f;      // time of the next clean-up pass (-1 = none pending)
    static int hidePasses;

    // Layout (metres)
    public static float LaneElevation = 0.13f; // lane surface above the paving
    public const float LaneGap = 1.2f;         // free space between neighbouring lanes
    public static int LanesPerRow = 6;
    public static float PathWidth = 3.0f;      // between rows

    static readonly float[] Grass = { 0.30f, 0.47f, 0.22f };

    /// Garden layout of the 18 lanes: tee x, tee z, direction in degrees (0 = towards +z, 90 = towards +x).
    /// Checked for overlaps (lanes plus the place where the player stands) with scratch tool layout.py.
    static readonly float[][] Garden =
    {
        new[] { 21.5f, 10.0f, 0f }, new[] { 24.0f, 10.8f, 0f }, new[] { 28.2f, 9.5f, 0f }, new[] { 34.0f, 10.5f, 0f }, new[] { 35.5f, 19.5f, -90f }, new[] { 26.5f, 22.5f, 90f }, new[] { 35.0f, 23.5f, 0f }, new[] { 31.0f, 26.0f, 0f }, new[] { 27.0f, 26.5f, 0f }, new[] { 35.5f, 35.5f, -90f }, new[] { 36.0f, 40.5f, -90f }, new[] { 26.0f, 38.0f, -90f }, new[] { 25.0f, 42.0f, -90f }, new[] { 15.5f, 43.0f, 180f }, new[] { 11.5f, 42.0f, 180f }, new[] { 5.5f, 43.0f, 180f }, new[] { 2.5f, 31.5f, 90f }, new[] { 12.5f, 24.5f, 180f },
    };

    public static void Build(IList<LaneDef> defs)
    {
        Clear();
        var holes = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Hole>())
            .Select(o => o.TryCast<Hole>()).Where(h => h != null)
            .OrderBy(h => h.holeNumber).ToList();
        if (holes.Count == 0) throw new InvalidOperationException("No holes - load a course first.");
        int n = Math.Min(defs.Count, holes.Count);

        // Ground at the first hole's original tee height (clear of the course's ball kill plane).
        var start = holes[0].startingPosition.transform.position;
        GroundY = start.y;
        try { GroundY = Mathf.Max(GroundY, Course.instance.globalBallKillPlaneLevel + 3f); } catch { }

        root = new GameObject("WalkaboutPark");
        var basePos = new Vector3(start.x, GroundY, start.z);
        root.transform.position = basePos;

        // Our own daylight (the course's lights are switched off with its scenery).
        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(root.transform, false);
        sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = 1.0f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        RenderSettings.sun = sun;
        // The game ships without real-time shadows; switch them on in its render pipeline if the build still has them.
        try
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.TryCast<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>();
            if (urp != null)
            {
                Plugin.L.LogInfo($"URP before: mainLightShadows={urp.supportsMainLightShadows} distance={urp.shadowDistance} res={urp.mainLightShadowmapResolution} cascades={urp.shadowCascadeCount} soft={urp.supportsSoftShadows} msaa={urp.msaaSampleCount} scale={urp.renderScale}");
                if (Plugin.Shadows.Value)
                {
                    urp.supportsMainLightShadows = true;
                    urp.shadowDistance = 45f;
                    urp.mainLightShadowmapResolution = 4096;
                    urp.shadowCascadeCount = 2;
                    urp.supportsSoftShadows = true;
                }
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("URP shadows: " + e.Message); }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.57f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.33f, 0.27f);
        RenderSettings.fog = false;
        RenderSettings.reflectionIntensity = 0f;

        // Pack lanes side by side by their real footprint (angled lanes are wider), LanesPerRow per row.
        int rows = (n + LanesPerRow - 1) / LanesPerRow;
        var bounds = defs.Take(n).Select(LaneGeometry.Bounds).ToList();
        float rowDepth = bounds.Max(b => b.max.y - Mathf.Min(0, b.min.y)) + 0.3f + PathWidth;
        const float gap = LaneGap;
        var origins = new Vector3[n];
        float maxRowWidth = 0f;
        for (int r = 0; r < rows; r++)
        {
            float cursor = 0f;
            for (int i = r * LanesPerRow; i < Math.Min(n, (r + 1) * LanesPerRow); i++)
            {
                float x = cursor - bounds[i].min.x;
                origins[i] = new Vector3(x, LaneElevation, r * rowDepth);
                cursor = x + bounds[i].max.x + gap;
            }
            maxRowWidth = Mathf.Max(maxRowWidth, cursor - gap);
        }

        // Lawn and paving
        Ground("Lawn", new Vector3(maxRowWidth / 2, -0.06f, rows * rowDepth / 2 - 1.5f), new Vector3(maxRowWidth + 3000f, 0.1f, rows * rowDepth + 3000f), Grass, true);

        // The 18 Eternit lanes are laid out as an irregular garden; other sets of lanes stay in rows.
        var yaws = new float[n];
        if (n == Garden.Length && Plugin.GardenLayout.Value)
            for (int i = 0; i < n; i++) { origins[i] = new Vector3(Garden[i][0], LaneElevation, Garden[i][1]); yaws[i] = Garden[i][2]; }
        for (int i = 0; i < n; i++)
            LaneBuilder.BuildAt(defs[i], holes[i], basePos + origins[i], Quaternion.Euler(0, yaws[i], 0), root.transform, LaneElevation);

        try { ParkDeco.Build(root.transform, defs, origins, yaws, n, LaneElevation); }
        catch (Exception e) { Plugin.L.LogWarning("Park decoration: " + e); }

        LaneBuilder.NoProbes(root);
        Active = true;
        HideCourse();
        hidePasses = 0; nextHide = Time.unscaledTime + 3f;
        LaneBuilder.PlacePlayerAndBall(LaneBuilder.CurrentLane);
        Plugin.L.LogInfo($"Park built: {n} lanes on holes 1-{n} of {holes.Count}, {rows} row(s)");
    }

    public static void Clear()
    {
        Active = false;
        LaneBuilder.Clear();
        foreach (var go in deactivated) { try { if (go != null) go.SetActive(true); } catch { } }
        deactivated.Clear();
        seenHidden.Clear(); seenKept.Clear();
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }

    /// Re-apply hiding periodically: LOD systems and spawned effects can bring course objects back.
    public static void RequestHide(float delay) { if (Active) { hidePasses = 1; nextHide = Time.unscaledTime + delay; } }

    public static void Tick()
    {
        // A pass scans every renderer of the course and costs a few hundred ms, so it must not run all the time: two
        // passes after the park is built, and one after each hole change (the game switches flags etc. back on then).
        if (!Active || nextHide < 0f || Time.unscaledTime < nextHide) return;
        nextHide = ++hidePasses < 2 ? Time.unscaledTime + 6f : -1f;
        HideCourse();
    }

    static void Ground(string name, Vector3 localPos, Vector3 size, float[] color, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = LaneBuilder.MakeMaterial(color);
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    // ---------- hiding the loaded course ----------

    static int hiddenRenderers, hiddenColliders;
    static readonly List<GameObject> deactivated = new();
    static readonly HashSet<int> seenHidden = new();
    static readonly HashSet<int> seenKept = new();     // renderers and colliders a pass decided to leave alone

    public static void HideCourse()
    {
        if (root == null) return;
        var keep = new HashSet<int> { root.transform.GetInstanceID() };
        var holeRoots = new HashSet<int>();
        var cupRoots = new HashSet<int>();
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Hole>()))
        {
            var h = o.TryCast<Hole>();
            if (h == null) continue;
            holeRoots.Add(h.transform.GetInstanceID());
            if (h.cupPosition != null) cupRoots.Add(h.cupPosition.GetInstanceID());
        }

        // Which top-level objects to keep: player rig, course setup (ball, effects), sky.
        bool KeepRoot(Transform t)
        {
            // Only the course's scenery root is hidden. Everything else stays: the player rig, CourseSetup (ball, putter,
            // hands, pointer, teleporter), and the game's own pop-ups ("Hole in one", "Par", stroke limit ...), which
            // are separate root objects that a VR player needs to see.
            var n = t.name;
            if (n == "SceneGroup") return false;
            if (n == "CourseSetup" || n.StartsWith("XRRig") || n.StartsWith("MultiplayerAvatar")) return true;
            // loose course props that are spawned as their own roots (e.g. the beach ball of the Tiki course)
            try { if (t.GetComponentInChildren<Rigidbody>(true) != null && t.GetComponentInChildren<MeshRenderer>(true) != null) return false; } catch { }
            return true;
        }

        // Classify a transform: 0 = hide, 1 = keep, 2 = inside a hole but not at its cup (hide visuals, keep colliders)
        int Classify(Transform t)
        {
            bool inHole = false;
            for (var x = t; x != null; x = x.parent)
            {
                int id = x.GetInstanceID();
                if (keep.Contains(id)) return 1;
                if (cupRoots.Contains(id)) return 1;
                if (holeRoots.Contains(id)) inHole = true;
                if (x.parent == null && KeepRoot(x)) return 1;
            }
            return inHole ? 2 : 0;
        }

        // 1) Switch the scenery off as whole objects: everything under the scene's scenery roots that is not a hole
        //    (or an ancestor of one). Inactive objects cannot render, collide, emit particles or sound, and the
        //    course's own scripts on them cannot switch parts back on.
        var needed = new HashSet<int>();
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Hole>()))
        {
            var h = o.TryCast<Hole>();
            if (h == null) continue;
            foreach (var start in new[] { h.transform, h.cupPosition, h.startingPosition != null ? h.startingPosition.transform : null })
                for (var x = start; x != null; x = x.parent) needed.Add(x.GetInstanceID());
        }
        int off = 0;
        void Walk(Transform t)
        {
            int id = t.GetInstanceID();
            if (holeRoots.Contains(id) || cupRoots.Contains(id)) return;
            if (!needed.Contains(id))
            {
                if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); deactivated.Add(t.gameObject); off++; }
                return;
            }
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i));
        }
        foreach (var go in root.scene.GetRootGameObjects())
            if (go != root && !KeepRoot(go.transform) && needed.Contains(go.transform.GetInstanceID())) Walk(go.transform);
        if (off > 0) Plugin.L.LogInfo($"Park: switched off {off} scenery objects");

        // 2) Whatever is left (hole internals, ancestors of holes, other roots): hide renderers, disable colliders.
        int hr = 0, hc = 0;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Renderer>(), true))
        {
            var r = o.TryCast<Renderer>();
            if (r == null) continue;
            // later passes only look at what changed: skip what is still hidden and what was already found to stay
            int rid = r.GetInstanceID();
            if (seenKept.Contains(rid)) continue;
            if (r.forceRenderingOff && !r.enabled && seenHidden.Contains(rid)) continue;
            int c = Classify(r.transform);
            bool hide = c != 1;
            // static-batched cup visuals stay at the original hole positions
            if (!hide && r.gameObject.layer is 16 or 17 or 18) hide = !r.transform.IsChildOf(root.transform);
            // nothing of the course's holes is shown (flags, cup rims): the lanes bring their own cup
            if (!hide) for (var x = r.transform; x != null; x = x.parent) if (holeRoots.Contains(x.GetInstanceID())) { hide = true; break; }
            if (!hide)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Combined Mesh") && !r.transform.IsChildOf(root.transform)) hide = true;
            }
            if (hide && (!r.forceRenderingOff || r.enabled) && !(seenHidden.Contains(r.GetInstanceID()) && r.sharedMaterials.Length == 0))
            {
                r.forceRenderingOff = true;
                r.enabled = false;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // hidden scenery must not shade the park
                // The game switches some of these back on (flags, cup parts); once that happened, take the materials
                // away so that it has nothing to draw.
                if (seenHidden.Contains(r.GetInstanceID()))
                {
                    r.sharedMaterials = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Material>(0);
                    Plugin.L.LogInfo($"Park: '{r.name}' was switched back on by the game - materials removed");
                }
                seenHidden.Add(r.GetInstanceID());
                hr++;
            }
            else if (!hide) seenKept.Add(rid);
        }
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Collider>(), true))
        {
            var col = o.TryCast<Collider>();
            if (col == null || !col.enabled) continue;
            int cid = col.GetInstanceID();
            if (seenKept.Contains(cid)) continue;
            if (Classify(col.transform) == 0) { col.enabled = false; hc++; } else seenKept.Add(cid);
        }
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Terrain>(), true))
        {
            var t = o.TryCast<Terrain>();
            if (t != null && t.enabled) t.enabled = false;
        }
        // The course's hint/dialogue bubble floats in front of the player at each hole.
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Canvas>()))
        {
            var c = o.TryCast<Canvas>();
            if (c != null && c.enabled && c.name.StartsWith("DialogueBox")) c.enabled = false;
        }

        // The course's sky is often a (now hidden) mesh; show its skybox material instead, or a plain sky colour.
        var cam = Camera.main;
        if (cam != null)
        {
            if (RenderSettings.skybox != null) cam.clearFlags = CameraClearFlags.Skybox;
            else { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.58f, 0.76f, 0.93f); }
        }
        hiddenRenderers += hr; hiddenColliders += hc;
        if (hr + hc > 5) Plugin.L.LogInfo($"Park: hid {hr} renderers, {hc} colliders (total {hiddenRenderers}/{hiddenColliders})");
    }
}
