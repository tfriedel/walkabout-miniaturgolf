using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using WPlayer = FarBridge.Worlds.Player;

namespace WalkaboutDesktop;

/// One built lane, tied to one of the game's holes.
public class LaneInstance
{
    public LaneDef Def;
    public GameObject Root;
    public Hole Hole;
    public Vector3 Origin;
    public Quaternion Rotation;
    public float BaseHeight;      // lane surface above the ground the player stands on
    /// Where the player stands: on the ground behind the tee, not on the lane (the plate is BaseHeight above the feet).
    public Vector3 Stand(Vector3 tee) => tee - Forward * Plugin.WarpBackDistance.Value - Vector3.up * BaseHeight;
    public Vector3 Forward => Rotation * Vector3.forward;
    public Vector3 ToWorld(float x, float y, float z) => Origin + Rotation * new Vector3(x, y, z);
    public Vector3 ToLocal(Vector3 world) => Quaternion.Inverse(Rotation) * (world - Origin);
}

/// Builds lanes from LaneDefs at runtime and re-targets game holes (tee + cup) onto them.
/// Lane coordinates: origin = centre of the tee end on the playing surface; x right, y up, z towards the hole.
public static class LaneBuilder
{
    static readonly List<LaneInstance> lanes = new();
    static readonly Dictionary<string, Material> matCache = new();
    static GameObject buildRoot; // root of the lane currently being built (used by the primitive helpers)

    public static IReadOnlyList<LaneInstance> Lanes => lanes;

    /// The lane of the game's active hole (or the last built one).
    public static LaneInstance CurrentLane
    {
        get
        {
            lanes.RemoveAll(l => l.Root == null);
            if (lanes.Count == 0) return null;
            var h = Hole.activeHole;
            if (h != null)
            {
                var p = h.Pointer;
                var hit = lanes.FirstOrDefault(l => l.Hole != null && l.Hole.Pointer == p);
                if (hit != null) return hit;
            }
            return lanes[lanes.Count - 1];
        }
    }

    /// The lane whose outline contains the given world point (first match), or null.
    public static LaneInstance LaneAt(Vector3 world)
    {
        foreach (var l in lanes)
        {
            if (l.Root == null) continue;
            var p = l.ToLocal(world);
            if (p.y > -0.5f && p.y < 2f && LaneGeometry.Inside(l.Def, p.x, p.z)) return l;
        }
        return null;
    }

    public static Vector3 TeeOf(LaneInstance l) => TeeWorld(l);

    public static bool Active => CurrentLane != null;
    public static LaneDef Current => CurrentLane?.Def;
    public static Vector3 Origin => CurrentLane?.Origin ?? Vector3.zero;
    public static Quaternion Rotation => CurrentLane?.Rotation ?? Quaternion.identity;
    public static Vector3 Forward => Rotation * Vector3.forward;
    public static Vector3 ToWorld(float x, float y, float z) => Origin + Rotation * new Vector3(x, y, z);

    public static void Clear()
    {
        foreach (var l in lanes) if (l.Root != null) UnityEngine.Object.Destroy(l.Root);
        lanes.Clear();
    }

    /// Single-lane mode: build above the active hole's tee (or where this hole's lane already is) and move the player there.
    public static void Build(LaneDef d)
    {
        var hole = Hole.activeHole;
        if (hole == null) throw new InvalidOperationException("No active hole - load a course first.");
        var existing = lanes.FirstOrDefault(l => l.Root != null && l.Hole != null && l.Hole.Pointer == hole.Pointer);
        Vector3 origin; Quaternion rot; Transform parent = null; float baseHeight = 0f;
        if (existing != null)
        {
            origin = existing.Origin; rot = existing.Rotation;
            parent = existing.Root.transform.parent;
            baseHeight = existing.Root.transform.Find("Skirt_L") != null ? existing.Origin.y - Park.GroundY : 0f;
            UnityEngine.Object.Destroy(existing.Root);
            lanes.Remove(existing);
        }
        else
        {
            var start = hole.startingPosition.transform;
            origin = start.position + Vector3.up * Plugin.LaneHeight.Value;
            var fwd = Vector3.ProjectOnPlane(start.forward, Vector3.up);
            rot = Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward, Vector3.up);
        }
        var inst = BuildAt(d, hole, origin, rot, parent, baseHeight);
        PlacePlayerAndBall(inst);
    }

    /// Build a lane at a given place for a given hole. baseHeight > 0 adds a solid base down to the ground.
    public static LaneInstance BuildAt(LaneDef d, Hole hole, Vector3 origin, Quaternion rot, Transform parent, float baseHeight)
    {
        var inst = new LaneInstance { Def = d, Hole = hole, Origin = origin, Rotation = rot, BaseHeight = baseHeight };
        buildRoot = new GameObject($"Lane_{hole?.holeNumber}_{d.name}");
        if (parent != null) buildRoot.transform.SetParent(parent, false);
        buildRoot.transform.SetPositionAndRotation(origin, rot);
        inst.Root = buildRoot;

        var greenMat = MakeMaterial(d.surfaceColor);
        var wallMat = MakeMaterial(d.bandColor);
        var obstacleMat = MakeMaterial(d.obstacleColor);

        float T = d.surfaceThickness, L = d.length, W = d.width, bh = d.bandHeight, bt = d.bandThickness;
        if (d.rollDecel.HasValue) BallPhysics.RollDecel = d.rollDecel.Value;
        if (d.rollDrag.HasValue) BallPhysics.RollDrag = d.rollDrag.Value;
        BandBounce = d.bandBounce;
        ObstacleBounce = d.obstacleBounce;

        float r = hole != null ? MeasureCupRadius(hole) : 0.054f;
        var bandPhys = BounceMaterial("Band", BandBounce);
        var obstaclePhys = BounceMaterial("Obstacle", ObstacleBounce);

        // Playing surface: height-profiled mesh with a real hole (also its collider).
        var surf = MeshPart("Surface", LaneGeometry.Surface(d, r), greenMat, d.surfaceTag, SurfacePhys, false);
        if (hole != null) surf.AddComponent<InPlaySurface>().hole = hole;
        // moulded obstacles that are part of the height profile, in their own colour
        for (int fi = 0; d.surface != null && fi < d.surface.Count; fi++)
        {
            if (d.surface[fi].color == null) continue;
            var fs = MeshPart("Surface_" + d.surface[fi].type, LaneGeometry.Surface(d, r, fi), MakeMaterial(d.surface[fi].color), "Plastic", SurfacePhys, false);
            if (hole != null) fs.AddComponent<InPlaySurface>().hole = hole;
        }

        // Cup: dark tube + bottom; the collider is our own because the game's cup has a flat collar that would
        // stick out of slopes and hills. The renderer faces inwards, the collider too.
        if (d.cupHole)
        {
            var cupMat = MakeMaterial(new[] { 0.05f, 0.05f, 0.05f });
            MeshPart("Cup", LaneGeometry.Cup(d, r, 0.095f, true), cupMat, d.surfaceTag, null, false);
        }

        // Borders (Banden) along the outline, as short boxes that follow the surface height.
        var border = LaneGeometry.BorderSegments(d, 0.05f);
        // A wedge ends in a vertical drop: cut the Bande exactly there, so no piece spans the step at an angle.
        if (d.surface != null)
            foreach (var f in d.surface)
            {
                if (f.type != "wedge") continue;
                for (int i = border.Count - 1; i >= 0; i--)
                {
                    var (a, b, st) = border[i];
                    if (Mathf.Abs(a.x - b.x) > 1e-4f || (a.y - f.z1) * (b.y - f.z1) >= 0f) continue;
                    var m = new Vector2(a.x, f.z1);
                    border[i] = (a, m, st);
                    border.Insert(i + 1, (m, b, st));
                }
            }
        // Join pieces that lie in one straight line on an evenly sloped (or flat) plate: fewer objects, no joints.
        for (int i = 0; i + 1 < border.Count; )
        {
            var (a0, b0, s0) = border[i]; var (a1, b1, s1) = border[i + 1];
            bool join = s0 == s1 && (b0 - a1).sqrMagnitude < 1e-8f && (b1 - a0).magnitude < 1.0f
                        && Vector2.Dot((b0 - a0).normalized, (b1 - a1).normalized) > 0.99999f;
            if (join)
            {
                float h0 = LaneGeometry.PlateHeight(d, a0.x, a0.y), h1 = LaneGeometry.PlateHeight(d, b1.x, b1.y);
                for (int k = 1; k < 8 && join; k++)
                {
                    var q = Vector2.Lerp(a0, b1, k / 8f);
                    if (Mathf.Abs(LaneGeometry.PlateHeight(d, q.x, q.y) - Mathf.Lerp(h0, h1, k / 8f)) > 0.0004f) join = false;
                }
            }
            if (join) { border[i] = (a0, b1, s0); border.RemoveAt(i + 1); } else i++;
        }
        for (int i = 0; i < border.Count; i++)
        {
            var (a, b, isStart) = border[i];
            if (isStart && !d.bandAtStart) continue;
            if (!d.HasCircle && !d.bandAtEnd && Mathf.Abs(a.y - L) < 1e-4f && Mathf.Abs(b.y - L) < 1e-4f) continue;
            var mid = (a + b) / 2; var dir = (b - a).normalized;
            var n = new Vector2(dir.y, -dir.x);
            if (LaneGeometry.Inside(d, mid.x + n.x * 0.01f, mid.y + n.y * 0.01f)) n = -n; // outward
            var off = n * (bt / 2);
            // Lengthen an end only at an outward corner of the outline (to close the outer corner). At straight joints and
            // where the outline bends inwards (lane meets target circle) the Bande ends exactly at the joint.
            float Ext(Vector2 end, Vector2 along)
            {
                var probe = end + along * 0.004f - n * 0.004f;
                return LaneGeometry.Inside(d, probe.x, probe.y) ? 0f : bt;
            }
            var go = BandPiece($"Band_{i}", a + off, b + off, bt, bh, T + 0.02f, d, d.bandTag, wallMat, Ext(a, -dir), Ext(b, dir));
            go.GetComponent<Collider>().sharedMaterial = bandPhys;
        }

        // Legs: the real lanes are a thin frame on short feet, not a solid block
        if (baseHeight > 0.01f)
        {
            var legMat = MakeMaterial(new[] { 0.30f, 0.31f, 0.33f });
            var (bmin, bmax) = LaneGeometry.Bounds(d);
            for (float z = bmin.y + 0.15f; z < bmax.y - 0.05f; z += 0.25f)
                foreach (float sx in new[] { -1f, 1f })
                {
                    // nearest point inside the outline on this side, at a 1 m rhythm or under anything raised
                    float x = float.NaN;
                    for (float t = bmax.x + 0.5f; t > 0.02f; t -= 0.02f) if (LaneGeometry.Inside(d, sx * t, z) && LaneGeometry.Inside(d, sx * (t + 0.09f), z)) { x = sx * t; break; }
                    if (float.IsNaN(x)) continue;
                    float h = LaneGeometry.PlateHeight(d, x, z);
                    bool rhythm = Mathf.Abs(Mathf.Round((z - bmin.y - 0.15f) / 1.25f) * 1.25f - (z - bmin.y - 0.15f)) < 0.01f;
                    if (!rhythm) continue;
                    float len = baseHeight + h - T;
                    if (len < 0.02f) continue;
                    Skirt("Leg", new Vector3(x, h - T - len / 2, z), new Vector3(0.04f, len, 0.04f), legMat);
                }
        }

        PaintMarkings(d);

        foreach (var p in d.parts)
        {
            var mat = p.color is { Length: >= 3 } ? MakeMaterial(p.color)
                    : p.material == "green" ? greenMat : obstacleMat;
            if (p.alpha < 0.999f) mat = Transparent(mat, p.alpha);
            var partPhys = p.bounce.HasValue ? BounceMaterial("Part_" + p.name, p.bounce.Value) : obstaclePhys;
            foreach (var go in BuildPart(d, p, mat, partPhys))
            {
                var c = go.GetComponent<Collider>();
                if (c != null && p.shape != "channel" && !p.slick && !p.surface) c.sharedMaterial = partPhys; // moulded channels keep their slick material
            }
        }

        // Every part counts as playing surface for this hole (otherwise a ball resting in the net, in a bowl or on a
        // ramp is "out of bounds" and the game puts it back to the tee).
        if (hole != null)
            foreach (var c in buildRoot.GetComponentsInChildren<Collider>())
                if (!c.isTrigger && c.GetComponent<InPlaySurface>() == null) c.gameObject.AddComponent<InPlaySurface>().hole = hole;

        // Near the cup the game moves the ball to the "BallNearCup" physics layer, which only collides with the
        // "Cup" layer (13). Give every collider of the lane a twin on that layer so nothing becomes see-through there.
        DuplicateToCupLayer(buildRoot);
        NoProbes(buildRoot);

        if (hole != null) RetargetHole(inst);

        lanes.Add(inst);
        buildRoot = null;
        Plugin.L.LogInfo($"Built lane '{d.name}' for hole #{hole?.holeNumber} ({L} x {W} m, {d.parts.Count} parts, hole r={r * 100:F1} cm) at {origin}");
        return inst;
    }

    static void RetargetHole(LaneInstance l)
    {
        var d = l.Def; var hole = l.Hole;
        var tee = TeeWorld(l);
        var cup = l.ToWorld(d.cup[0], d.cupHeight ?? LaneGeometry.Height(d, d.cup[0], d.cup[1]), d.cup[1]);
        hole.startingPosition.transform.SetPositionAndRotation(tee, l.Rotation);
        if (hole.playerStartPosition != null)
            hole.playerStartPosition.SetPositionAndRotation(l.Stand(tee), l.Rotation);
        if (hole.cupPosition != null)
        {
            hole.cupPosition.SetPositionAndRotation(cup, l.Rotation);
            // the game's cup collider has a flat 26 cm collar; ours follows the surface, so switch the game's off
            foreach (var mc in hole.cupPosition.GetComponentsInChildren<MeshCollider>(true))
                if (mc.gameObject.layer == 13 && !mc.isTrigger) mc.enabled = false;
        }
        var col = l.Root.transform.Find("Surface").GetComponent<Collider>();
        hole.inPlaySurfaces = new Il2CppReferenceArray<Collider>(new[] { col });
    }

    public static void PlacePlayerAndBall(LaneInstance l)
    {
        if (l == null) return;
        ResetBall();
        DesktopController.ResetAim();
        var tee = TeeWorld(l);
        WPlayer.Instance?.WarpToLocation(l.Stand(tee), l.Forward);
        DesktopController.ResetView(l.Forward);
    }

    public static void ResetBall()
    {
        var l = CurrentLane;
        if (l == null) return;
        var ball = Ball.instance;
        if (ball == null) return;
        var tee = TeeWorld(l);
        float r = ball.radius > 0 ? ball.radius : 0.0214f;
        var p = tee + Vector3.up * (r + 0.002f);
        ball.rb.velocity = Vector3.zero;
        ball.rb.angularVelocity = Vector3.zero;
        ball.rb.position = p;
        ball.transform.position = p;
        try { ball.respawnPosition = p; } catch (Exception e) { Plugin.L.LogWarning("respawnPosition: " + e.Message); }
    }

    // ---------- visible hole ----------

    const float HolePlateHalf = 0.12f; // half size of the square patch around the hole that carries the round opening
    const int HoleSegments = 32;       // divisible by 8 so the patch corners are sample points
    static float measuredCupRadius = -1f;

    static float MeasureCupRadius(Hole hole)
    {
        const float fallback = 0.054f;
        if (measuredCupRadius > 0) return measuredCupRadius;
        try
        {
            var cupT = hole.cupPosition;
            foreach (var mc in cupT.GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc.gameObject.layer != 13 || mc.sharedMesh == null) continue;
                var mesh = mc.sharedMesh;
                if (!mesh.isReadable) { Plugin.L.LogInfo($"Cup mesh '{mesh.name}' not readable, using {fallback}"); return fallback; }
                float best = float.MaxValue, outer = 0f;
                foreach (var v in mesh.vertices)
                {
                    var local = cupT.InverseTransformPoint(mc.transform.TransformPoint(v));
                    float rr = new Vector2(local.x, local.z).magnitude;
                    outer = Mathf.Max(outer, rr);
                    if (Mathf.Abs(local.y) < 0.004f && rr > 0.01f) best = Mathf.Min(best, rr);
                }
                Plugin.L.LogInfo($"Cup mesh '{mesh.name}' on {mc.gameObject.name}: hole r={best:F4} m, apron r={outer:F3} m");
                if (best < 0.2f) return measuredCupRadius = best;
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("MeasureCupRadius: " + e.Message); }
        return fallback;
    }

    static Vector3 TeeWorld(LaneInstance l) =>
        l.ToWorld(l.Def.tee[0], LaneGeometry.Height(l.Def, l.Def.tee[0], l.Def.tee[1]), l.Def.tee[1]);

    /// Mesh object with renderer and (optionally convex) mesh collider.
    static GameObject MeshPart(string name, LaneGeometry.MeshData md, Material mat, string tag, PhysicMaterial phys, bool convex, bool collider = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(buildRoot.transform, false);
        try { go.tag = string.IsNullOrEmpty(tag) ? "Untagged" : tag; } catch { }
        var mesh = md.ToMesh(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider)
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.convex = convex;
            mc.sharedMesh = mesh;
            if (phys != null) mc.sharedMaterial = phys;
        }
        return go;
    }

    /// Box from a to b (lane xz) that follows the surface: `above` metres above the surface, `below` metres below it.
    static GameObject Segment(string name, Vector2 a, Vector2 b, float thickness, float above, float below, LaneDef d, string tag, Material mat,
                              float extStart = -1f, float extEnd = -1f)
    {
        // Banden (extStart given) stand on the lane plate; obstacle walls stand on whatever is there
        bool band = extStart >= 0f;
        var inA = a + (b - a).normalized * 0.001f; var inB = b - (b - a).normalized * 0.001f;   // sample just inside the piece
        float ha = band ? LaneGeometry.PlateHeight(d, inA.x, inA.y) : LaneGeometry.Height(d, a.x, a.y);
        float hb = band ? LaneGeometry.PlateHeight(d, inB.x, inB.y) : LaneGeometry.Height(d, b.x, b.y);
        var pa = new Vector3(a.x, ha, a.y); var pb = new Vector3(b.x, hb, b.y);
        var axis = pb - pa;
        float len = axis.magnitude;
        if (len < 1e-4f) axis = Vector3.forward;
        var rot = Quaternion.LookRotation(axis.normalized, Vector3.up);
        var centre = (pa + pb) / 2 + Vector3.up * ((above - below) / 2);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.layer = 0;
        try { go.tag = string.IsNullOrEmpty(tag) ? "Untagged" : tag; } catch { }
        go.transform.SetParent(buildRoot.transform, false);
        if (extStart < 0f) { extStart = thickness * 0.25f; extEnd = thickness * 0.25f; } // walls: slight overlap at joints
        else if (Mathf.Abs(ha - hb) > 1e-4f) { extStart += 0.003f; extEnd += 0.003f; }     // Banden on a slope: close the wedge gaps between the tilted pieces
        go.transform.localPosition = centre + axis.normalized * ((extEnd - extStart) / 2);
        go.transform.localRotation = rot;
        go.transform.localScale = new Vector3(thickness, above + below, len + extStart + extEnd);
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// Our objects are lit by the park's sun and ambient colour only - not by the hidden course's baked light probes,
    /// which tint every lane differently depending on where it stands.
    public static void NoProbes(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
    }

    static readonly float[] LineDark = { 0.13f, 0.13f, 0.14f };

    /// Painted line that follows the surface (no collider). `lift` raises it, e.g. onto an obstacle.
    static GameObject PaintLine(LaneDef d, string name, IList<Vector2> pts, float width, float[] color, float lift = 0f, bool closed = false)
    {
        var md = new LaneGeometry.MeshData();
        var path = new List<Vector2>();
        int n = pts.Count;
        for (int i = 0; i < (closed ? n : n - 1); i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % n];
            int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 0.025f));
            for (int k = 0; k < steps; k++) path.Add(Vector2.Lerp(a, b, (float)k / steps));
        }
        path.Add(closed ? pts[0] : pts[n - 1]);
        int prevL = -1, prevR = -1;
        for (int i = 0; i < path.Count; i++)
        {
            var dir = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            var nrm = new Vector2(dir.y, -dir.x) * (width / 2);
            Vector3 P(Vector2 q) => new Vector3(q.x, LaneGeometry.Height(d, q.x, q.y) + 0.002f + lift, q.y);
            int l = md.Add(P(path[i] - nrm)), r = md.Add(P(path[i] + nrm));
            if (prevL >= 0) md.Quad(prevL, l, r, prevR, Vector3.up);
            prevL = l; prevR = r;
        }
        return MeshPart(name, md, DoubleSided(MakeMaterial(color)), "Untagged", null, false, false);
    }

    /// Stroke letters for the lettering painted into the tee field (x 0..1 across the letter, y 0..1 up).
    internal static readonly Dictionary<char, float[][]> Strokes = new()
    {
        ['G'] = new[] { new[] { 1f, 0.8f, 0.72f, 1f, 0.3f, 1f, 0f, 0.75f, 0f, 0.25f, 0.3f, 0f, 0.72f, 0f, 1f, 0.2f, 1f, 0.48f, 0.55f, 0.48f } },
        ['O'] = new[] { new[] { 0.3f, 0f, 0f, 0.25f, 0f, 0.75f, 0.3f, 1f, 0.7f, 1f, 1f, 0.75f, 1f, 0.25f, 0.7f, 0f, 0.3f, 0f } },
        ['L'] = new[] { new[] { 0f, 1f, 0f, 0f, 1f, 0f } },
        ['F'] = new[] { new[] { 1f, 1f, 0f, 1f, 0f, 0f }, new[] { 0f, 0.52f, 0.8f, 0.52f } },
        ['M'] = new[] { new[] { 0f, 0f, 0f, 1f, 0.5f, 0.4f, 1f, 1f, 1f, 0f } },
        ['A'] = new[] { new[] { 0f, 0f, 0.5f, 1f, 1f, 0f }, new[] { 0.2f, 0.38f, 0.8f, 0.38f } },
        ['B'] = new[] { new[] { 0f, 0f, 0f, 1f, 0.62f, 1f, 0.9f, 0.88f, 0.9f, 0.64f, 0.62f, 0.52f, 0f, 0.52f }, new[] { 0.62f, 0.52f, 1f, 0.4f, 1f, 0.13f, 0.7f, 0f, 0f, 0f } },
        ['C'] = new[] { new[] { 1f, 0.8f, 0.72f, 1f, 0.3f, 1f, 0f, 0.75f, 0f, 0.25f, 0.3f, 0f, 0.72f, 0f, 1f, 0.2f } },
        ['E'] = new[] { new[] { 1f, 1f, 0f, 1f, 0f, 0f, 1f, 0f }, new[] { 0f, 0.52f, 0.8f, 0.52f } },
        ['H'] = new[] { new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 0f, 1f, 1f }, new[] { 0f, 0.5f, 1f, 0.5f } },
        ['I'] = new[] { new[] { 0f, 0f, 0f, 1f } },
        ['N'] = new[] { new[] { 0f, 0f, 0f, 1f, 1f, 0f, 1f, 1f } },
        ['R'] = new[] { new[] { 0f, 0f, 0f, 1f, 0.68f, 1f, 1f, 0.86f, 1f, 0.62f, 0.68f, 0.48f, 0f, 0.48f }, new[] { 0.55f, 0.48f, 1f, 0f } },
        ['T'] = new[] { new[] { 0f, 1f, 1f, 1f }, new[] { 0.5f, 1f, 0.5f, 0f } },
    };

    /// Paints lines of text centred on x = 0, readable from the tee end (letter tops towards the hole). One mesh, no collider.
    static GameObject PaintText(LaneDef d, string name, string[] lines, float zTop, float letterH, float letterW, float gap, float lineStep, float stroke, float[] color)
    {
        var md = new LaneGeometry.MeshData();
        Vector3 P(Vector2 q) => new Vector3(q.x, LaneGeometry.Height(d, q.x, q.y) + 0.002f, q.y);
        float hs = stroke / 2;
        for (int li = 0; li < lines.Length; li++)
        {
            float Adv(char c) => c == ' ' ? letterW * 0.7f : (c == 'I' ? stroke : letterW) + gap;
            float total = -gap; foreach (char c in lines[li]) total += Adv(c);
            float x = -total / 2 + hs, z0 = zTop - letterH - li * lineStep;
            foreach (char c in lines[li])
            {
                if (Strokes.TryGetValue(c, out var ss))
                    foreach (var s in ss)
                        for (int k = 0; k + 3 < s.Length; k += 2)
                        {
                            float lw = letterW - stroke, lh = letterH - stroke;
                            var a = new Vector2(x + s[k] * lw, z0 + hs + s[k + 1] * lh); var b = new Vector2(x + s[k + 2] * lw, z0 + hs + s[k + 3] * lh);
                            var dir = (b - a).normalized; var nrm = new Vector2(dir.y, -dir.x) * hs;
                            a -= dir * hs; b += dir * hs;                       // square caps close the corners
                            md.Quad(md.Add(P(a - nrm)), md.Add(P(b - nrm)), md.Add(P(b + nrm)), md.Add(P(a + nrm)), Vector3.up);
                        }
                x += Adv(c);
            }
        }
        return MeshPart(name, md, DoubleSided(MakeMaterial(color)), "Untagged", null, false, false);
    }

    /// Markings every lane has: the Abschlagfeld and the repositioning line inside the target circle.
    static void PaintMarkings(LaneDef d)
    {
        const float w = 0.006f;
        if (d.teeField)
            PaintLine(d, "Line_TeeField", new List<Vector2> { new(-0.25f, 0.01f), new(-0.25f, 0.40f), new(0.25f, 0.40f), new(0.25f, 0.01f) }, w, LineDark);
        if (d.teeField && d.teeSign)   // lettering in the tee field, as painted on many German courses (photo: Harrislee)
            PaintText(d, "Text_TeeField", new[] { "BAHNEN", "BITTE NICHT", "BETRETEN" }, 0.365f, 0.072f, 0.036f, 0.011f, 0.112f, 0.0085f, new[] { 0.36f, 0.36f, 0.37f });
        if (d.circleLine && d.HasCircle && d.circle == null)
        {
            float R = d.CircleRadius - 0.20f, cx = d.CircleX, cz = d.CircleZ, chord = 0.36f;
            float a0 = Mathf.Asin(-chord / R);                       // angle of the right-hand end of the chord (0 = +x, 90 deg = +z)
            var pts = new List<Vector2>();
            for (int i = 0; i <= 48; i++)
            {
                float a = Mathf.Lerp(a0, Mathf.PI - a0, i / 48f);
                pts.Add(new Vector2(cx + R * Mathf.Cos(a), cz + R * Mathf.Sin(a)));
            }
            PaintLine(d, "Line_Circle", pts, w, LineDark, 0f, true);
        }
    }

    /// One piece of Bande from a to b: its top follows the plate height at both ends and its end faces are vertical, so
    /// neighbouring pieces meet exactly (tilted boxes leave notches at the joints of curved lanes).
    static GameObject BandPiece(string name, Vector2 a, Vector2 b, float thickness, float above, float below, LaneDef d, string tag, Material mat,
                                float extStart, float extEnd)
    {
        var dir = (b - a).normalized;
        var inA = a + dir * 0.001f; var inB = b - dir * 0.001f;
        float ha = LaneGeometry.PlateHeight(d, inA.x, inA.y), hb = LaneGeometry.PlateHeight(d, inB.x, inB.y);
        a -= dir * extStart; b += dir * extEnd;
        var n = new Vector2(dir.y, -dir.x) * (thickness / 2);
        Vector3 P(Vector2 q, float y) => new Vector3(q.x, y, q.y);
        var v = new[] { P(a - n, ha - below), P(a + n, ha - below), P(a + n, ha + above), P(a - n, ha + above),
                        P(b - n, hb - below), P(b + n, hb - below), P(b + n, hb + above), P(b - n, hb + above) };
        var md = new LaneGeometry.MeshData();
        var centre = (v[0] + v[6]) / 2;
        void Face(int i0, int i1, int i2, int i3)
        {
            int q0 = md.Add(v[i0]), q1 = md.Add(v[i1]), q2 = md.Add(v[i2]), q3 = md.Add(v[i3]);
            md.Quad(q0, q1, q2, q3, (v[i0] + v[i1] + v[i2] + v[i3]) / 4 - centre);
        }
        Face(0, 1, 2, 3); Face(4, 5, 6, 7); Face(3, 2, 6, 7); Face(0, 1, 5, 4); Face(1, 2, 6, 5); Face(0, 3, 7, 4);
        return MeshPart(name, md, mat, tag, null, true);
    }

    static List<Vector2> Pts(float[][] p)
    {
        var l = new List<Vector2>();
        if (p != null) foreach (var q in p) if (q != null && q.Length >= 2) l.Add(new Vector2(q[0], q[1]));
        return l;
    }

    /// Build one part; returns the created objects (walls/arcs create several).
    static List<GameObject> BuildPart(LaneDef d, Part p, Material mat, PhysicMaterial phys)
    {
        var made = new List<GameObject>();
        switch (p.shape)
        {
            case "cylinder":
            {
                var pos = V(p.pos); pos.y += LaneGeometry.Height(d, pos.x, pos.z);
                made.Add(Cylinder(p.name, pos, p.size[0], p.size[1], V(p.rot), p.tag, mat));
                break;
            }
            case "prism":
                made.Add(MeshPart(p.name, LaneGeometry.Prism(d, Pts(p.points), p.height, p.baseY ?? -1f), mat, p.tag, phys, !p.concave));
                break;
            case "pyramid":
            {
                var pts = Pts(p.points);
                var apex = p.apex is { Length: >= 2 } ? new Vector2(p.apex[0], p.apex[1]) : Centroid(pts);
                made.Add(MeshPart(p.name, LaneGeometry.Pyramid(d, pts, apex, p.height), mat, p.tag, phys, true));
                break;
            }
            case "channel":
            {
                // smooth moulded channel: the ball slides along its walls with little loss
                var slick = new PhysicMaterial("LaneChannel") { bounciness = 0.3f, bounceCombine = PhysicMaterialCombine.Minimum,
                    dynamicFriction = 0.02f, staticFriction = 0.02f, frictionCombine = PhysicMaterialCombine.Minimum };
                made.Add(MeshPart(p.name, p.profile != null ? LaneGeometry.Sweep(p.points, p.profile) : LaneGeometry.Channel(p.points, p.width), DoubleSided(mat), p.tag, slick, false));
                break;
            }
            case "loop":
            {
                var go = MeshPart(p.name, LaneGeometry.Loop(p.radius, p.width, p.height, p.shift, p.entry, p.exit, p.cover),
                                  DoubleSided(mat), p.tag, phys, false);
                var pos = V(p.pos); pos.y += LaneGeometry.Height(d, pos.x, pos.z);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(V(p.rot));
                made.Add(go);
                break;
            }
            case "lathe":
            {
                var go = MeshPart(p.name, LaneGeometry.Lathe(Pts(p.profile), p.axis, p.gapFrom, p.gapTo, p.material != "outer"),
                                  DoubleSided(mat), p.tag, phys, false);
                var pos = V(p.pos); pos.y += LaneGeometry.Height(d, pos.x, pos.z);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(V(p.rot));
                made.Add(go);
                break;
            }
            case "frustum":
                made.Add(MeshPart(p.name, LaneGeometry.Frustum(d, Pts(p.points), Pts(p.top), p.height), mat, p.tag, phys, true));
                break;
            case "tube":
            {
                var a = V(p.a); a.y += LaneGeometry.Height(d, a.x, a.z);
                var b = V(p.b); b.y += LaneGeometry.Height(d, b.x, b.z);
                made.Add(MeshPart(p.name, LaneGeometry.Tube(a, b, p.diameter / 2, Mathf.Max(0.003f, p.thickness)), mat, p.tag, phys, false));
                break;
            }
            case "wall":
            {
                var pts = Pts(p.points);
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    // baseY: a strip lying on top of other walls (height = its own thickness), following the slope
                    var seg = Segment($"{p.name}_{i}", pts[i], pts[i + 1], p.thickness, p.height, p.baseY.HasValue ? 0f : 0.03f, d, p.tag, mat);
                    if (p.baseY.HasValue) seg.transform.localPosition += Vector3.up * p.baseY.Value;
                    made.Add(seg);
                }
                break;
            }
            case "line":
            {
                // painted marking (Grenzlinie, Ablegelinie): visible only, the ball rolls over it
                // default colour: red Grenzlinie; "dashed" style is not distinguished
                PaintLine(d, p.name, Pts(p.points), Mathf.Max(0.005f, p.thickness), p.color ?? new[] { 0.80f, 0.08f, 0.08f }, p.pos[1]);
                break;
            }
            case "arc":
            {
                var c = new Vector2(p.center[0], p.center[1]);
                float a0 = p.from * Mathf.Deg2Rad, a1 = p.to * Mathf.Deg2Rad;
                int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * p.radius / 0.05f));
                Vector2 P(int i) { float a = Mathf.Lerp(a0, a1, (float)i / n); return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * p.radius; }
                for (int i = 0; i < n; i++)
                    made.Add(Segment($"{p.name}_{i}", P(i), P(i + 1), p.thickness, p.height, 0.03f, d, p.tag, mat));
                break;
            }
            case "mesh":
            {
                var path = System.IO.Path.IsPathRooted(p.file) ? p.file : System.IO.Path.Combine(Plugin.LanesFolder, p.file);
                // slick: smooth moulded channel, the ball runs along its walls with little loss (same as "channel")
                var meshPhys = p.slick ? new PhysicMaterial("LaneChannel") { bounciness = 0.3f, bounceCombine = PhysicMaterialCombine.Minimum,
                    dynamicFriction = 0.02f, staticFriction = 0.02f, frictionCombine = PhysicMaterialCombine.Minimum } : p.surface ? SurfacePhys : phys;
                var go = MeshPart(p.name, LaneGeometry.LoadObj(path), DoubleSided(mat), p.tag, meshPhys, p.convex);
                go.transform.localPosition = V(p.pos);
                go.transform.localRotation = Quaternion.Euler(V(p.rot));
                made.Add(go);
                break;
            }
            default: // box
            {
                var pos = V(p.pos); pos.y += LaneGeometry.Height(d, pos.x, pos.z);
                made.Add(Box(p.name, pos, V(p.size), V(p.rot), p.tag, mat));
                break;
            }
        }
        return made;
    }

    static Vector2 Centroid(List<Vector2> pts) { var c = Vector2.zero; foreach (var q in pts) c += q; return pts.Count > 0 ? c / pts.Count : c; }

    /// Give every (non-trigger) collider under root a twin on the Cup layer (13).
    static void DuplicateToCupLayer(GameObject root)
    {
        foreach (var c in root.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || c.gameObject.layer == 13) continue;
            var twin = new GameObject(c.name + "_cup");
            twin.layer = 13;
            try { twin.tag = c.gameObject.tag; } catch { }
            twin.transform.SetParent(c.transform, false);
            var box = c.TryCast<BoxCollider>();
            if (box != null)
            {
                var t = twin.AddComponent<BoxCollider>();
                t.center = box.center; t.size = box.size; t.sharedMaterial = box.sharedMaterial;
                continue;
            }
            var mc = c.TryCast<MeshCollider>();
            if (mc != null)
            {
                var t = twin.AddComponent<MeshCollider>();
                t.convex = mc.convex; t.sharedMesh = mc.sharedMesh; t.sharedMaterial = mc.sharedMaterial;
            }
        }
    }

    // ---------- primitives ----------

    static Vector3 V(float[] a) => a is { Length: >= 3 } ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

    static GameObject Box(string name, Vector3 localPos, Vector3 size, Vector3 euler, string tag, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Setup(go, name, localPos, euler, tag, mat);
        go.transform.localScale = size;
        return go;
    }

    /// Box without collider (visual only).
    static GameObject Skirt(string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var go = Box(name, localPos, size, Vector3.zero, null, mat);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Cylinder(string name, Vector3 localPos, float diameter, float height, Vector3 euler, string tag, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        // Unity's cylinder is 2 units tall with a capsule collider; swap for an exact mesh collider.
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        mc.convex = true;
        Setup(go, name, localPos, euler, tag, mat);
        go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
        return go;
    }

    static void Setup(GameObject go, string name, Vector3 localPos, Vector3 euler, string tag, Material mat)
    {
        go.name = name;
        go.layer = 0;
        try { go.tag = string.IsNullOrEmpty(tag) ? "Untagged" : tag; }
        catch (Exception e) { Plugin.L.LogWarning($"tag '{tag}': {e.Message}"); }
        go.transform.SetParent(buildRoot.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ---------- materials ----------

    public static float BandBounce = 1.0f, ObstacleBounce = 0.9f;

    /// Physics material whose bounciness wins against the ball's own (0.9): combine = Minimum.
    /// Playing surface: sliding friction between ball and plate. The game's ball material has friction 0 and averages
    /// with the surface, so 0.6 here gives an effective coefficient of 0.3 (the value all earlier tests ran with -
    /// an estimate, not measured). This is what makes the ball roll, skid after a hard hit and react to its spin.
    public static float SurfaceFriction = 0.6f;
    static PhysicMaterial surfacePhys;
    static PhysicMaterial SurfacePhys => surfacePhys ??= new PhysicMaterial("LaneSurface")
    {
        bounciness = 0f, bounceCombine = PhysicMaterialCombine.Average,
        dynamicFriction = SurfaceFriction, staticFriction = SurfaceFriction, frictionCombine = PhysicMaterialCombine.Average,
    };

    static PhysicMaterial BounceMaterial(string name, float bounce)
    {
        return new PhysicMaterial("Lane" + name)
        {
            bounciness = Mathf.Clamp01(bounce),
            bounceCombine = PhysicMaterialCombine.Minimum,
            dynamicFriction = 0.2f,
            staticFriction = 0.2f,
            frictionCombine = PhysicMaterialCombine.Average,
        };
    }

    /// Change the bounce of all lanes' borders/obstacles live (no rebuild).
    public static void SetBounce(float band, float obstacle)
    {
        BandBounce = band; ObstacleBounce = obstacle;
        foreach (var l in lanes)
        {
            if (l.Root == null) continue;
            foreach (var c in l.Root.GetComponentsInChildren<Collider>())
            {
                var m = c.sharedMaterial;
                if (m == null || !m.name.StartsWith("Lane")) continue;
                m.bounciness = m.name.Contains("Band") ? band : obstacle;
            }
        }
    }

    static readonly Dictionary<Material, Material> doubleSided = new();

    /// Copy of a material that renders both faces (thin 3D parts like loops, nets, bowls).
    static readonly Dictionary<string, Material> transparent = new();
    /// See-through, double-sided version of a material (nets).
    internal static Material Transparent(Material m, float alpha)
    {
        string key = m.name + "_a" + alpha.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        if (transparent.TryGetValue(key, out var t) && t != null) return t;
        t = new Material(m) { name = key };
        if (t.HasProperty("_Surface")) t.SetFloat("_Surface", 1f);
        if (t.HasProperty("_Blend")) t.SetFloat("_Blend", 0f);
        if (t.HasProperty("_SrcBlend")) t.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (t.HasProperty("_DstBlend")) t.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (t.HasProperty("_ZWrite")) t.SetFloat("_ZWrite", 0f);
        if (t.HasProperty("_Cull")) t.SetFloat("_Cull", 0f);
        t.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        t.SetOverrideTag("RenderType", "Transparent");
        t.renderQueue = 3000;
        foreach (var prop in new[] { "_BaseColor", "_Color" })
            if (t.HasProperty(prop)) { var c = t.GetColor(prop); c.a = alpha; t.SetColor(prop, c); }
        transparent[key] = t;
        doubleSided[t] = t;
        return t;
    }

    static Material DoubleSided(Material m)
    {
        if (doubleSided.TryGetValue(m, out var d) && d != null) return d;
        d = new Material(m) { name = m.name + "_2s" };
        if (d.HasProperty("_Cull")) d.SetFloat("_Cull", 0f);
        doubleSided[m] = d;
        return d;
    }

    static Shader litShader;
    static bool shaderLogged;

    /// A plain coloured material using the game's URP shader (falls back to whatever a cube primitive gets).
    public static Material MakeMaterial(float[] rgb)
    {
        var c = rgb is { Length: >= 3 } ? new Color(rgb[0], rgb[1], rgb[2]) : Color.gray;
        string key = $"{c.r:F3},{c.g:F3},{c.b:F3}";
        if (matCache.TryGetValue(key, out var cached) && cached != null) return cached;

        if (litShader == null)
        {
            foreach (var n in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit", "Standard" })
            {
                litShader = Shader.Find(n);
                if (litShader != null) break;
            }
            if (!shaderLogged) { shaderLogged = true; Plugin.L.LogInfo("Lane shader: " + (litShader != null ? litShader.name : "none found, using primitive default")); }
        }

        Material m;
        if (litShader != null) m = new Material(litShader);
        else
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m = new Material(tmp.GetComponent<MeshRenderer>().sharedMaterial);
            UnityEngine.Object.Destroy(tmp);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        // matt paint; no reflections of the hidden course's surroundings
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.name = "Lane_" + key;
        matCache[key] = m;
        return m;
    }
}
