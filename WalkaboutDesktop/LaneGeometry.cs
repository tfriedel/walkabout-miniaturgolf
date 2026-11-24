using System;
using System.Collections.Generic;
using UnityEngine;

namespace WalkaboutDesktop;

/// Pure geometry for a lane in lane coordinates (x right, z along the lane, y up).
public static class LaneGeometry
{
    // ---------- height profile ----------

    public static float Height(LaneDef d, float x, float z)
    {
        float h = 0f, raised = 0f;
        if (d.surface == null) return h;
        foreach (var f in d.surface)
        {
            float c = Contribution(f, x, z, out bool isRaised);
            if (isRaised) raised = Mathf.Max(raised, c); else h += c;
        }
        return h + raised;
    }

    /// Height of the lane plate itself, without the moulded obstacles that sit on it (coloured features).
    /// The Banden follow this, not the obstacles.
    public static float PlateHeight(LaneDef d, float x, float z)
    {
        float h = 0f, raised = 0f;
        if (d.surface == null) return h;
        foreach (var f in d.surface)
        {
            if (f.color != null) continue;
            float c = Contribution(f, x, z, out bool isRaised);
            if (isRaised) raised = Mathf.Max(raised, c); else h += c;
        }
        return h + raised;
    }

    /// Index of the coloured feature that shapes the surface at (x, z), or -1 for the plain plate.
    public static int Owner(LaneDef d, float x, float z)
    {
        if (d.surface == null) return -1;
        for (int i = d.surface.Count - 1; i >= 0; i--)
        {
            var f = d.surface[i];
            if (f.color == null) continue;
            if (f.type == "vulkan" ? VulkanFootprint(f, x, z) : Contribution(f, x, z, out _) > 1e-4f) return i;
        }
        return -1;
    }

    /// Height added by one feature. isRaised: combine by maximum with other raised features instead of adding.
    public static float Contribution(Feature f, float x, float z, out bool isRaised)
    {
        isRaised = f.type is "ramp" or "plateau" or "halfcone" or "vulkan" or "rohrhuegel" or "schanze";
        if (f.x0.HasValue && x < f.x0.Value) return 0f;
        if (f.x1.HasValue && x > f.x1.Value) return 0f;
        switch (f.type)
        {
            case "slope":
                if (z <= f.z0) return 0f;
                return f.height * Mathf.Clamp01((z - f.z0) / Mathf.Max(1e-4f, f.z1 - f.z0));
            case "wedge":
                if (z <= f.z0 || z > f.z1) return 0f;
                return f.height * (z - f.z0) / Mathf.Max(1e-4f, f.z1 - f.z0);
            case "hump":
                if (z <= f.z0 || z >= f.z1) return 0f;
                if (f.peak.HasValue) // crest not in the middle: two cosine halves
                    return z < f.peak.Value
                        ? f.height * 0.5f * (1f - Mathf.Cos(Mathf.PI * (z - f.z0) / (f.peak.Value - f.z0)))
                        : f.height * 0.5f * (1f + Mathf.Cos(Mathf.PI * (z - f.peak.Value) / (f.z1 - f.peak.Value)));
                return f.height * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * (z - f.z0) / (f.z1 - f.z0)));
            case "profile":
            {
                // Height profile along the lane through the points [z, h] (smooth, Catmull-Rom), level across.
                var q = f.points;
                if (q == null || q.Length < 2 || z <= q[0][0] || z >= q[q.Length - 1][0]) return 0f;
                int i = 0; while (i + 2 < q.Length && z > q[i + 1][0]) i++;
                float[] p0 = q[Mathf.Max(i - 1, 0)], p1 = q[i], p2 = q[i + 1], p3 = q[Mathf.Min(i + 2, q.Length - 1)];
                float t = (z - p1[0]) / (p2[0] - p1[0]);
                // tangents (dh/dz) from the neighbours, scaled to this interval
                float m1 = (p2[1] - p0[1]) / Mathf.Max(1e-5f, p2[0] - p0[0]) * (p2[0] - p1[0]);
                float m2 = (p3[1] - p1[1]) / Mathf.Max(1e-5f, p3[0] - p1[0]) * (p2[0] - p1[0]);
                float t2 = t * t, t3 = t2 * t;
                return Mathf.Max(0f, (2 * t3 - 3 * t2 + 1) * p1[1] + (t3 - 2 * t2 + t) * m1 + (-2 * t3 + 3 * t2) * p2[1] + (t3 - t2) * m2);
            }
            case "barrel":
            {
                // Segment of a cylinder lying across the lane: circular arc along the lane from z0 to z1, level across.
                // The feet are rounded into the plate (no crease), as on the real moulding.
                if (z <= f.z0 - 0.20f || z >= f.z1 + 0.20f) return 0f;
                float half = (f.z1 - f.z0) / 2, dz = z - (f.z0 + f.z1) / 2;
                float R = (half * half + f.height * f.height) / (2f * f.height);
                float arc = Mathf.Sqrt(Mathf.Max(0f, R * R - dz * dz)) - (R - f.height);
                const float e = 0.012f;
                float fade = Mathf.Clamp01((half + 0.20f - Mathf.Abs(dz)) / 0.08f);
                return (arc + Mathf.Sqrt(arc * arc + e * e)) / 2f * fade;
            }
            case "dome":
            {
                float r = Mathf.Sqrt((x - f.x) * (x - f.x) + (z - f.z) * (z - f.z));
                if (r >= f.radius) return 0f;
                return f.height * 0.5f * (1f + Mathf.Cos(Mathf.PI * r / f.radius));
            }
            case "ramp":
                if (z <= f.z0 || z > f.z1) return 0f;
                return f.height * Mathf.Pow((z - f.z0) / Mathf.Max(1e-4f, f.z1 - f.z0), Mathf.Max(0.1f, f.curve));
            case "plateau":
                return f.points != null && InPolygon(Pts(f.points), x, z) ? f.height : 0f;
            case "halfcone":
            {
                // Part of a truncated cone lying across the lane: circular-segment cross-sections from end a to end b.
                if (f.a == null || f.b == null) return 0f;
                float t = (x - f.a[0]) / (f.b[0] - f.a[0]);
                if (t < 0f || t > 1f) return 0f;
                float w = Mathf.Lerp(f.a[1], f.b[1], t), hh = Mathf.Lerp(f.a[2], f.b[2], t), dz = Mathf.Abs(z - f.z);
                if (dz >= w) return 0f;
                float R = (w * w + hh * hh) / (2f * hh);
                return Mathf.Sqrt(R * R - dz * dz) - (R - hh);
            }
            case "vulkan":
                return Vulkan(f, x, z);
            case "schanze":
            {
                // Moulded jump ramp: triangular in plan (tip towards the tee), rising to a ridge at z1; a flat groove
                // of `width` runs up the middle, the sides fall away like a roof; steep back face behind the ridge.
                const float back = 0.12f;
                if (z <= f.z0 || z >= f.z1 + back) return 0f;
                float s = Mathf.Clamp01((z - f.z0) / Mathf.Max(1e-4f, f.z1 - f.z0));
                float top = z <= f.z1 ? f.height * Mathf.Pow(s, Mathf.Max(0.1f, f.curve)) : f.height * (1f - (z - f.z1) / back);
                float ax = Mathf.Abs(x), g = f.width / 2, w = Mathf.Max(g + 0.01f, 0.45f * s);
                if (ax <= g) return top;
                if (ax >= w) return 0f;
                return top * (1f - (ax - g) / (w - g));
            }
            case "rohrhuegel":
            {
                // Moulded hill across the whole lane from z0 to z1: a valley narrows and rises to the pipe mouth
                // (height `height` at z = `z`), the shoulders beside it rise to `r1`. Behind the mouth a slot of
                // `width` stays open for the pipe.
                if (z < f.z0 || z > f.z1) return 0f;
                float s = Mathf.Clamp01((z - f.z0) / Mathf.Max(1e-4f, f.z - f.z0));
                float floor = f.height * Mathf.Pow(s, Mathf.Max(0.1f, f.curve));
                float sm = s * s * (3f - 2f * s);
                float shoulder = Mathf.Max(floor, f.r1 * sm);
                float ax = Mathf.Abs(x);
                float edgeFade = Mathf.Clamp01((0.445f - ax) / 0.03f) * Mathf.Clamp01((f.z1 - z) / 0.03f); // sides and back slope down to the plate
                if (z > f.z) return ax < f.width / 2 ? f.height : shoulder * edgeFade;
                float w = Mathf.Lerp(0.45f, f.width / 2, Mathf.Pow(s, 0.6f));   // valley half width
                float t = Mathf.Clamp01((ax - 0.5f * w) / Mathf.Max(0.02f, 0.5f * w + 0.06f));
                return (floor + (shoulder - floor) * t * t * (3f - 2f * t)) * Mathf.Lerp(1f, edgeFade, t);
            }
        }
        return 0f;
    }

    // Vulkan: truncated cone (base `radius`, top `r1`, `height`) with a rimmed, dished plateau around the hole at
    // (x, z) and a straight walled chute of inner `width` from z0 on the lane up into the plateau.
    const float VWallT = 0.025f, VRimW = 0.03f;
    static bool VulkanFootprint(Feature f, float x, float z)
    {
        float dx = x - f.x, dz = z - f.z;
        if (dx * dx + dz * dz < f.radius * f.radius) return true;
        return Mathf.Abs(dx) < f.width / 2 + VWallT && z >= f.z0 && z <= f.z;
    }

    static float Vulkan(Feature f, float x, float z)
    {
        float dx = x - f.x, dz = z - f.z, r = Mathf.Sqrt(dx * dx + dz * dz);
        float H = f.height, R = f.radius, Rt = f.r1, Ri = Rt - VRimW, rimH = f.rim, wallH = f.wall, dish = 0.015f;
        float floorEdge = H - rimH;                       // plateau floor at its edge = top of the chute
        float zEnd = f.z - Ri;                            // the chute ends where the plateau floor begins
        float ax = Mathf.Abs(dx), half = f.width / 2;
        bool inChuteZ = z >= f.z0 && z <= zEnd;
        float s = Mathf.Clamp01((z - f.z0) / Mathf.Max(1e-4f, zEnd - f.z0));
        float chute = floorEdge * Mathf.Pow(s, Mathf.Max(0.1f, f.curve));
        if (inChuteZ && ax < half) return chute;                                    // chute floor (recessed in the cone)
        if (r < Ri) return floorEdge - dish * (1f - r / Ri);                        // dished plateau floor
        if (r < Rt && dz < 0 && ax < half) return floorEdge;                        // opening in the rim
        float cone = r <= Rt ? H : r < R ? H * (R - r) / (R - Rt) : 0f;
        if (inChuteZ && ax < half + VWallT) return Mathf.Max(cone, chute + wallH * Mathf.Clamp01(s * 8f));
        return cone;
    }

    public static Vector2 Gradient(LaneDef d, float x, float z)
    {
        const float e = 0.01f;
        return new Vector2((Height(d, x + e, z) - Height(d, x - e, z)) / (2 * e),
                           (Height(d, x, z + e) - Height(d, x, z - e)) / (2 * e));
    }

    // ---------- outline: polygon (default: straight rectangle up to the circle centre) united with the target circle ----------

    public static List<Vector2> Pts(float[][] p)
    {
        var l = new List<Vector2>();
        if (p != null) foreach (var q in p) if (q != null && q.Length >= 2) l.Add(new Vector2(q[0], q[1]));
        return l;
    }

    static readonly Dictionary<LaneDef, List<Vector2>> polyCache = new();

    public static List<Vector2> Polygon(LaneDef d)
    {
        if (polyCache.TryGetValue(d, out var p)) return p;
        if (d.outline != null && d.outline.Length >= 3) p = Pts(d.outline);
        else
        {
            float W = d.width / 2, end = d.HasCircle ? d.CircleZ : d.length;
            p = new List<Vector2> { new(-W, 0), new(W, 0), new(W, end), new(-W, end) };
        }
        polyCache[d] = p;
        return p;
    }

    public static bool InPolygon(List<Vector2> poly, float x, float z)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    static bool InCircle(LaneDef d, float x, float z)
    {
        if (!d.HasCircle) return false;
        float dx = x - d.CircleX, dz = z - d.CircleZ;
        return dx * dx + dz * dz <= d.CircleRadius * d.CircleRadius;
    }

    public static bool Inside(LaneDef d, float x, float z) => InPolygon(Polygon(d), x, z) || InCircle(d, x, z);

    static Vector2 NearestOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
        return a + ab * t;
    }

    /// Nearest point of the lane outline (polygon or circle) for a point outside it.
    public static Vector2 Snap(LaneDef d, float x, float z)
    {
        var p = new Vector2(x, z);
        var poly = Polygon(d);
        Vector2 best = poly[0]; float bd = float.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            var q = NearestOnSegment(p, poly[i], poly[(i + 1) % poly.Count]);
            float dd = (q - p).sqrMagnitude;
            if (dd < bd) { bd = dd; best = q; }
        }
        if (d.HasCircle)
        {
            var c = new Vector2(d.CircleX, d.CircleZ);
            var dir = p - c;
            var q = dir.sqrMagnitude < 1e-8f ? c : c + dir.normalized * d.CircleRadius;
            if ((q - p).sqrMagnitude < bd) best = q;
        }
        return best;
    }

    /// Short border segments along the boundary of polygon ∪ circle (no ordering needed: each becomes one box).
    /// isStart marks segments on the tee edge (z = 0) of the default rectangle.
    public static List<(Vector2 a, Vector2 b, bool isStart)> BorderSegments(LaneDef d, float step = 0.10f)
    {
        var segs = new List<(Vector2, Vector2, bool)>();
        var poly = Polygon(d);
        bool defaultRect = d.outline == null || d.outline.Length < 3;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / step));
            bool isStart = defaultRect && Mathf.Abs(a.y) < 1e-4f && Mathf.Abs(b.y) < 1e-4f;
            for (int k = 0; k < n; k++)
            {
                var p0 = Vector2.Lerp(a, b, (float)k / n); var p1 = Vector2.Lerp(a, b, (float)(k + 1) / n);
                // keep only the part outside the circle, cut exactly where the edge meets the circle
                bool in0 = InCircleStrict(d, p0), in1 = InCircleStrict(d, p1);
                if (in0 && in1) continue;
                if (in0 != in1)
                {
                    var cut = Bisect(p0, p1, q => InCircleStrict(d, q));
                    if (in0) p0 = cut; else p1 = cut;
                    if ((p1 - p0).sqrMagnitude < 1e-8f) continue;
                }
                segs.Add((p0, p1, isStart));
            }
        }
        if (d.HasCircle)
        {
            var c = new Vector2(d.CircleX, d.CircleZ);
            int n = 96;
            for (int k = 0; k < n; k++)
            {
                float a0 = 2 * Mathf.PI * k / n, a1 = 2 * Mathf.PI * (k + 1) / n;
                var p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * d.CircleRadius;
                var p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * d.CircleRadius;
                // keep only the part of the circle outside the polygon, cut exactly where the two outlines meet
                bool Covered(Vector2 q) { var o = q + (q - c).normalized * 0.002f; return InPolygon(poly, o.x, o.y); }
                bool c0 = Covered(p0), c1 = Covered(p1);
                if (c0 && c1) continue;
                if (c0 != c1)
                {
                    float lo = a0, hi = a1; // bisect on the angle so the cut point stays on the circle
                    for (int it = 0; it < 14; it++)
                    {
                        float am = (lo + hi) / 2;
                        var qm = c + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * d.CircleRadius;
                        if (Covered(qm) == c0) lo = am; else hi = am;
                    }
                    var cut = c + new Vector2(Mathf.Cos((lo + hi) / 2), Mathf.Sin((lo + hi) / 2)) * d.CircleRadius;
                    if (c0) p0 = cut; else p1 = cut;
                    if ((p1 - p0).sqrMagnitude < 1e-8f) continue;
                }
                segs.Add((p0, p1, false));
            }
        }
        return segs;
    }

    static Vector2 Bisect(Vector2 a, Vector2 b, Func<Vector2, bool> test)
    {
        bool ta = test(a);
        for (int it = 0; it < 14; it++)
        {
            var m = (a + b) / 2;
            if (test(m) == ta) a = m; else b = m;
        }
        return (a + b) / 2;
    }

    static bool InCircleStrict(LaneDef d, Vector2 p)
    {
        if (!d.HasCircle) return false;
        float r = d.CircleRadius - 0.005f;
        return (p - new Vector2(d.CircleX, d.CircleZ)).sqrMagnitude < r * r;
    }

    public static (Vector2 min, Vector2 max) Bounds(LaneDef d)
    {
        var poly = Polygon(d);
        var mn = poly[0]; var mx = poly[0];
        foreach (var p in poly) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
        if (d.HasCircle)
        {
            var c = new Vector2(d.CircleX, d.CircleZ); var r = new Vector2(d.CircleRadius, d.CircleRadius);
            mn = Vector2.Min(mn, c - r); mx = Vector2.Max(mx, c + r);
        }
        return (mn, mx);
    }

    // ---------- mesh building ----------

    public class MeshData
    {
        public readonly List<Vector3> V = new();
        public readonly List<int> T = new();

        public int Add(Vector3 v) { V.Add(v); return V.Count - 1; }

        /// Triangle whose front face points along `normal` (Unity: clockwise when seen from the front).
        public void Tri(int a, int b, int c, Vector3 normal)
        {
            var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(n, normal) >= 0) { T.Add(a); T.Add(b); T.Add(c); }
            else { T.Add(a); T.Add(c); T.Add(b); }
        }

        public void Quad(int a, int b, int c, int d, Vector3 normal) { Tri(a, b, c, normal); Tri(a, c, d, normal); }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = V.ToArray();
            m.triangles = T.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    public const float Grid = 0.04f;          // surface grid spacing
    public const float HolePatchHalf = 0.12f; // square around the hole carrying the round opening (multiple of Grid)
    public const int HoleSegments = 32;

    /// Playing surface with a real round hole. Grid lines are aligned to the hole so the hole patch fits exactly.
    public static MeshData Surface(LaneDef d, float holeR, int owner = -1)
    {
        var md = new MeshData();
        float cx = d.cup[0], cz = d.cup[1], hp = HolePatchHalf, s = d.grid > 0 ? d.grid : Grid;
        var (bmin, bmax) = Bounds(d);
        int i0 = Mathf.FloorToInt((bmin.x - s - cx) / s), i1 = Mathf.CeilToInt((bmax.x + s - cx) / s);
        int j0 = Mathf.FloorToInt((bmin.y - s - cz) / s), j1 = Mathf.CeilToInt((bmax.y + s - cz) / s);
        bool withHole = d.cupHole;
        bool holeHere = withHole && Owner(d, cx, cz) == owner;
        var cache = new Dictionary<long, int>();
        int Vert(int i, int j)
        {
            long key = ((long)i << 32) ^ (uint)j;
            if (cache.TryGetValue(key, out int idx)) return idx;
            float x = cx + i * s, z = cz + j * s;
            if (!Inside(d, x, z)) { var p = Snap(d, x, z); x = p.x; z = p.y; }
            idx = md.Add(new Vector3(x, Height(d, x, z), z));
            cache[key] = idx;
            return idx;
        }
        bool coloured = false;
        if (d.surface != null) foreach (var f in d.surface) if (f.color != null) coloured = true;
        var cx4 = new float[4]; var cz4 = new float[4]; var in4 = new bool[4]; var poly = new List<int>();
        var freeCache = new Dictionary<long, int>();
        int Free(float x, float z)
        {
            long key = ((long)Mathf.RoundToInt(x * 20000f) << 32) ^ (uint)Mathf.RoundToInt(z * 20000f);
            if (freeCache.TryGetValue(key, out int idx)) return idx;
            idx = md.Add(new Vector3(x, Height(d, x, z), z));
            freeCache[key] = idx;
            return idx;
        }
        int hpCells = Mathf.RoundToInt(hp / s);
        for (int i = i0; i < i1; i++)
            for (int j = j0; j < j1; j++)
            {
                if (withHole && i >= -hpCells && i < hpCells && j >= -hpCells && j < hpCells) continue; // hole patch
                float x0 = cx + i * s, z0 = cz + j * s;
                bool any = Inside(d, x0, z0) || Inside(d, x0 + s, z0) || Inside(d, x0, z0 + s) || Inside(d, x0 + s, z0 + s);
                if (!any) continue;
                if (!coloured) { md.Quad(Vert(i, j), Vert(i, j + 1), Vert(i + 1, j + 1), Vert(i + 1, j), Vector3.up); continue; }
                // Cells crossed by the edge of a coloured obstacle are cut along that edge, so both sides get a smooth outline.
                cx4[0] = x0; cz4[0] = z0; cx4[1] = x0; cz4[1] = z0 + s; cx4[2] = x0 + s; cz4[2] = z0 + s; cx4[3] = x0 + s; cz4[3] = z0;
                int nIn = 0;
                for (int k = 0; k < 4; k++) { in4[k] = Owner(d, cx4[k], cz4[k]) == owner; if (in4[k]) nIn++; }
                if (nIn == 0) continue;
                if (nIn == 4) { md.Quad(Vert(i, j), Vert(i, j + 1), Vert(i + 1, j + 1), Vert(i + 1, j), Vector3.up); continue; }
                poly.Clear();
                for (int k = 0; k < 4; k++)
                {
                    int k1 = (k + 1) % 4;
                    if (in4[k]) poly.Add(Free(cx4[k], cz4[k]));
                    if (in4[k] != in4[k1])
                    {
                        // bisect for the crossing point; always ordered from the lower to the higher corner so that
                        // the neighbouring mesh finds exactly the same point
                        bool swap = cx4[k] > cx4[k1] || cz4[k] > cz4[k1];
                        float ax = swap ? cx4[k1] : cx4[k], az = swap ? cz4[k1] : cz4[k], bx = swap ? cx4[k] : cx4[k1], bz = swap ? cz4[k] : cz4[k1];
                        bool ina = Owner(d, ax, az) == owner;
                        for (int it = 0; it < 10; it++)
                        {
                            float mx = (ax + bx) / 2, mz = (az + bz) / 2;
                            if ((Owner(d, mx, mz) == owner) == ina) { ax = mx; az = mz; } else { bx = mx; bz = mz; }
                        }
                        poly.Add(Free((ax + bx) / 2, (az + bz) / 2));
                    }
                }
                for (int k = 1; k + 1 < poly.Count; k++) md.Tri(poly[0], poly[k], poly[k + 1], Vector3.up);
            }

        if (!holeHere) return md;

        // Hole patch: square boundary (on the grid) to a ring at the hole radius.
        // The square edge is subdivided like the grid so it meets the neighbouring cells without cracks.
        var outer = new List<Vector2>();
        for (int k = -hpCells; k < hpCells; k++) outer.Add(new Vector2(cx + k * s, cz - hp));   // bottom, left->right
        for (int k = -hpCells; k < hpCells; k++) outer.Add(new Vector2(cx + hp, cz + k * s));   // right, up
        for (int k = hpCells; k > -hpCells; k--) outer.Add(new Vector2(cx + k * s, cz + hp));   // top, right->left
        for (int k = hpCells; k > -hpCells; k--) outer.Add(new Vector2(cx - hp, cz + k * s));   // left, down
        // ring point per outer point, at the same angle
        int n = outer.Count;
        var oi = new int[n]; var ri = new int[n];
        for (int k = 0; k < n; k++)
        {
            var o = outer[k];
            oi[k] = md.Add(new Vector3(o.x, Height(d, o.x, o.y), o.y));
            var dir = (o - new Vector2(cx, cz)).normalized;
            var r = new Vector2(cx, cz) + dir * holeR;
            ri[k] = md.Add(new Vector3(r.x, Height(d, r.x, r.y), r.y));
        }
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            md.Quad(ri[k], oi[k], oi[k1], ri[k1], Vector3.up);
        }
        return md;
    }

    /// Cup: tube from the surface down `depth` below the surface height at the hole centre, plus bottom.
    public static MeshData Cup(LaneDef d, float r, float depth, bool inwardNormals)
    {
        var md = new MeshData();
        float cx = d.cup[0], cz = d.cup[1], hc = Height(d, cx, cz);
        int n = HoleSegments * 2;
        var top = new int[n]; var bot = new int[n];
        for (int k = 0; k < n; k++)
        {
            float a = 2 * Mathf.PI * k / n;
            float x = cx + r * Mathf.Cos(a), z = cz + r * Mathf.Sin(a);
            top[k] = md.Add(new Vector3(x, Height(d, x, z), z));
            bot[k] = md.Add(new Vector3(x, hc - depth, z));
        }
        int centre = md.Add(new Vector3(cx, hc - depth, cz));
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            var mid = (md.V[top[k]] + md.V[top[k1]]) / 2;
            var inward = new Vector3(cx - mid.x, 0, cz - mid.z);
            md.Quad(top[k], top[k1], bot[k1], bot[k], inwardNormals ? inward : -inward);
            md.Tri(centre, bot[k], bot[k1], Vector3.up);
        }
        return md;
    }

    /// Pyramid: base polygon on the surface, apex at `height` above the surface (convex).
    public static MeshData Pyramid(LaneDef d, IList<Vector2> pts, Vector2 apex, float height)
    {
        var md = new MeshData();
        float minH = float.MaxValue;
        foreach (var p in pts) minH = Mathf.Min(minH, Height(d, p.x, p.y));
        int n = pts.Count;
        var b = new int[n];
        for (int k = 0; k < n; k++) b[k] = md.Add(new Vector3(pts[k].x, minH - 0.02f, pts[k].y));
        var c = Vector2.zero; foreach (var p in pts) c += p; c /= n;
        int top = md.Add(new Vector3(apex.x, Height(d, apex.x, apex.y) + height, apex.y));
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            var mid = (pts[k] + pts[k1]) / 2;
            md.Tri(b[k], b[k1], top, new Vector3(mid.x - c.x, 0.3f, mid.y - c.y));
        }
        return md;
    }

    /// Minimal Wavefront OBJ reader (v / f lines, polygons fanned into triangles).
    /// Convention (our Blender scripts write it this way): v = lane (x, y-up, z) i.e. Blender (x, z, y);
    /// swapping Blender's y/z mirrors the handedness, which turns Blender's counter-clockwise faces into Unity's clockwise.
    public static MeshData LoadObj(string path)
    {
        var md = new MeshData();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var raw in System.IO.File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("v "))
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                md.V.Add(new Vector3(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv)));
            }
            else if (line.StartsWith("f "))
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var idx = new int[p.Length - 1];
                for (int i = 1; i < p.Length; i++)
                {
                    int v = int.Parse(p[i].Split('/')[0], inv);
                    idx[i - 1] = v > 0 ? v - 1 : md.V.Count + v;
                }
                for (int i = 1; i < idx.Length - 1; i++) { md.T.Add(idx[0]); md.T.Add(idx[i]); md.T.Add(idx[i + 1]); }
            }
        }
        return md;
    }

    /// Truncated pyramid / cone: base outline on the surface, `top` outline (same vertex count) at `height`. Convex.
    public static MeshData Frustum(LaneDef d, IList<Vector2> bottom, IList<Vector2> top, float height)
    {
        var md = new MeshData();
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (var p in bottom) { float h = Height(d, p.x, p.y); minH = Mathf.Min(minH, h); maxH = Mathf.Max(maxH, h); }
        int n = bottom.Count;
        var c = Vector2.zero; foreach (var p in bottom) c += p; c /= n;
        var b = new int[n]; var t = new int[n];
        for (int k = 0; k < n; k++)
        {
            b[k] = md.Add(new Vector3(bottom[k].x, minH - 0.02f, bottom[k].y));
            t[k] = md.Add(new Vector3(top[k].x, maxH + height, top[k].y));
        }
        // flat faces with crisp edges: the top and every side get their own vertices
        var tt = new int[n];
        for (int k = 0; k < n; k++) tt[k] = md.Add(md.V[t[k]]);
        for (int k = 1; k < n - 1; k++) md.Tri(tt[0], tt[k], tt[k + 1], Vector3.up);
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            var mid = (bottom[k] + bottom[k1]) / 2;
            int q0 = md.Add(md.V[b[k]]), q1 = md.Add(md.V[b[k1]]), q2 = md.Add(md.V[t[k1]]), q3 = md.Add(md.V[t[k]]);
            md.Quad(q0, q1, q2, q3, new Vector3(mid.x - c.x, 0.2f, mid.y - c.y));
        }
        return md;
    }

    /// Looping channel: straight entry along +z, one vertical loop of `radius` (channel floor) that drifts sideways
    /// by `shift`, then a straight exit. Floor faces the loop centre, side walls face the channel. Local coordinates
    /// relative to the entry start (0,0,0).
    public static MeshData Loop(float radius, float width, float wallH, float shift, float entry, float exit, bool cover = true)
    {
        var md = new MeshData();
        var path = new List<(Vector3 p, Vector3 n)>();
        int ne = Mathf.Max(2, Mathf.CeilToInt(entry / 0.05f));
        for (int i = 0; i <= ne; i++) path.Add((new Vector3(0, 0, -entry + entry * i / ne + entry), Vector3.up));
        float z0 = entry; // loop starts here
        int nl = 96;
        for (int i = 1; i <= nl; i++)
        {
            float t = (float)i / nl, th = 2 * Mathf.PI * t;
            float s = t * t * (3 - 2 * t); // smooth sideways drift
            var p = new Vector3(shift * s, radius * (1 - Mathf.Cos(th)), z0 + radius * Mathf.Sin(th));
            var c = new Vector3(p.x, radius, z0);
            path.Add((p, (c - p).normalized));
        }
        int nx = Mathf.Max(2, Mathf.CeilToInt(exit / 0.05f));
        for (int i = 1; i <= nx; i++) path.Add((new Vector3(shift, 0, z0 + exit * i / nx), Vector3.up));

        var fl = new int[path.Count]; var fr = new int[path.Count]; var wl = new int[path.Count]; var wr = new int[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            var (p, n) = path[i];
            var tan = i + 1 < path.Count ? path[i + 1].p - p : p - path[i - 1].p;
            var side = Vector3.Cross(n, tan.normalized).normalized; // lateral
            fl[i] = md.Add(p - side * width / 2); fr[i] = md.Add(p + side * width / 2);
            wl[i] = md.Add(p - side * width / 2 + n * wallH); wr[i] = md.Add(p + side * width / 2 + n * wallH);
        }
        int loopStart = ne, loopEnd = ne + nl; // path indices of the loop part
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var n = path[i].n;
            var tan = path[i + 1].p - path[i].p;
            var side = Vector3.Cross(n, tan.normalized).normalized;
            bool inLoop = i >= loopStart && i < loopEnd;
            // floor only inside the loop: on the straights the lane surface is the floor (a second coplanar floor makes the ball hop)
            if (inLoop) md.Quad(fl[i], fl[i + 1], fr[i + 1], fr[i], n);
            md.Quad(fl[i], fl[i + 1], wl[i + 1], wl[i], side);       // left wall faces into the channel
            md.Quad(fr[i], fr[i + 1], wr[i + 1], wr[i], -side);      // right wall faces into the channel
            // cover over the upper half of the loop: a closed band there, like the real ring, so the ball cannot drop out
            float th = 2 * Mathf.PI * (i - loopStart) / nl;
            if (cover && inLoop && th > Mathf.PI * 0.35f && th < Mathf.PI * 1.65f) md.Quad(wl[i], wl[i + 1], wr[i + 1], wr[i], -n);
        }
        return md;
    }

    /// Open channel swept along a centre line. Each point: x, y, z, left wall height, right wall height, floor (1/0).
    /// Left/right as seen in the direction of travel. Without floor the lane surface is the floor.
    /// Moulded channel: a cross-section `profile` swept along the centre line. Profile points are
    /// [offset to the right of travel, height, absolute]: height is above the channel floor (limited by the path
    /// point's cap), or with absolute = 1 the point sits at the path point's base level (skirts down to the plate).
    /// Path points: x, floor y, z, cap, base.
    public static MeshData Sweep(float[][] pts, float[][] profile)
    {
        var md = new MeshData();
        int n = pts.Length, m = profile.Length;
        var idx = new int[n, m];
        var tans = new Vector3[n]; var sides = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var p = new Vector3(pts[i][0], pts[i][1], pts[i][2]);
            var a = i > 0 ? new Vector3(pts[i - 1][0], 0, pts[i - 1][2]) : new Vector3(p.x, 0, p.z);
            var b = i + 1 < n ? new Vector3(pts[i + 1][0], 0, pts[i + 1][2]) : new Vector3(p.x, 0, p.z);
            tans[i] = (b - a).normalized;
            sides[i] = Vector3.Cross(Vector3.up, tans[i]).normalized;
            float cap = pts[i].Length > 3 ? pts[i][3] : 10f, bas = pts[i].Length > 4 ? pts[i][4] : 0f;
            for (int k = 0; k < m; k++)
            {
                bool abs = profile[k].Length > 2 && profile[k][2] > 0.5f;
                float y = abs ? bas : p.y + Mathf.Min(profile[k][1], cap);
                idx[i, k] = md.Add(new Vector3(p.x, 0, p.z) + sides[i] * profile[k][0] + Vector3.up * y);
            }
        }
        for (int i = 0; i + 1 < n; i++)
            for (int k = 0; k + 1 < m; k++)
            {
                var e = md.V[idx[i, k + 1]] - md.V[idx[i, k]];
                var nrm = Vector3.Cross(tans[i], e);
                if (Mathf.Abs(nrm.y) > 0.05f * nrm.magnitude) { if (nrm.y < 0) nrm = -nrm; }
                else
                {
                    float off = (profile[k][0] + profile[k + 1][0]) / 2;
                    var outward = sides[i] * Mathf.Sign(off);
                    bool outerFace = k == 0 || k == m - 2;
                    nrm = outerFace ? outward : -outward;
                }
                md.Quad(idx[i, k], idx[i + 1, k], idx[i + 1, k + 1], idx[i, k + 1], nrm);
            }
        return md;
    }

    public static MeshData Channel(float[][] pts, float width)
    {
        var md = new MeshData();
        int n = pts.Length;
        var fl = new int[n]; var fr = new int[n]; var wl = new int[n]; var wr = new int[n];
        var side = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var p = new Vector3(pts[i][0], pts[i][1], pts[i][2]);
            var a = i > 0 ? new Vector3(pts[i - 1][0], pts[i - 1][1], pts[i - 1][2]) : p;
            var b = i + 1 < n ? new Vector3(pts[i + 1][0], pts[i + 1][1], pts[i + 1][2]) : p;
            var tan = b - a; tan.y = 0;
            side[i] = Vector3.Cross(Vector3.up, tan.normalized).normalized; // to the right of the direction of travel
            fl[i] = md.Add(p - side[i] * width / 2); fr[i] = md.Add(p + side[i] * width / 2);
            wl[i] = md.Add(p - side[i] * width / 2 + Vector3.up * pts[i][3]);
            wr[i] = md.Add(p + side[i] * width / 2 + Vector3.up * pts[i][4]);
        }
        for (int i = 0; i + 1 < n; i++)
        {
            if (pts[i].Length > 5 && pts[i][5] > 0.5f) md.Quad(fl[i], fl[i + 1], fr[i + 1], fr[i], Vector3.up);
            if (pts[i][3] > 0.001f || pts[i + 1][3] > 0.001f) md.Quad(fl[i], fl[i + 1], wl[i + 1], wl[i], side[i]);
            if (pts[i][4] > 0.001f || pts[i + 1][4] > 0.001f) md.Quad(fr[i], fr[i + 1], wr[i + 1], wr[i], -side[i]);
        }
        return md;
    }

    /// Body of revolution: `profile` [(radius, height)] around the y axis ("y") or the z axis ("z") through the origin.
    /// Angles in degrees: around y 0 = +x, 90 = +z; around z 0 = +x, 90 = +y. Faces point away from the axis
    /// where the radius grows with the profile direction (use a closed profile for solid rings).
    public static MeshData Lathe(IList<Vector2> profile, string axis, float gapFrom, float gapTo, bool inwardFaces)
    {
        var md = new MeshData();
        int n = 48;
        bool hasGap = gapTo > gapFrom;
        float a0 = hasGap ? gapTo : 0f, a1 = hasGap ? gapFrom + 360f : 360f;
        int steps = Mathf.Max(3, Mathf.CeilToInt(n * (a1 - a0) / 360f));
        var idx = new int[steps + 1, profile.Count];
        Vector3 P(float r, float h, float ang)
        {
            float c = Mathf.Cos(ang * Mathf.Deg2Rad), s = Mathf.Sin(ang * Mathf.Deg2Rad);
            return axis == "z" ? new Vector3(r * c, r * s, h) : new Vector3(r * c, h, r * s);
        }
        for (int i = 0; i <= steps; i++)
        {
            float ang = Mathf.Lerp(a0, a1, (float)i / steps);
            for (int k = 0; k < profile.Count; k++) idx[i, k] = md.Add(P(profile[k].x, profile[k].y, ang));
        }
        for (int i = 0; i < steps; i++)
            for (int k = 0; k + 1 < profile.Count; k++)
            {
                var a = md.V[idx[i, k]]; var b = md.V[idx[i, k + 1]];
                var mid = (a + b) / 2;
                var radial = axis == "z" ? new Vector3(mid.x, mid.y, 0) : new Vector3(mid.x, 0, mid.z);
                md.Quad(idx[i, k], idx[i + 1, k], idx[i + 1, k + 1], idx[i, k + 1], inwardFaces ? -radial : radial);
            }
        return md;
    }

    /// Open pipe between two points (inner + outer wall, end rings). Inner faces point inwards.
    public static MeshData Tube(Vector3 a, Vector3 b, float innerR, float wall)
    {
        var md = new MeshData();
        var axis = (b - a).normalized;
        var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        var v = Vector3.Cross(axis, u);
        int n = 24;
        float outerR = innerR + wall;
        var ia = new int[n]; var ib = new int[n]; var oa = new int[n]; var ob = new int[n];
        for (int k = 0; k < n; k++)
        {
            float ang = 2 * Mathf.PI * k / n;
            var dir = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
            ia[k] = md.Add(a + dir * innerR); ib[k] = md.Add(b + dir * innerR);
            oa[k] = md.Add(a + dir * outerR); ob[k] = md.Add(b + dir * outerR);
        }
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            var mid = (md.V[ia[k]] + md.V[ia[k1]]) / 2;
            var radial = mid - (a + axis * Vector3.Dot(mid - a, axis));
            md.Quad(ia[k], ia[k1], ib[k1], ib[k], -radial);  // inner wall faces the axis
            md.Quad(oa[k], oa[k1], ob[k1], ob[k], radial);   // outer wall faces out
            md.Quad(ia[k], ia[k1], oa[k1], oa[k], -axis);    // ring at a
            md.Quad(ib[k], ib[k1], ob[k1], ob[k], axis);     // ring at b
        }
        return md;
    }

    /// Vertical prism over a convex outline, sitting on the surface (flat top at surface height under the centroid + height).
    /// Triangulate a simple polygon (convex or not) by ear clipping; returns index triples.
    public static List<int> Triangulate(IList<Vector2> p)
    {
        var res = new List<int>();
        var idx = new List<int>(); for (int i = 0; i < p.Count; i++) idx.Add(i);
        float area = 0; for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; area += a.x * b.y - b.x * a.y; }
        float sgn = area >= 0 ? 1f : -1f;
        float Cr(Vector2 a, Vector2 b, Vector2 c) => ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * sgn;
        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool cut = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                if (Cr(p[ia], p[ib], p[ic]) <= 1e-12f) continue;        // reflex or degenerate corner
                bool empty = true;
                foreach (int k in idx)
                {
                    if (k == ia || k == ib || k == ic) continue;
                    if (Cr(p[ia], p[ib], p[k]) >= 0 && Cr(p[ib], p[ic], p[k]) >= 0 && Cr(p[ic], p[ia], p[k]) >= 0) { empty = false; break; }
                }
                if (!empty) continue;
                res.Add(ia); res.Add(ib); res.Add(ic);
                idx.RemoveAt(i); cut = true; break;
            }
            if (!cut) idx.RemoveAt(0);   // numerically stuck: drop a vertex
        }
        if (idx.Count == 3) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
        return res;
    }

    /// Prism over an outline. `baseY` >= 0: a slab from baseY to `height` above the surface (e.g. a roof), with an underside.
    public static MeshData Prism(LaneDef d, IList<Vector2> pts, float height, float baseY = -1f)
    {
        var md = new MeshData();
        var c = Vector2.zero; foreach (var p in pts) c += p; c /= pts.Count;
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (var p in pts) { float h = Height(d, p.x, p.y); minH = Mathf.Min(minH, h); maxH = Mathf.Max(maxH, h); }
        float yb = baseY >= 0f ? minH + baseY : minH - 0.03f, yt = maxH + height;
        int n = pts.Count;
        var b = new int[n]; var t = new int[n]; var top = new int[n];
        for (int k = 0; k < n; k++)
        {
            b[k] = md.Add(new Vector3(pts[k].x, yb, pts[k].y));
            t[k] = md.Add(new Vector3(pts[k].x, yt, pts[k].y));
        }
        // the flat top has its own vertices (crisp edge), and is triangulated properly so concave outlines work
        for (int k = 0; k < n; k++) top[k] = md.Add(new Vector3(pts[k].x, yt, pts[k].y));
        var tri = Triangulate(pts);
        for (int k = 0; k + 2 < tri.Count; k += 3) md.Tri(top[tri[k]], top[tri[k + 1]], top[tri[k + 2]], Vector3.up);
        if (baseY >= 0f)
        {
            var bot = new int[n];
            for (int k = 0; k < n; k++) bot[k] = md.Add(new Vector3(pts[k].x, yb, pts[k].y));
            for (int k = 0; k + 2 < tri.Count; k += 3) md.Tri(bot[tri[k]], bot[tri[k + 1]], bot[tri[k + 2]], Vector3.down);
        }
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            var mid = (pts[k] + pts[k1]) / 2;
            var outward = new Vector3(mid.x - c.x, 0, mid.y - c.y);
            md.Quad(b[k], b[k1], t[k1], t[k], outward);
        }
        return md;
    }
}
