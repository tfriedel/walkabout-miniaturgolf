using System;
using System.Collections.Generic;
using UnityEngine;

namespace WalkaboutDesktop;

/// The surroundings of the park: an overgrown city-garden minigolf course in the style of the 1950s/60s (inspired by
/// photos of Hertzberg Golf, Berlin): slab paving, planted beds behind the lanes, mushroom lamps, a retro kiosk with a
/// small beer garden, red benches, big trees and apartment houses behind them.
/// Everything is visual only (no colliders) and is merged into one mesh per colour, so detail costs few draw calls.
static class ParkDeco
{
    // ------------------------------------------------------------------ batching
    class Batch
    {
        public readonly List<Vector3> V = new();
        public readonly List<int> T = new();
        public List<Vector2> UV;        // only foliage batches (group 2)
        public void Tri(int a, int b, int c, Vector3 n)
        {
            if (Vector3.Dot(Vector3.Cross(V[b] - V[a], V[c] - V[a]), n) >= 0) { T.Add(a); T.Add(b); T.Add(c); }
            else { T.Add(a); T.Add(c); T.Add(b); }
        }
        public void Quad(int a, int b, int c, int d, Vector3 n) { Tri(a, b, c, n); Tri(a, c, d, n); }
    }

    static readonly Dictionary<long, Batch> batches = new();
    static readonly Dictionary<long, Color> batchColor = new();
    static Transform parent;
    static System.Random rnd;

    static Color C(float r, float g, float b) { Color c = default; c.r = r; c.g = g; c.b = b; c.a = 1f; return c; }
    // Vector maths and Mathf are calls into the game runtime and far too slow for a million vertices:
    // the inner loops below use plain floats and System.MathF, and build structs through their fields.
    static Vector3 V3(float x, float y, float z) { Vector3 v = default; v.x = x; v.y = y; v.z = z; return v; }
    static Vector2 V2(float x, float y) { Vector2 v = default; v.x = x; v.y = y; return v; }
    static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
    static T Pick<T>(params T[] xs) => xs[rnd.Next(xs.Length)];

    /// grp 0 = plain, 1 = plain (small or distant things), 2 = foliage (leaf texture, tinted)
    static Batch Get(Color c, int grp)
    {
        long key = ((long)grp << 24) | ((long)(int)(c.r * 255) << 16) | ((long)(int)(c.g * 255) << 8) | (long)(int)(c.b * 255);
        if (!batches.TryGetValue(key, out var b)) { batches[key] = b = new Batch(); batchColor[key] = c; if (grp == 2) b.UV = new List<Vector2>(); }
        return b;
    }

    static void Flush()
    {
        int verts = 0;
        foreach (var kv in batches)
        {
            var b = kv.Value; if (b.V.Count == 0) continue;
            var c = batchColor[kv.Key];
            var m = new Mesh { name = "Deco" };
            if (b.V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = b.V.ToArray();
            if (b.UV != null) m.uv = b.UV.ToArray();
            m.triangles = b.T.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            var go = new GameObject($"Deco_{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = b.UV != null ? FoliageMat(c) : LaneBuilder.MakeMaterial(new[] { c.r, c.g, c.b });
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            verts += b.V.Count;
        }
        Plugin.L.LogInfo($"Park scenery: {batches.Count} meshes, {verts} vertices");
        batches.Clear(); batchColor.Clear();
    }

    // ------------------------------------------------------------------ primitives
    static readonly Vector3[] BoxN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

    static void Box(Vector3 c, Vector3 size, Color col, Quaternion? rot = null, int grp = 0)
    {
        var b = Get(col, grp); var q = rot ?? Quaternion.identity; var h = size / 2;
        foreach (var n in BoxN)
        {
            // two axes spanning the face
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(n, u);
            Vector3 P(float su, float sw) => c + q * Vector3.Scale(n + u * su + w * sw, h);
            int i = b.V.Count;
            b.V.Add(P(-1, -1)); b.V.Add(P(1, -1)); b.V.Add(P(1, 1)); b.V.Add(P(-1, 1));
            b.Quad(i, i + 1, i + 2, i + 3, q * n);
        }
    }
    static void Box(float x, float y, float z, float sx, float sy, float sz, Color col, float yaw = 0f, int grp = 0)
        => Box(new Vector3(x, y, z), new Vector3(sx, sy, sz), col, yaw == 0f ? null : Quaternion.Euler(0, yaw, 0), grp);

    /// Bar from a to b with a w x h cross-section.
    static void Beam(Vector3 a, Vector3 b, float w, float h, Color col, int grp = 0)
    {
        var d = b - a; float len = d.magnitude; if (len < 1e-5f) return;
        Box((a + b) / 2, new Vector3(w, len, h), col, Quaternion.FromToRotation(Vector3.up, d / len), grp);
    }

    /// Solid of revolution. prof = r0,y0, r1,y1, ... listed from the bottom centre outwards, up and back to the top centre.
    static void Lathe(Vector3 pos, float[] prof, int seg, Color col, Quaternion? rot = null, bool hard = false, int grp = 0, float a0 = 0f)
    {
        var b = Get(col, grp); var q = rot ?? Quaternion.identity; int np = prof.Length / 2;
        Vector3 P(int i, int k)
        {
            float a = a0 + k * 2 * Mathf.PI / seg;
            return pos + q * new Vector3(prof[2 * i] * Mathf.Cos(a), prof[2 * i + 1], prof[2 * i] * Mathf.Sin(a));
        }
        Vector3 N(int i, int k)
        {
            float a = a0 + (k + 0.5f) * 2 * Mathf.PI / seg;
            float dr = prof[2 * i + 2] - prof[2 * i], dy = prof[2 * i + 3] - prof[2 * i + 1];
            return q * new Vector3(dy * Mathf.Cos(a), -dr, dy * Mathf.Sin(a));
        }
        if (!hard)
        {
            int i0 = b.V.Count;
            for (int i = 0; i < np; i++) for (int k = 0; k < seg; k++) b.V.Add(P(i, k));
            for (int i = 0; i < np - 1; i++)
                for (int k = 0; k < seg; k++)
                {
                    int k2 = (k + 1) % seg;
                    b.Quad(i0 + i * seg + k, i0 + i * seg + k2, i0 + (i + 1) * seg + k2, i0 + (i + 1) * seg + k, N(i, k));
                }
        }
        else
        {
            for (int i = 0; i < np - 1; i++)
                for (int k = 0; k < seg; k++)
                {
                    int j = b.V.Count;
                    b.V.Add(P(i, k)); b.V.Add(P(i, k + 1)); b.V.Add(P(i + 1, k + 1)); b.V.Add(P(i + 1, k));
                    b.Quad(j, j + 1, j + 2, j + 3, N(i, k));
                }
        }
    }

    /// Round bar from a to b (radius r0 at a, r1 at b).
    static void Tube(Vector3 a, Vector3 b, float r0, float r1, Color col, int seg = 8, int grp = 0)
    {
        var d = b - a; float len = d.magnitude; if (len < 1e-5f) return;
        Lathe(a, new[] { 0f, 0f, r0, 0f, r1, len, 0f, len }, seg, col, Quaternion.FromToRotation(Vector3.up, d / len), false, grp);
    }
    static void Pole(float x, float z, float y0, float y1, float r, Color col, int seg = 8) => Tube(new Vector3(x, y0, z), new Vector3(x, y1, z), r, r, col, seg);

    // icosphere, cached per level of detail
    static readonly Dictionary<int, (Vector3[] v, int[] t)> ico = new();
    static readonly Dictionary<int, float[]> icoF = new();
    static (Vector3[] v, int[] t) Ico(int detail)
    {
        if (ico.TryGetValue(detail, out var m)) return m;
        float t = (1 + Mathf.Sqrt(5)) / 2;
        var v = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
        var f = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        for (int d = 0; d < detail; d++)
        {
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int i)) return i;
                v.Add(((v[a] + v[b]) / 2).normalized); cache[key] = v.Count - 1; return v.Count - 1;
            }
            var nf = new List<int>();
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            f = nf;
        }
        // front faces outwards
        for (int i = 0; i < f.Count; i += 3)
            if (Vector3.Dot(Vector3.Cross(v[f[i + 1]] - v[f[i]], v[f[i + 2]] - v[f[i]]), v[f[i]] + v[f[i + 1]] + v[f[i + 2]]) < 0) (f[i + 1], f[i + 2]) = (f[i + 2], f[i + 1]);
        return ico[detail] = (v.ToArray(), f.ToArray());
    }

    /// Lumpy ball (foliage, bushes, clouds, flower heads). amp = strength of the lumps, fine = strength of small bumps.
    static void Blob(Vector3 pos, Vector3 scale, Color col, int detail = 2, float amp = 0.16f, int grp = 0, float fine = 0f)
    {
        var (v, t) = Ico(detail); var b = Get(col, grp); int i0 = b.V.Count;
        if (!icoF.TryGetValue(detail, out var f))
        {
            f = new float[v.Length * 3];
            for (int i = 0; i < v.Length; i++) { f[3 * i] = v[i].x; f[3 * i + 1] = v[i].y; f[3 * i + 2] = v[i].z; }
            icoF[detail] = f;
        }
        float s = (float)rnd.NextDouble() * 50f;
        float px = pos.x, py = pos.y, pz = pos.z, sx = scale.x, sy = scale.y, sz = scale.z;
        bool uv = b.UV != null;
        for (int i = 0; i < f.Length; i += 3)
        {
            float x = f[i], y = f[i + 1], z = f[i + 2], k = 1f;
            if (amp != 0) k += amp * (MathF.Sin((x + 0.3f * y + 0.5f * z) * 2.3f + s) + MathF.Sin((-0.4f * x + y + 0.7f * z) * 3.1f + 1.7f * s) + MathF.Sin((0.6f * x - 0.8f * y + z) * 4.7f + 2.9f * s)) / 3f;
            if (fine != 0) k += fine * (MathF.Sin((0.7f * x + 0.2f * y - 0.6f * z) * 11f + 3.1f * s) + MathF.Sin((-0.3f * x + 0.9f * y + 0.4f * z) * 13f + 0.7f * s) + MathF.Sin((0.5f * x - 0.5f * y + 0.8f * z) * 17f + 5.3f * s)) / 3f;
            float wx = px + x * k * sx, wy = py + y * k * sy, wz = pz + z * k * sz;
            b.V.Add(V3(wx, wy, wz));
            if (uv) b.UV.Add(V2((wx * 0.83f + wz * 0.41f) * 0.42f, (wy * 0.90f + wz * 0.37f - wx * 0.2f) * 0.42f));
        }
        var tl = b.T;
        for (int i = 0; i < t.Length; i++) tl.Add(i0 + t[i]);
    }
    static void Blob(float x, float y, float z, float sx, float sy, float sz, Color col, int detail = 2, float amp = 0.16f, int grp = 0)
        => Blob(V3(x, y, z), V3(sx, sy, sz), col, detail, amp, grp);

    /// Letters made of bars on a wall. origin = left end of the baseline; right / up = directions on the wall.
    static void Text(string s, Vector3 origin, Vector3 right, Vector3 up, float h, float w, float gap, float thick, Color col)
    {
        float x = 0;
        var outw = Vector3.Cross(up, right).normalized;
        foreach (char ch in s)
        {
            if (LaneBuilder.Strokes.TryGetValue(ch, out var ss))
                foreach (var st in ss)
                    for (int k = 0; k + 3 < st.Length; k += 2)
                    {
                        var a = origin + right * (x + st[k] * w) + up * (st[k + 1] * h);
                        var b = origin + right * (x + st[k + 2] * w) + up * (st[k + 3] * h);
                        var d = (b - a).normalized;
                        var c = (a + b) / 2;
                        Box(c, new Vector3(thick, (b - a).magnitude + thick, thick * 0.5f), col, Quaternion.LookRotation(outw, d) * Quaternion.identity, 1);
                    }
            x += ch == ' ' ? w * 0.7f : (ch == 'I' ? thick : w) + gap;
        }
    }
    static float TextWidth(string s, float w, float gap, float thick)
    {
        float x = -gap; foreach (char ch in s) x += ch == ' ' ? w * 0.7f : (ch == 'I' ? thick : w) + gap; return x;
    }

    // ------------------------------------------------------------------ textured ground
    static uint Hash(int x, int y, int s) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 1274126177); h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16); } }
    static float H01(int x, int y, int s) => (Hash(x, y, s) & 0xFFFF) / 65535f;
    /// tileable value noise: `cells` lattice cells across the texture
    static float VNoise(float u, float v, int cells, int seed)
    {
        float x = u * cells, y = v * cells; int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int x0 = ((xi % cells) + cells) % cells, y0 = ((yi % cells) + cells) % cells, x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
        float a = H01(x0, y0, seed), b = H01(x1, y0, seed), c = H01(x0, y1, seed), d = H01(x1, y1, seed);
        float ab = a + (b - a) * fx, cd = c + (d - c) * fx;
        return ab + (cd - ab) * fy;
    }
    static byte B(float v) => (byte)(v <= 0 ? 0 : v >= 1 ? 255 : v * 255f);

    static Material TexMat(string name, int size, Func<int, int, Color> f, bool repeat = true)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { var c = f(x, y); Color32 q = default; q.r = B(c.r); q.g = B(c.g); q.b = B(c.b); q.a = B(c.a); px[y * size + x] = q; }
        tex.SetPixels32(px);
        tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; tex.filterMode = FilterMode.Trilinear; tex.anisoLevel = 8;
        tex.Apply(true);
        var m = new Material(LaneBuilder.MakeMaterial(new[] { 1f, 1f, 1f })) { name = name };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        return m;
    }

    static Material pavingMat, lawnMat;
    static Material PavingMat() => pavingMat != null ? pavingMat : pavingMat = TexMat("ParkPaving", 512, (x, y) =>
    {
        // 2 x 2 m of weathered 50 cm concrete slabs with mossy joints
        int sx = x / 128, sy = y / 128, lx = x % 128, ly = y % 128;
        float u = x / 512f, v = y / 512f;
        int e = Math.Min(Math.Min(lx, 127 - lx), Math.Min(ly, 127 - ly));
        float stain = VNoise(u, v, 6, 3) * 0.6f + VNoise(u, v, 14, 4) * 0.4f;
        if (e < 2)
        {
            float mz = 0.55f + 0.45f * VNoise(u, v, 40, 9);
            return C(0.30f * mz + 0.12f, 0.34f * mz + 0.12f, 0.24f * mz + 0.10f);
        }
        float g = 0.585f + (H01(sx, sy, 1) - 0.5f) * 0.09f + (stain - 0.5f) * 0.12f + (H01(x, y, 2) - 0.5f) * 0.05f;
        if (e < 7) g -= 0.035f * (7 - e) / 5f;
        return C(g, g * 0.985f, g * 0.94f);
    });
    static Material LawnMat() => lawnMat != null ? lawnMat : lawnMat = TexMat("ParkLawn", 256, (x, y) =>
    {
        float u = x / 256f, v = y / 256f;
        float n = VNoise(u, v, 5, 11) * 0.5f + VNoise(u, v, 13, 12) * 0.3f + VNoise(u, v, 37, 13) * 0.2f;
        float k = 0.78f + 0.44f * n + (H01(x, y, 5) - 0.5f) * 0.10f;
        return C(0.29f * k, 0.46f * k, 0.20f * k);
    });

    static Texture2D foliageTex;
    static readonly Dictionary<int, Material> foliageMats = new();
    /// leaf-mottled material in the given tint: light leaf tips over darker gaps (cellular noise)
    static Material FoliageMat(Color c)
    {
        int key = ((int)(c.r * 255) << 16) | ((int)(c.g * 255) << 8) | (int)(c.b * 255);
        if (foliageMats.TryGetValue(key, out var m) && m != null) return m;
        if (foliageTex == null)
        {
            const int N = 256, cells = 34;
            var basis = TexMat("ParkFoliage", N, (x, y) =>
            {
                float u = (float)x / N * cells, v = (float)y / N * cells; int xi = (int)MathF.Floor(u), yi = (int)MathF.Floor(v);
                float best = 9f, second = 9f; int bx = 0, by = 0;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ((xi + dx) % cells + cells) % cells, cy = ((yi + dy) % cells + cells) % cells;
                    float px = xi + dx + H01(cx, cy, 31), py = yi + dy + H01(cx, cy, 32);
                    float d = (px - u) * (px - u) + (py - v) * (py - v);
                    if (d < best) { second = best; best = d; bx = cx; by = cy; } else if (d < second) second = d;
                }
                float edge = MathF.Sqrt(second) - MathF.Sqrt(best);            // 0 at the gaps between leaves
                float leaf = 0.86f + 0.30f * H01(bx, by, 33);                    // every leaf its own brightness
                float gap = edge * 3.2f; if (gap > 1) gap = 1;
                float big = VNoise((float)x / N, (float)y / N, 5, 34);           // lighter and darker patches
                float k = (0.83f + (leaf - 0.83f) * gap) * (0.86f + 0.28f * big);
                return C(k * 1.02f, k * 1.04f, k * 0.92f);
            });
            foliageTex = basis.GetTexture("_BaseMap").TryCast<Texture2D>();
        }
        m = new Material(LaneBuilder.MakeMaterial(new[] { c.r, c.g, c.b })) { name = "ParkFoliage_" + key };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", foliageTex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", foliageTex);
        return foliageMats[key] = m;
    }

    /// paved rectangle given in the coordinates of a lane (or with yaw 0 in park coordinates)
    static void Pad(string name, Vector3 pos, float yaw, float x0, float z0, float x1, float z1, float y)
    {
        var m = new Mesh { name = name };
        float ou = (float)rnd.NextDouble(), ov = (float)rnd.NextDouble();
        m.vertices = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
        m.uv = new[] { new Vector2(x0 / 2 + ou, z0 / 2 + ov), new Vector2(x0 / 2 + ou, z1 / 2 + ov), new Vector2(x1 / 2 + ou, z1 / 2 + ov), new Vector2(x1 / 2 + ou, z0 / 2 + ov) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateNormals(); m.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pos.x, 0, pos.z);
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = PavingMat();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void GroundQuad(string name, float x0, float z0, float x1, float z1, float y, Material mat, float tile)
    {
        var m = new Mesh { name = name };
        m.vertices = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
        m.uv = new[] { new Vector2(x0 / tile, z0 / tile), new Vector2(x0 / tile, z1 / tile), new Vector2(x1 / tile, z1 / tile), new Vector2(x1 / tile, z0 / tile) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateNormals(); m.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    static void Paving(string name, float x0, float z0, float x1, float z1) => GroundQuad(name, x0, z0, x1, z1, 0.012f, PavingMat(), 2f);

    // ------------------------------------------------------------------ painted shadows
    // The game build renders no real-time shadows. Instead every tree crown, plant and piece of furniture is splatted
    // into one shadow map of the garden (shifted away from the sun by its height); the map is laid over the ground
    // and over the playing surface of every lane, so trees shade the lanes they stand over.
    const int SM = 2048;
    static float[] sm, smTree; static float smX0, smZ0, smSize;
    static readonly Vector2 SunShift = new(-0.448f, 0.640f);       // ground offset of a shadow per metre of height (sun at 52 deg, from the front right)
    const float SunX = -0.448f, SunZ = 0.640f;

    /// soft elliptical shadow of something `height` above the ground at (x, z); dapple = sun flecks as under leaves
    static void Shade(float x, float z, float rx, float rz, float height = 0f, bool strong = false, bool dapple = false)
    {
        if (sm == null) return;
        var map = dapple ? smTree : sm;
        float cx = x + SunX * height, cz = z + SunZ * height, a = strong ? 0.46f : 0.34f, k = SM / smSize;
        int px0 = Math.Max(0, (int)MathF.Floor((cx - rx - smX0) * k)), px1 = Math.Min(SM - 1, (int)MathF.Ceiling((cx + rx - smX0) * k));
        int pz0 = Math.Max(0, (int)MathF.Floor((cz - rz - smZ0) * k)), pz1 = Math.Min(SM - 1, (int)MathF.Ceiling((cz + rz - smZ0) * k));
        for (int pz = pz0; pz <= pz1; pz++)
        {
            float wz = smZ0 + (pz + 0.5f) / k, v = (wz - cz) / rz, vv = v * v;
            if (vv >= 1f) continue;
            int row = pz * SM;
            for (int px = px0; px <= px1; px++)
            {
                float u = (smX0 + (px + 0.5f) / k - cx) / rx, d = u * u + vv;
                if (d >= 1f) continue;
                float f = (1 - MathF.Sqrt(d)) * 2.6f; if (f > 1f) f = 1f; f = f * f * (3 - 2 * f);
                map[row + px] = 1 - (1 - map[row + px]) * (1 - a * f);
            }
        }
    }

    static void FlushShadows(IList<LaneDef> defs, Vector3[] origins, float[] yaws, int n)
    {
        var tex = new Texture2D(SM, SM, TextureFormat.RGBA32, true);
        var px = new Color32[SM * SM];
        // leaves let sun flecks through: the tree shade is broken up by a noise pattern before it joins the rest
        float kk = smSize / SM;
        for (int z = 0; z < SM; z++)
            for (int x = 0; x < SM; x++)
            {
                int i = z * SM + x; float tr = smTree[i], al = sm[i];
                if (tr > 0.004f)
                {
                    float wx = smX0 + x * kk, wz = smZ0 + z * kk;
                    float nz = VNoise(wx / 7f, wz / 7f, 9, 21) * 0.6f + VNoise(wx / 7f, wz / 7f, 23, 22) * 0.4f;
                    float h = (nz - 0.36f) / 0.22f; h = h < 0 ? 0 : h > 1 ? 1 : h; h = h * h * (3 - 2 * h);
                    al = 1 - (1 - al) * (1 - tr * (0.30f + 0.70f * h));
                }
                Color32 c = default; c.a = (byte)((al > 0.60f ? 0.60f : al) * 255f); px[i] = c;
            }
        smTree = null;
        tex.SetPixels32(px); tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Trilinear; tex.anisoLevel = 4; tex.Apply(true);
        sm = null;
        var basis = new Material(LaneBuilder.MakeMaterial(new[] { 1f, 1f, 1f })) { name = "ParkShadowMap" };
        if (basis.HasProperty("_BaseMap")) basis.SetTexture("_BaseMap", tex);
        if (basis.HasProperty("_MainTex")) basis.SetTexture("_MainTex", tex);
        var mat = LaneBuilder.Transparent(basis, 1f);

        GameObject Emit(string name, List<Vector3> v, List<Vector2> uv, List<int> tr, Material m2)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = v.ToArray(); if (uv != null) m.uv = uv.ToArray(); m.triangles = tr.ToArray();
            m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m2; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
        Vector2 UV(float x, float z) => new Vector2((x - smX0) / smSize, (z - smZ0) / smSize);

        // on the ground
        float x1 = smX0 + smSize, z1 = smZ0 + smSize;
        Emit("Shadows_Ground", new List<Vector3> { new(smX0, 0.026f, smZ0), new(smX0, 0.026f, z1), new(x1, 0.026f, z1), new(x1, 0.026f, smZ0) },
             new List<Vector2> { UV(smX0, smZ0), UV(smX0, z1), UV(x1, z1), UV(x1, smZ0) }, new List<int> { 0, 1, 2, 0, 2, 3 }, mat);

        // on the playing surfaces, and the sharp shadow of every lane plate on its paving
        var lv = new List<Vector3>(); var lt = new List<int>();
        for (int i = 0; i < n; i++)
        {
            var q = Quaternion.Euler(0, yaws[i], 0); var d = defs[i];
            var owners = new List<int> { -1 };
            if (d.surface != null) for (int fi = 0; fi < d.surface.Count; fi++) if (d.surface[fi].color != null) owners.Add(fi);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            foreach (int owner in owners)
            {
                var md = LaneGeometry.Surface(d, 0.0546f, owner);
                int o0 = v.Count;
                foreach (var p in md.V)
                {
                    var w = origins[i] + q * p;
                    v.Add(w + Vector3.up * 0.0015f); uv.Add(UV(w.x - SunShift.x * w.y, w.z - SunShift.y * w.y));
                }
                foreach (int k in md.T) tr.Add(o0 + k);
            }
            if (v.Count > 0) Emit("Shadows_Lane" + (i + 1), v, uv, tr, mat);

            var poly = LaneGeometry.Polygon(d);
            var tris = LaneGeometry.Triangulate(poly);
            int i0 = lv.Count; float h = origins[i].y;
            foreach (var p in poly) { var w = origins[i] + q * new Vector3(p.x, 0, p.y); lv.Add(new Vector3(w.x + SunShift.x * h, 0.024f, w.z + SunShift.y * h)); }
            for (int k = 0; k + 2 < tris.Count; k += 3)
            {
                int a = i0 + tris[k], bb = i0 + tris[k + 1], c = i0 + tris[k + 2];
                if (Vector3.Cross(lv[bb] - lv[a], lv[c] - lv[a]).y >= 0) { lt.Add(a); lt.Add(bb); lt.Add(c); } else { lt.Add(a); lt.Add(c); lt.Add(bb); }
            }
        }
        Emit("Shadows_LanePlates", lv, null, lt, LaneBuilder.Transparent(LaneBuilder.MakeMaterial(new[] { 0.02f, 0.02f, 0.03f }), 0.36f));
    }

    // ------------------------------------------------------------------ colours
    static readonly Color Leaf1 = C(0.17f, 0.37f, 0.13f), Leaf2 = C(0.22f, 0.45f, 0.15f), Leaf3 = C(0.13f, 0.30f, 0.13f), Leaf4 = C(0.32f, 0.52f, 0.17f);
    static readonly Color Leaf5 = C(0.27f, 0.50f, 0.20f), Fir2 = C(0.13f, 0.30f, 0.17f), Stone = C(0.60f, 0.59f, 0.56f);
    static readonly Color Fir = C(0.10f, 0.25f, 0.15f), Bark = C(0.25f, 0.20f, 0.16f), BarkPale = C(0.47f, 0.44f, 0.38f), Soil = C(0.24f, 0.17f, 0.11f);
    static readonly Color Kerb = C(0.62f, 0.61f, 0.57f), Concrete = C(0.66f, 0.65f, 0.61f), Metal = C(0.82f, 0.83f, 0.82f), DarkMetal = C(0.22f, 0.24f, 0.24f);
    static readonly Color BenchRed = C(0.78f, 0.16f, 0.13f), WoodDark = C(0.31f, 0.21f, 0.14f), Wood = C(0.66f, 0.50f, 0.30f), White = C(0.95f, 0.95f, 0.92f);
    static readonly Color Sage = C(0.62f, 0.72f, 0.55f), SageDark = C(0.52f, 0.62f, 0.46f), Orange = C(0.87f, 0.42f, 0.13f), Chalk = C(0.15f, 0.24f, 0.19f);
    static readonly Color Yellow = C(0.96f, 0.80f, 0.12f), Blue = C(0.16f, 0.36f, 0.72f), Red = C(0.83f, 0.15f, 0.12f), Peach = C(0.95f, 0.68f, 0.56f), Purple = C(0.50f, 0.28f, 0.66f), Pink = C(0.90f, 0.47f, 0.62f);
    static readonly Color[] Bulbs = { C(0.95f, 0.25f, 0.20f), C(0.98f, 0.82f, 0.20f), C(0.25f, 0.60f, 0.90f), C(0.35f, 0.75f, 0.35f), C(0.98f, 0.55f, 0.15f) };

    // ------------------------------------------------------------------ plants
    /// bent, tapering limb from a to b
    static void Limb(Vector3 a, Vector3 b, float r0, float r1, Color col, int seg, int parts = 3)
    {
        var bend = new Vector3(R(-1, 1), R(-0.3f, 0.6f), R(-1, 1)) * (b - a).magnitude * 0.10f;
        Vector3 prev = a;
        for (int i = 1; i <= parts; i++)
        {
            float t = (float)i / parts;
            var p = Vector3.Lerp(a, b, t) + bend * Mathf.Sin(t * Mathf.PI);
            Tube(prev - (p - prev).normalized * r0 * 0.3f, p, Mathf.Lerp(r0, r1, (i - 1f) / parts), Mathf.Lerp(r0, r1, t), col, seg);
            prev = p;
        }
    }

    /// deciduous tree: slightly crooked trunk with a root flare, forking branches and a wide crown of leafy clumps
    /// that close into one canopy. detail 3 = seen from close by (inside the garden), 2 = further away
    static void Tree(float x, float z, float h, bool pale = false, int detail = 3)
    {
        var p = new Vector3(x, 0, z);
        var lean = new Vector3(R(-0.10f, 0.10f), 1, R(-0.10f, 0.10f)).normalized;
        var bark = pale ? BarkPale : Bark;
        float th = h * R(0.40f, 0.48f), tr = h * 0.017f + 0.05f;
        var top = p + lean * th + new Vector3(R(-0.2f, 0.2f), 0, R(-0.2f, 0.2f));
        int seg = detail >= 3 ? 14 : 8;
        Lathe(p - Vector3.up * 0.05f, new[] { 0f, 0f, tr * 1.7f, 0f, tr * 1.30f, 0.20f, tr * 1.08f, 0.60f, 0f, 0.60f }, seg, bark);        // root flare
        Limb(p + Vector3.up * 0.45f, top, tr * 1.08f, tr * 0.78f, bark, seg, 4);
        float cr = h * 0.34f;
        var centre = p + lean * (th + cr * 0.62f);
        var cols = pale ? new[] { Leaf4, Leaf2, Leaf4, Leaf5 } : new[] { Leaf1, Leaf2, Leaf3, Leaf2, Leaf5 };
        Shade(x, z, tr * 5f, tr * 5f, 0.2f, true); Shade(x, z, tr * 3.5f, tr * 3.5f, th * 0.35f); Shade(x, z, tr * 3.5f, tr * 3.5f, th * 0.7f);

        void Leaves(Vector3 c, float s, int d)
        {
            Blob(c, new Vector3(s, s * R(0.62f, 0.80f), s), cols[rnd.Next(cols.Length)], d, 0.24f, 2, d >= 3 ? 0.05f : 0.035f);
            Shade(c.x, c.z, s * 1.1f, s * 1.1f, c.y, false, true);
        }
        // the body of the crown: a few large masses
        Leaves(centre, cr * 0.80f, detail);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 1.571f + R(0, 1f);
            Leaves(centre + new Vector3(MathF.Cos(a) * cr * 0.45f, R(-0.1f, 0.35f) * cr, MathF.Sin(a) * cr * 0.45f), cr * R(0.52f, 0.66f), detail);
        }
        // branches carry the outer clumps, which overlap into a closed, lumpy outline
        int nb = detail >= 3 ? 9 + rnd.Next(3) : 7 + rnd.Next(2);
        for (int i = 0; i < nb; i++)
        {
            float a = i * 2 * MathF.PI / nb + R(0, 0.5f), d = cr * R(0.70f, 0.98f);
            var end = centre + new Vector3(MathF.Cos(a) * d, R(-0.32f, 0.30f) * cr, MathF.Sin(a) * d);
            var from = Vector3.Lerp(p + lean * th * 0.85f, top, R(0, 1));
            Limb(from, Vector3.Lerp(from, end, 0.8f), tr * 0.42f, tr * 0.12f, bark, detail >= 3 ? 8 : 5);
            float s = cr * R(0.36f, 0.50f);
            Leaves(end, s, 2);
            Leaves(end + new Vector3(R(-1, 1), R(-0.2f, 0.9f), R(-1, 1)) * s * 0.7f, s * R(0.6f, 0.8f), 2);
            if (detail >= 3) Leaves(end + new Vector3(R(-1, 1), R(-0.9f, 0.1f), R(-1, 1)) * s * 0.8f, s * R(0.5f, 0.7f), 2);
        }
    }

    static void Conifer(float x, float z, float h)
    {
        Tube(new Vector3(x, 0, z), new Vector3(x, 0.4f, z), 0.06f, 0.05f, Bark, 6);
        float r = h * R(0.15f, 0.19f);
        Shade(x, z, r * 1.5f, r * 1.5f, 0.1f, true); Shade(x, z, r * 1.3f, r * 1.3f, h * 0.35f); Shade(x, z, r * 1.0f, r * 1.0f, h * 0.7f);
        // layered, slightly ragged column of foliage
        int layers = 11;
        for (int i = 0; i < layers; i++)
        {
            float t = (i + 0.5f) / layers, lr = r * (1.05f - 0.80f * t * t), y = 0.3f + (h - 0.3f) * t;
            Blob(new Vector3(x + R(-0.03f, 0.03f), y, z + R(-0.03f, 0.03f)), new Vector3(lr, (h / layers) * 1.5f, lr), i % 3 == 0 ? Fir2 : Fir, 2, 0.08f, 2, 0.04f);
        }
    }

    static void Shrub(float x, float z, float size, Color? col = null)
    {
        int n = 3 + rnd.Next(3);
        Shade(x, z, size * 0.85f, size * 0.85f, size * 0.25f, true);
        for (int i = 0; i < n; i++)
        {
            float s = size * R(0.40f, 0.62f);
            var c = col ?? Pick(Leaf1, Leaf2, Leaf3);
            Blob(new Vector3(x + R(-0.35f, 0.35f) * size, s * 0.72f, z + R(-0.35f, 0.35f) * size), new Vector3(s, s * 0.85f, s), c, 2, 0.22f, 2, 0.06f);
        }
    }

    /// daisy-like flower on a stem (the yellow perennials of the reference photos)
    static void Flower(float x, float z, float h, Color col)
    {
        var top = new Vector3(x + R(-0.04f, 0.04f), h, z + R(-0.04f, 0.04f));
        Beam(new Vector3(x, 0.05f, z), top, 0.008f, 0.008f, Leaf2, 1);
        float r = R(0.035f, 0.055f);
        Lathe(top, new[] { 0f, -0.004f, r, 0f, r * 0.9f, 0.006f, 0f, 0.012f }, 7, col, Quaternion.Euler(R(-25, 25), 0, R(-25, 25)), false, 1);
        Blob(top + Vector3.up * 0.012f, Vector3.one * r * 0.38f, C(0.45f, 0.30f, 0.10f), 0, 0, 1);
    }

    static void Crocus(float x, float z, Color col)
    {
        float h = R(0.07f, 0.11f);
        Blob(x, h * 0.8f, z, 0.022f, h * 0.55f, 0.022f, col, 0, 0, 1);
        Beam(new Vector3(x, 0, z), new Vector3(x + R(-0.03f, 0.03f), h * 0.7f, z + R(-0.03f, 0.03f)), 0.006f, 0.006f, Leaf2, 1);
    }

    static void Meadow(float cx, float cz, float rx, float rz, int count, params Color[] cols)
    {
        for (int i = 0; i < count; i++)
        {
            float a = R(0, 6.283f), d = Mathf.Sqrt(R(0, 1));
            Crocus(cx + Mathf.Cos(a) * d * rx, cz + Mathf.Sin(a) * d * rz, cols[rnd.Next(cols.Length)]);
        }
    }

    /// planted bed: a low mound of soil edged with field stones, with shrubs, perennials and sometimes a conifer or lamp
    static void Bed(float cx, float cz, float rx, float rz, Color flower, bool lamp)
    {
        Blob(new Vector3(cx, 0.0f, cz), new Vector3(rx, 0.13f, rz), Soil, 2, 0.05f);
        int stones = (int)((rx + rz) * Mathf.PI / 0.21f);
        for (int i = 0; i < stones; i++)
        {
            float a = i * 2 * Mathf.PI / stones;
            Blob(new Vector3(cx + Mathf.Cos(a) * rx * 0.99f, 0.045f, cz + Mathf.Sin(a) * rz * 0.99f), new Vector3(R(0.085f, 0.12f), R(0.06f, 0.09f), R(0.085f, 0.12f)), Pick(Stone, Kerb, Concrete), 1, 0.25f, 1);
        }
        Vector2 In(float margin) { float a = R(0, 6.283f), d = Mathf.Sqrt(R(0, 1)); return new Vector2(cx + Mathf.Cos(a) * d * (rx - margin), cz + Mathf.Sin(a) * d * (rz - margin)); }
        int shrubs = Mathf.Max(1, (int)(rx * rz * 1.1f));
        for (int i = 0; i < shrubs; i++) { var q = In(0.5f); Shrub(q.x, q.y, R(0.55f, 1.0f)); }
        if (rnd.Next(3) == 0) { var q = In(0.6f); Conifer(q.x, q.y, R(1.6f, 2.6f)); }
        for (int i = 0; i < (int)(rx * rz * 9); i++) { var q = In(0.15f); Blob(new Vector3(q.x, 0.14f, q.y), new Vector3(R(0.12f, 0.24f), R(0.06f, 0.11f), R(0.12f, 0.24f)), Pick(Leaf2, Leaf4, Leaf1), 1, 0.2f, 2); }
        for (int i = 0; i < (int)(rx * rz * 22); i++) { var q = In(0.12f); Flower(q.x, q.y, R(0.30f, 0.85f), rnd.Next(6) == 0 ? White : flower); }
        if (lamp) { var q = In(0.5f); MushroomLamp(q.x, q.y, R(1.5f, 2.2f), Pick(Red, Blue, Yellow, Orange, White)); }
        Shade(cx, cz, rx * 1.05f, rz * 1.05f, 0.1f);
    }

    static void HedgeRow(Vector3 a, Vector3 b)
    {
        float len = (b - a).magnitude; int n = Mathf.Max(2, (int)(len / 1.05f));
        var across = Vector3.Cross(Vector3.up, (b - a).normalized);
        for (int i = 0; i <= n; i++)
        {
            var p = Vector3.Lerp(a, b, (float)i / n) + across * R(-0.2f, 0.2f);
            float h = R(1.9f, 2.7f);
            Shade(p.x, p.z, 1.25f, 1.25f, h * 0.45f, true);
            Blob(new Vector3(p.x, h * 0.48f, p.z), new Vector3(R(0.85f, 1.1f), h * 0.55f, R(0.85f, 1.1f)), Pick(Leaf1, Leaf3, Leaf1, Leaf2), 2, 0.2f, 2, 0.05f);
        }
    }

    // ------------------------------------------------------------------ furniture
    static void MushroomLamp(float x, float z, float h, Color col)
    {
        Pole(x, z, 0, h, 0.022f, C(0.86f, 0.86f, 0.84f), 10);
        if (h < 3f) { Shade(x, z, 0.36f, 0.36f, h, true); Shade(x, z, 0.10f, 0.10f, 0.05f, true); }
        float r = R(0.24f, 0.34f);
        Lathe(new Vector3(x, h, z), new[] { 0f, -0.01f, r * 0.96f, -0.015f, r, 0.01f, r * 0.9f, 0.06f, r * 0.66f, 0.115f, r * 0.33f, 0.150f, 0f, 0.16f }, 28, col);
        Lathe(new Vector3(x, h - 0.07f, z), new[] { 0f, 0f, 0.05f, 0f, 0.05f, 0.06f, 0f, 0.06f }, 8, White, null, false, 1);
    }

    static void Bench(float x, float z, float yaw)
    {
        var q = Quaternion.Euler(0, yaw, 0); var p = new Vector3(x, 0, z);
        { var ext = q * new Vector3(1.0f, 0, 0.42f); Shade(x, z, Mathf.Abs(ext.x) + 0.25f, Mathf.Abs(ext.z) + 0.25f, 0.35f, true); }
        foreach (float s in new[] { -0.62f, 0.62f })
        {
            Box(p + q * new Vector3(s, 0.21f, 0.02f), new Vector3(0.09f, 0.42f, 0.40f), Concrete, q);
            Box(p + q * new Vector3(s, 0.62f, -0.21f), new Vector3(0.06f, 0.44f, 0.05f), Concrete, q * Quaternion.Euler(-12, 0, 0));
        }
        for (int i = 0; i < 4; i++) Box(p + q * new Vector3(0, 0.44f, -0.15f + i * 0.11f), new Vector3(1.6f, 0.035f, 0.085f), BenchRed, q);
        for (int i = 0; i < 2; i++) Box(p + q * new Vector3(0, 0.62f + i * 0.14f, -0.235f - i * 0.03f), new Vector3(1.6f, 0.095f, 0.03f), BenchRed, q * Quaternion.Euler(-12, 0, 0));
    }

    static void Bin(float x, float z)
    {
        Pole(x, z, 0, 0.9f, 0.025f, DarkMetal, 6); Shade(x + 0.19f, z, 0.26f, 0.26f, 0.4f, true);
        Lathe(new Vector3(x + 0.19f, 0.42f, z), new[] { 0f, 0f, 0.15f, 0f, 0.17f, 0.45f, 0.15f, 0.45f, 0.14f, 0.05f, 0f, 0.05f }, 18, C(0.16f, 0.36f, 0.22f));
    }

    static void RoundTable(float x, float z, Color chair)
    {
        Pole(x, z, 0, 0.70f, 0.03f, White, 6); Shade(x, z, 0.62f, 0.62f, 0.7f, true);
        Lathe(new Vector3(x, 0, z), new[] { 0f, 0f, 0.22f, 0f, 0.22f, 0.02f, 0f, 0.02f }, 10, White);
        Lathe(new Vector3(x, 0.70f, z), new[] { 0f, 0f, 0.41f, -0.16f, 0.42f, 0.01f, 0.40f, 0.03f, 0f, 0.03f }, 28, White);       // table cloth
        Lathe(new Vector3(x + 0.08f, 0.73f, z - 0.05f), new[] { 0f, 0f, 0.035f, 0f, 0.045f, 0.10f, 0.02f, 0.13f, 0f, 0.13f }, 8, Blue, null, false, 1);   // vase
        Blob(x + 0.08f, 0.93f, z - 0.05f, 0.07f, 0.06f, 0.07f, Pick(Red, Yellow, Pink), 1, 0.3f, 1);
        float a0 = R(0, 6.28f);
        for (int i = 0; i < 3; i++)
        {
            float a = a0 + i * 2.1f; var q = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, 0);
            var c = new Vector3(x + Mathf.Cos(a) * 0.72f, 0, z + Mathf.Sin(a) * 0.72f);
            Lathe(c + Vector3.up * 0.44f, new[] { 0f, 0f, 0.19f, 0f, 0.20f, 0.03f, 0f, 0.04f }, 18, chair);
            Shade(c.x, c.z, 0.30f, 0.30f, 0.44f, true);
            Box(c + q * new Vector3(0, 0.74f, 0.19f), new Vector3(0.34f, 0.22f, 0.025f), chair, q);
            foreach (var l in new[] { new Vector2(-0.15f, -0.15f), new Vector2(0.15f, -0.15f), new Vector2(-0.16f, 0.18f), new Vector2(0.16f, 0.18f) })
                Beam(c + q * new Vector3(l.x, 0, l.y), c + q * new Vector3(l.x * 0.85f, l.y > 0 ? 0.64f : 0.44f, l.y), 0.016f, 0.016f, White, 1);
        }
    }

    static void Parasol(float x, float z, Color col, float r = 1.5f)
    {
        Lathe(new Vector3(x, 0, z), new[] { 0f, 0f, 0.25f, 0f, 0.25f, 0.08f, 0f, 0.08f }, 10, Concrete);
        Pole(x, z, 0, 2.45f, 0.022f, Metal, 6); Shade(x, z, r * 1.25f, r * 1.25f, 2.05f, true); Shade(x, z, 0.35f, 0.35f, 0.05f, true);
        Lathe(new Vector3(x, 0, z), new[] { 0f, 2.10f, r, 1.98f, r, 2.00f, 0f, 2.45f }, 8, col, null, true);
    }

    static void BeerSet(float x, float z, float yaw)
    {
        var q = Quaternion.Euler(0, yaw, 0); var p = new Vector3(x, 0, z);
        var green = C(0.20f, 0.42f, 0.25f);
        { var ext = q * new Vector3(1.2f, 0, 0.85f); Shade(x, z, Mathf.Abs(ext.x) + 0.3f, Mathf.Abs(ext.z) + 0.3f, 0.5f, true); }
        Box(p + q * new Vector3(0, 0.76f, 0), new Vector3(2.2f, 0.035f, 0.55f), Wood, q);
        foreach (float s in new[] { -0.65f, 0.65f })
        {
            Box(p + q * new Vector3(0, 0.46f, s), new Vector3(2.2f, 0.03f, 0.25f), Wood, q);
            foreach (float e in new[] { -0.85f, 0.85f }) Box(p + q * new Vector3(e, 0.22f, s), new Vector3(0.03f, 0.44f, 0.22f), green, q);
        }
        foreach (float e in new[] { -0.85f, 0.85f }) Box(p + q * new Vector3(e, 0.37f, 0), new Vector3(0.03f, 0.74f, 0.45f), green, q);
    }

    static void Pavilion(float x, float z)
    {
        const float s = 1.5f;
        Shade(x, z, 2.4f, 2.4f, 2.2f, true);
        foreach (var c in new[] { new Vector2(-s, -s), new Vector2(s, -s), new Vector2(-s, s), new Vector2(s, s) }) Pole(x + c.x, z + c.y, 0, 2.15f, 0.025f, Metal, 6);
        Lathe(new Vector3(x, 0, z), new[] { 0f, 2.15f, s * 1.4142f, 2.05f, s * 1.4142f, 2.20f, 0f, 3.0f }, 4, White, null, true, 0, Mathf.PI / 4);
    }

    static void TableTennis(float x, float z, float yaw)
    {
        var q = Quaternion.Euler(0, yaw, 0); var p = new Vector3(x, 0, z);
        Box(p + q * new Vector3(0, 0.74f, 0), new Vector3(2.74f, 0.06f, 1.525f), Concrete, q);
        { var ext = q * new Vector3(1.37f, 0, 0.76f); Shade(x, z, Mathf.Abs(ext.x) + 0.45f, Mathf.Abs(ext.z) + 0.45f, 0.74f, true); }
        Box(p + q * new Vector3(0, 0.84f, 0), new Vector3(0.012f, 0.15f, 1.60f), DarkMetal, q);
        foreach (float e in new[] { -0.85f, 0.85f }) Box(p + q * new Vector3(e, 0.355f, 0), new Vector3(0.09f, 0.71f, 1.0f), Yellow, q);
        Box(p + q * new Vector3(0, 0.774f, 0), new Vector3(2.74f, 0.004f, 0.02f), White, q, 1);
    }

    /// rustic crossed-lath fence (Jaegerzaun)
    static void Fence(Vector3 a, Vector3 b)
    {
        var d = b - a; float len = d.magnitude; var dir = d / len; var side = Vector3.Cross(Vector3.up, dir) * 0.012f;
        for (float s = 0; s <= len + 0.01f; s += 2f) Box(a + dir * Mathf.Min(s, len) + Vector3.up * 0.45f, new Vector3(0.07f, 0.9f, 0.07f), WoodDark, Quaternion.LookRotation(dir));
        for (float s = 0; s < len - 0.2f; s += 0.16f)
        {
            float e = Mathf.Min(s + 0.62f, len);
            Beam(a + dir * s + Vector3.up * 0.06f + side, a + dir * e + Vector3.up * (0.06f + (e - s) * 1.18f) + side, 0.028f, 0.012f, WoodDark, 1);
            Beam(a + dir * e + Vector3.up * 0.06f - side, a + dir * s + Vector3.up * (0.06f + (e - s) * 1.18f) - side, 0.028f, 0.012f, WoodDark, 1);
        }
    }

    /// string of coloured bulbs hanging between two points
    static void StringLights(Vector3 a, Vector3 b, float sag)
    {
        int n = Mathf.Max(6, (int)((b - a).magnitude / 0.45f));
        Vector3 P(int i) { float t = (float)i / n; return Vector3.Lerp(a, b, t) - Vector3.up * sag * 4 * t * (1 - t); }
        for (int i = 0; i < n; i++)
        {
            Beam(P(i), P(i + 1), 0.008f, 0.008f, DarkMetal, 1);
            if (i > 0) Blob(P(i) - Vector3.up * 0.045f, new Vector3(0.035f, 0.05f, 0.035f), Bulbs[i % Bulbs.Length], 1, 0, 1);
        }
    }

    // ------------------------------------------------------------------ buildings
    static void Kiosk(float x, float z)
    {
        const float w = 4.4f, d = 3.0f, h = 2.45f;
        var o = new Vector3(x, 0, z);
        Shade(x, z + 0.45f, w / 2 + 1.6f, d / 2 + 1.9f, h * 0.55f, true); Shade(x, z + 0.45f, w / 2 + 1.3f, d / 2 + 1.6f, h, true);
        Box(o + new Vector3(0, h / 2, 0), new Vector3(w, h, d), Sage);
        Box(o + new Vector3(0, 0.06f, 0), new Vector3(w + 0.1f, 0.12f, d + 0.1f), Concrete);
        // clapboard lines on the front and the sides
        for (float y = 0.22f; y < h - 0.05f; y += 0.15f)
        {
            Box(o + new Vector3(0, y, d / 2 + 0.004f), new Vector3(w, 0.022f, 0.012f), SageDark, null, 1);
            Box(o + new Vector3(w / 2 + 0.004f, y, 0), new Vector3(0.012f, 0.022f, d), SageDark, null, 1);
            Box(o + new Vector3(-w / 2 - 0.004f, y, 0), new Vector3(0.012f, 0.022f, d), SageDark, null, 1);
        }
        var blue = C(0.50f, 0.63f, 0.78f); var blueDark = C(0.42f, 0.54f, 0.69f);
        Box(o + new Vector3(-w / 2 - 0.002f, h / 2, 0), new Vector3(0.01f, h, d), blue);
        for (float y = 0.22f; y < h - 0.05f; y += 0.15f) Box(o + new Vector3(-w / 2 - 0.009f, y, 0), new Vector3(0.012f, 0.022f, d), blueDark, null, 1);
        // orange corner posts
        foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
            Box(o + new Vector3(sx * w / 2, h / 2, sz * d / 2), new Vector3(0.11f, h, 0.11f), Orange);
        // serving window with frame, shutter, counter and the shelves inside
        float fz = d / 2 + 0.02f;
        Box(o + new Vector3(0.25f, 1.55f, fz), new Vector3(1.9f, 1.05f, 0.03f), C(0.10f, 0.09f, 0.08f));
        Box(o + new Vector3(0.25f, 2.105f, fz + 0.01f), new Vector3(2.06f, 0.07f, 0.06f), Orange); Box(o + new Vector3(0.25f, 1.0f, fz + 0.01f), new Vector3(2.06f, 0.07f, 0.06f), Orange);
        Box(o + new Vector3(-0.74f, 1.55f, fz + 0.01f), new Vector3(0.07f, 1.1f, 0.06f), Orange); Box(o + new Vector3(1.24f, 1.55f, fz + 0.01f), new Vector3(0.07f, 1.1f, 0.06f), Orange);
        Box(o + new Vector3(0.25f, 1.02f, fz + 0.20f), new Vector3(2.1f, 0.045f, 0.36f), Orange);
        foreach (float sy in new[] { 1.45f, 1.78f })
        {
            Box(o + new Vector3(0.25f, sy, fz + 0.012f), new Vector3(1.8f, 0.02f, 0.02f), Wood, null, 1);
            for (float bx = -0.55f; bx < 1.1f; bx += R(0.09f, 0.16f))
                Box(o + new Vector3(bx, sy + 0.085f, fz + 0.014f), new Vector3(0.05f, R(0.11f, 0.17f), 0.02f), Pick(C(0.35f, 0.22f, 0.10f), C(0.20f, 0.45f, 0.25f), Red, Yellow, White, C(0.25f, 0.35f, 0.6f)), null, 1);
        }
        // chalkboards with scribbled price lists
        foreach (float bx in new[] { -1.45f, 1.75f })
        {
            float bw = bx < 0 ? 0.95f : 0.6f;
            Box(o + new Vector3(bx, 1.5f, fz), new Vector3(bw, 1.25f, 0.03f), Chalk);
            Box(o + new Vector3(bx, 1.5f, fz - 0.005f), new Vector3(bw + 0.08f, 1.33f, 0.03f), Orange);
            for (float y = 1.98f; y > 1.0f; y -= 0.12f)
                Box(o + new Vector3(bx - bw * 0.42f + R(0.1f, bw * 0.3f), y, fz + 0.018f), new Vector3(R(bw * 0.25f, bw * 0.6f), 0.022f, 0.004f), C(0.85f, 0.87f, 0.84f), null, 1);
        }
        // flat roof with a deep front overhang, fascia lettering
        Box(o + new Vector3(0, h + 0.07f, 0.45f), new Vector3(w + 1.1f, 0.14f, d + 1.7f), C(0.30f, 0.27f, 0.24f));
        float fasciaZ = d / 2 + 0.45f + 0.85f;
        Box(o + new Vector3(0, h + 0.20f, fasciaZ - 0.02f), new Vector3(w + 1.1f, 0.40f, 0.05f), C(0.27f, 0.20f, 0.16f));
        const string name = "BAHNENGOLF";
        float tw = TextWidth(name, 0.19f, 0.17f, 0.035f);
        Text(name, o + new Vector3(tw / 2, h + 0.09f, fasciaZ + 0.012f), Vector3.left, Vector3.up, 0.22f, 0.19f, 0.17f, 0.035f, White);
        // door on the right-hand side
        Box(o + new Vector3(w / 2 + 0.012f, 1.02f, -0.6f), new Vector3(0.03f, 2.0f, 0.85f), SageDark);
        Box(o + new Vector3(w / 2 + 0.02f, 2.04f, -0.6f), new Vector3(0.04f, 0.07f, 0.97f), Orange);
        foreach (float sz in new[] { -1.06f, -0.14f }) Box(o + new Vector3(w / 2 + 0.02f, 1.02f, sz), new Vector3(0.04f, 2.04f, 0.07f), Orange);
        // mushroom lamps on the roof and bulbs along the eave
        MushroomLamp(x - 1.6f, z - 0.4f, h + 1.5f, Red); MushroomLamp(x - 1.15f, z + 0.3f, h + 0.9f, Yellow);
        StringLights(o + new Vector3(-w / 2 - 0.5f, h + 0.0f, fasciaZ), o + new Vector3(w / 2 + 0.5f, h + 0.0f, fasciaZ), 0.10f);
        // putter rack and ball basket on the left wall
        Box(o + new Vector3(-w / 2 - 0.2f, 0.95f, 0.5f), new Vector3(0.10f, 0.05f, 1.3f), Wood);
        for (int i = 0; i < 7; i++)
        {
            float pz = 0.5f - 0.55f + i * 0.18f;
            Beam(o + new Vector3(-w / 2 - 0.34f, 0.03f, pz), o + new Vector3(-w / 2 - 0.2f, 1.0f, pz), 0.014f, 0.014f, Metal, 1);
            Box(o + new Vector3(-w / 2 - 0.37f, 0.03f, pz), new Vector3(0.11f, 0.035f, 0.028f), DarkMetal, null, 1);
        }
    }

    static readonly Color[] Facades = { C(0.86f, 0.80f, 0.66f), C(0.78f, 0.63f, 0.46f), C(0.60f, 0.29f, 0.23f), C(0.80f, 0.80f, 0.77f), C(0.88f, 0.84f, 0.74f), C(0.70f, 0.72f, 0.62f) };
    /// apartment house of the Berlin kind; `yaw` turns its window front (local -z) towards the park
    static void House(float x, float z, float w, float d, int floors, float yaw, Color col)
    {
        var q = Quaternion.Euler(0, yaw, 0); var o = new Vector3(x, 0, z);
        float h = 1.2f + floors * 3.5f;
        Box(o + Vector3.up * (h / 2 - 1f), new Vector3(w, h + 2f, d), col, q, 1);
        Box(o + Vector3.up * (h + 0.15f), new Vector3(w + 0.5f, 0.3f, d + 0.5f), C(0.84f, 0.82f, 0.76f), q, 1);
        Box(o + Vector3.up * (h + 1.4f), new Vector3(w - 0.6f, 2.4f, d - 2.2f), C(0.36f, 0.20f, 0.17f), q, 1);
        var win = C(0.19f, 0.24f, 0.30f);
        for (int f = 1; f < floors; f++) Box(o + q * new Vector3(0, 1.2f + f * 3.5f, -d / 2 - 0.04f), new Vector3(w, 0.16f, 0.10f), C(0.84f, 0.82f, 0.76f), q, 1);
        Box(o + q * new Vector3(0, 0.6f, -d / 2 - 0.05f), new Vector3(w, 1.2f, 0.12f), new Color(col.r * 0.8f, col.g * 0.8f, col.b * 0.8f), q, 1);
        for (int i = 0; i < 2; i++) Box(o + q * new Vector3(R(-w * 0.35f, w * 0.35f), h + 3.0f, R(-1.5f, 1.5f)), new Vector3(0.7f, 1.6f, 0.9f), C(0.45f, 0.26f, 0.22f), q, 1);
        int cols = Mathf.Max(3, (int)(w / 2.7f));
        for (int f = 0; f < floors; f++)
            for (int i = 0; i < cols; i++)
            {
                float lx = -w / 2 + (i + 0.5f) * w / cols, y = 2.9f + f * 3.5f;
                Box(o + q * new Vector3(lx, y, -d / 2 - 0.03f), new Vector3(1.15f, 1.9f, 0.08f), win, q, 1);
                Box(o + q * new Vector3(lx, y, -d / 2 - 0.01f), new Vector3(1.42f, 2.2f, 0.06f), C(0.90f, 0.89f, 0.85f), q, 1);
                Box(o + q * new Vector3(lx, y + 0.15f, -d / 2 - 0.075f), new Vector3(1.15f, 0.05f, 0.02f), C(0.90f, 0.89f, 0.85f), q, 1);
                Box(o + q * new Vector3(lx, y, -d / 2 - 0.075f), new Vector3(0.05f, 1.9f, 0.02f), C(0.90f, 0.89f, 0.85f), q, 1);
                Box(o + q * new Vector3(lx, y - 1.03f, -d / 2 - 0.07f), new Vector3(1.4f, 0.10f, 0.16f), C(0.84f, 0.82f, 0.76f), q, 1);
                if ((i + f * 2) % 5 == 2 && f > 0) Box(o + q * new Vector3(lx, y - 0.75f, -d / 2 - 0.5f), new Vector3(1.9f, 0.95f, 1.0f), C(0.30f, 0.30f, 0.31f), q, 1);
            }
    }

    static void Cloud(float x, float y, float z, float s)
    {
        int n = 4 + rnd.Next(4);
        for (int i = 0; i < n; i++)
            Blob(new Vector3(x + R(-1.4f, 1.4f) * s, y + R(-0.12f, 0.2f) * s, z + R(-0.7f, 0.7f) * s), new Vector3(R(0.7f, 1.2f) * s, R(0.28f, 0.42f) * s, R(0.6f, 0.9f) * s), C(0.97f, 0.97f, 0.98f), 2, 0.18f, 1);
    }

    // seven-segment lane numbers (segments a..g: top, top right, bottom right, bottom, bottom left, top left, middle)
    static readonly string[] Segments = { "abcdef", "bc", "abged", "abgcd", "fgbc", "afgcd", "afgedc", "abc", "abcdefg", "abfgcd" };
    static void Number(int number, Vector3 centre, Quaternion q)
    {
        const float w = 0.045f, h = 0.09f, t = 0.011f, gap = 0.022f;
        string str = number.ToString();
        float total = str.Length * w + (str.Length - 1) * gap;
        var ink = C(0.10f, 0.10f, 0.12f);
        for (int k = 0; k < str.Length; k++)
        {
            var c = centre + q * new Vector3(-total / 2 + w / 2 + k * (w + gap), 0, 0);
            foreach (char seg in Segments[str[k] - '0'])
            {
                Vector3 p; Vector3 size;
                switch (seg)
                {
                    case 'a': p = new Vector3(0, h / 2, 0); size = new Vector3(w + t, t, 0.004f); break;
                    case 'd': p = new Vector3(0, -h / 2, 0); size = new Vector3(w + t, t, 0.004f); break;
                    case 'g': p = new Vector3(0, 0, 0); size = new Vector3(w + t, t, 0.004f); break;
                    case 'b': p = new Vector3(w / 2, h / 4, 0); size = new Vector3(t, h / 2 + t, 0.004f); break;
                    case 'c': p = new Vector3(w / 2, -h / 4, 0); size = new Vector3(t, h / 2 + t, 0.004f); break;
                    case 'e': p = new Vector3(-w / 2, -h / 4, 0); size = new Vector3(t, h / 2 + t, 0.004f); break;
                    default: p = new Vector3(-w / 2, h / 4, 0); size = new Vector3(t, h / 2 + t, 0.004f); break;
                }
                Box(c + q * p, size, ink, q, 1);
            }
        }
    }

    // ------------------------------------------------------------------ the garden
    struct LaneBox { public Vector2 pos; public float yaw; public Vector2 min, max; }
    static readonly List<LaneBox> lanes = new();
    static readonly List<Vector4> reserved = new();     // x0, z0, x1, z1: kiosk, terrace, paths
    static readonly List<Vector3> occupied = new();     // x, z, radius of things already placed
    static readonly List<Vector2> treeSpots = new();
    static float gx0, gx1, gz0, gz1;
    const float StandBack = 1.6f;                       // the player's place behind the tee belongs to the lane

    static Vector2 ToLane(LaneBox l, float x, float z)
    {
        float a = l.yaw * Mathf.Deg2Rad, dx = x - l.pos.x, dz = z - l.pos.y;
        return new Vector2(dx * Mathf.Cos(a) - dz * Mathf.Sin(a), dx * Mathf.Sin(a) + dz * Mathf.Cos(a));
    }
    static Vector2 FromLane(LaneBox l, float lx, float lz)
    {
        float a = l.yaw * Mathf.Deg2Rad;
        return l.pos + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * lx + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * lz;
    }
    /// distance to the nearest lane (its plate plus the player's place behind the tee)
    static float LaneDist(float x, float z, int skip = -1)
    {
        float best = 1e9f;
        for (int i = 0; i < lanes.Count; i++)
        {
            if (i == skip) continue;
            var l = lanes[i]; var p = ToLane(l, x, z);
            float dx = Mathf.Max(Mathf.Max(l.min.x - p.x, p.x - l.max.x), 0), dz = Mathf.Max(Mathf.Max(-StandBack - p.y, p.y - l.max.y), 0);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }
    static bool Clear(float x, float z, float r, float lanePad)
    {
        if (x < gx0 + 1.3f + r || x > gx1 - 1.3f - r || z < gz0 + 0.9f + r || z > gz1 - 1.3f - r) return false;
        if (LaneDist(x, z) < r + lanePad) return false;
        foreach (var q in reserved) if (x > q.x - r && x < q.z + r && z > q.y - r && z < q.w + r) return false;
        foreach (var o in occupied) if ((x - o.x) * (x - o.x) + (z - o.y) * (z - o.y) < (r + o.z) * (r + o.z)) return false;
        return true;
    }
    /// a free place of radius r inside the given rectangle; it is marked as taken
    static bool Spot(float r, float lanePad, out Vector2 p, float x0 = float.NaN, float z0 = 0, float x1 = 0, float z1 = 0, int tries = 80)
    {
        if (float.IsNaN(x0)) { x0 = gx0; x1 = gx1; z0 = gz0; z1 = gz1; }
        for (int i = 0; i < tries; i++)
        {
            float x = R(x0, x1), z = R(z0, z1);
            if (!Clear(x, z, r, lanePad)) continue;
            occupied.Add(new Vector3(x, z, r)); p = new Vector2(x, z); return true;
        }
        p = default; return false;
    }

    /// origins = tee of every lane (y = height of the playing surface above the paving), yaws = its direction in degrees.
    /// All coordinates are local to the park root; y = 0 is the ground.
    public static void Build(Transform root, IList<LaneDef> defs, Vector3[] origins, float[] yaws, int n, float laneElevation)
    {
        parent = root; rnd = new System.Random(18);
        batches.Clear(); batchColor.Clear(); lanes.Clear(); reserved.Clear(); occupied.Clear(); treeSpots.Clear();

        // ---- the lanes and the extent of the garden around them
        float bx0 = 1e9f, bx1 = -1e9f, bz0 = 1e9f, bz1 = -1e9f;
        for (int i = 0; i < n; i++)
        {
            var (mn, mx) = LaneGeometry.Bounds(defs[i]);
            var l = new LaneBox { pos = new Vector2(origins[i].x, origins[i].z), yaw = yaws[i], min = mn, max = mx };
            lanes.Add(l);
            foreach (var c in new[] { FromLane(l, mn.x, -StandBack), FromLane(l, mx.x, -StandBack), FromLane(l, mn.x, mx.y), FromLane(l, mx.x, mx.y) })
            { bx0 = Mathf.Min(bx0, c.x); bx1 = Mathf.Max(bx1, c.x); bz0 = Mathf.Min(bz0, c.y); bz1 = Mathf.Max(bz1, c.y); }
        }
        gx0 = bx0 - 3.4f; gx1 = bx1 + 3.4f; gz1 = bz1 + 3.4f; gz0 = bz0 - 9.6f;        // the front 9 m: kiosk, terrace, entrance
        float gateX = Mathf.Min(gx0 + 22.5f, gx1 - 6f);

        smSize = Mathf.Max(gx1 - gx0, gz1 - gz0) + 12f;
        smX0 = (gx0 + gx1) / 2 - smSize / 2; smZ0 = (gz0 + gz1) / 2 - smSize / 2;
        sm = new float[SM * SM]; smTree = new float[SM * SM];

        // ---- ground: lawn everywhere, a pad of slabs under every lane, terrace, entrance, sidewalk and street
        Vector4 terrace = new(gx0 + 0.8f, gz0 + 0.8f, gateX + 1.2f, gz0 + 8.6f);
        try
        {
            GroundQuad("LawnTex", -1500, -1500, 1500, 1500, -0.006f, LawnMat(), 4f);
            var lawn = root.Find("Lawn"); if (lawn != null) lawn.GetComponent<MeshRenderer>().enabled = false;
            for (int i = 0; i < n; i++)
            {
                var l = lanes[i];
                var a = FromLane(l, l.min.x - 0.9f, -2.0f); var b = FromLane(l, l.max.x + 0.9f, l.max.y + 0.8f);
                if (Mathf.Abs(Mathf.Repeat(l.yaw + 45f, 90f) - 45f) < 0.5f)       // square to the garden: all pads share one grid of slabs
                    GroundQuad("Pad" + (i + 1), Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), 0.012f + i * 0.0003f, PavingMat(), 2f);
                else
                    Pad("Pad" + (i + 1), origins[i], l.yaw, l.min.x - 0.9f, -2.0f, l.max.x + 0.9f, l.max.y + 0.8f, 0.012f + i * 0.0003f);
            }
            GroundQuad("Paving_Terrace", terrace.x, terrace.y, terrace.z, terrace.w, 0.011f, PavingMat(), 2f);
            GroundQuad("Paving_Entrance", gateX - 1.2f, gz0 - 1.2f, gateX + 1.2f, gz0 + 9.7f, 0.0105f, PavingMat(), 2f);
            GroundQuad("Paving_Sidewalk", gx0 - 40f, gz0 - 4.2f, gx1 + 40f, gz0 - 1.2f, 0.010f, PavingMat(), 2f);
        }
        catch (Exception e) { Plugin.L.LogWarning("Park ground textures: " + e.Message); }
        reserved.Add(terrace); reserved.Add(new Vector4(gateX - 1.4f, gz0 - 2f, gateX + 1.4f, gz0 + 9.9f));
        Box((gx0 + gx1) / 2, -0.02f, gz0 - 8.2f, gx1 - gx0 + 80f, 0.05f, 8f, C(0.24f, 0.24f, 0.26f), 0, 1);       // street
        for (float x = gx0 - 38f; x < gx1 + 38f; x += 6f) Box(x, 0.008f, gz0 - 8.2f, 3f, 0.004f, 0.12f, White, 0, 1);

        // ---- kiosk and beer garden on the terrace
        float kx = gx0 + 4.2f, kz = gz0 + 3.7f;
        Kiosk(kx, kz);
        Color[] chairs = { BenchRed, Orange, Orange, BenchRed, C(0.93f, 0.75f, 0.20f) };
        var tables = new[] { new Vector2(gx0 + 8.7f, gz0 + 6.0f), new Vector2(gx0 + 11.3f, gz0 + 3.0f), new Vector2(gx0 + 13.9f, gz0 + 6.6f), new Vector2(gx0 + 16.7f, gz0 + 3.4f), new Vector2(gx0 + 19.2f, gz0 + 6.4f) };
        for (int i = 0; i < tables.Length; i++) if (tables[i].x < gateX - 1.9f) RoundTable(tables[i].x, tables[i].y, chairs[i % chairs.Length]);
        Parasol(gx0 + 10.1f, gz0 + 4.6f, Peach); Parasol(gx0 + 15.4f, gz0 + 5.0f, Red, 1.3f);
        Bin(kx + 3.0f, kz + 1.9f);
        Bed(gx0 + 1.9f, gz0 + 7.4f, 0.9f, 0.9f, Pink, false);
        var pA = new Vector3(gateX - 1.5f, 3.3f, terrace.w - 0.3f); var pB = new Vector3(gateX - 1.5f, 3.3f, terrace.y + 0.3f); var pC = new Vector3(gx0 + 7.6f, 3.3f, terrace.w - 0.3f);
        foreach (var p in new[] { pA, pB, pC }) { Pole(p.x, p.z, 0, p.y + 0.05f, 0.03f, C(0.80f, 0.80f, 0.78f), 10); Shade(p.x, p.z, 0.12f, 0.12f, 0.05f, true); }
        var kRoof = new Vector3(kx + 2.6f, 2.62f, kz + 3.2f);
        StringLights(kRoof, pA, 0.45f); StringLights(pA, pB, 0.40f); StringLights(pB, new Vector3(kx + 2.6f, 2.62f, kz - 1.0f), 0.35f); StringLights(kRoof, pC, 0.2f); StringLights(pC, pA, 0.7f);

        // ---- at every lane: number sign, a mushroom lamp, here and there a bench; stepping stones lead to the next tee
        Color[] lampCols = { Red, Blue, Yellow, Orange, White, Blue, Red, Yellow };
        for (int i = 0; i < n; i++)
        {
            var l = lanes[i]; var q = Quaternion.Euler(0, l.yaw, 0);
            var s = FromLane(l, l.min.x - 0.30f, 0.1f);
            Pole(s.x, s.y, 0, 0.70f + laneElevation, 0.015f, C(0.30f, 0.30f, 0.32f), 8);
            var sc = new Vector3(s.x, 0.78f + laneElevation, s.y);
            Box(sc, new Vector3(0.22f, 0.16f, 0.02f), C(0.95f, 0.95f, 0.90f), q);
            Number(i + 1, sc + q * new Vector3(0, 0, -0.012f), q);

            foreach (float side in new[] { 1f, -1f })
            {
                var lp = FromLane(l, side > 0 ? l.max.x + 1.15f : l.min.x - 1.15f, 0.9f);
                if (LaneDist(lp.x, lp.y, i) < 0.45f || !Clear(lp.x, lp.y, 0.3f, 0f)) continue;
                MushroomLamp(lp.x, lp.y, R(1.6f, 2.3f), lampCols[i % lampCols.Length]); occupied.Add(new Vector3(lp.x, lp.y, 0.35f)); break;
            }
            if (i % 2 == 0)
                foreach (float side in new[] { -1f, 1f })
                {
                    var bp = FromLane(l, side > 0 ? l.max.x + 1.45f : l.min.x - 1.45f, l.max.y * 0.55f);
                    if (LaneDist(bp.x, bp.y, i) < 1.3f || !Clear(bp.x, bp.y, 0.9f, 0f)) continue;
                    Bench(bp.x, bp.y, l.yaw - side * 90f); occupied.Add(new Vector3(bp.x, bp.y, 1.0f)); break;
                }
            if (i + 1 < n)
            {
                var a = FromLane(l, 0, l.max.y + 1.0f); var b = FromLane(lanes[i + 1], 0, -1.3f);
                float len = (b - a).magnitude, yaw = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg;
                for (float d = 0.2f; d < len; d += 0.74f)
                {
                    var p = Vector2.Lerp(a, b, d / len);
                    if (LaneDist(p.x, p.y) < 1.0f) continue;
                    bool free = true; foreach (var rq in reserved) if (p.x > rq.x && p.x < rq.z && p.y > rq.y && p.y < rq.w) free = false;
                    if (!free) continue;
                    Box(new Vector3(p.x, 0.012f, p.y), new Vector3(0.5f, 0.035f, 0.5f), Pick(Kerb, Stone, Concrete), Quaternion.Euler(0, yaw + R(-9f, 9f), 0), 1);
                    occupied.Add(new Vector3(p.x, p.y, 0.3f));
                }
            }
        }

        // ---- larger things on the lawn
        Vector2 sp;
        if (Spot(2.5f, 1.0f, out sp, gateX + 2.5f, gz0 + 1.5f, gx1 - 2f, gz0 + 7.5f)) { Pavilion(sp.x, sp.y); BeerSet(sp.x, sp.y, 90); }
        if (Spot(1.6f, 1.0f, out sp, gateX + 1.8f, gz0 + 1.5f, gx1 - 2f, gz0 + 8f)) BeerSet(sp.x, sp.y, R(-15, 15));
        if (Spot(2.4f, 1.0f, out sp))
        {
            TableTennis(sp.x, sp.y, 90);
            try { GroundQuad("Paving_TT", sp.x - 1.7f, sp.y - 2.2f, sp.x + 1.7f, sp.y + 2.2f, 0.0115f, PavingMat(), 2f); } catch { }
        }
        for (int i = 0; i < 2; i++) if (Spot(1.7f, 1.0f, out sp)) BeerSet(sp.x, sp.y, Pick(0f, 90f) + R(-8, 8));

        // ---- trees in the garden: close to the lanes, so their crowns reach over them
        int wanted = Mathf.Max(8, (int)((gx1 - gx0) * (gz1 - gz0) / 115f)), made = 0;
        for (int tries = 0; tries < 2500 && made < wanted; tries++)
        {
            float x = R(gx0 + 2f, gx1 - 2f), z = R(gz0 + 1.5f, gz1 - 2f);
            if (!Clear(x, z, 0.45f, 0.25f)) continue;
            bool near = false; foreach (var ts in treeSpots) if ((ts.x - x) * (ts.x - x) + (ts.y - z) * (ts.y - z) < 7.0f * 7.0f) near = true;
            if (near) continue;
            // prefer places next to a lane or the terrace over the open lawn
            if (LaneDist(x, z) > 3.2f && rnd.Next(3) != 0) continue;
            treeSpots.Add(new Vector2(x, z)); occupied.Add(new Vector3(x, z, 0.7f));
            Box(new Vector3(x, 0.018f, z), new Vector3(1.3f, 0.03f, 1.3f), Soil, null, 1);
            Tree(x, z, R(9.5f, 13f), made % 5 == 2, 3);
            made++;
        }

        // ---- planted beds, flower meadows, single shrubs and conifers, more lamps
        Color[] bedFlowers = { Yellow, Yellow, C(0.95f, 0.55f, 0.15f), Pink, Yellow, Purple, Red };
        Color[] crocus = { Purple, White, C(0.98f, 0.85f, 0.35f), C(0.62f, 0.45f, 0.80f) };
        int beds = (int)((gx1 - gx0) * (gz1 - gz0) / 75f);
        for (int i = 0; i < beds; i++)
        {
            float rx = R(0.9f, 2.3f), rz = R(0.8f, 1.8f);
            if (Spot(Mathf.Max(rx, rz) + 0.1f, 1.0f, out sp)) Bed(sp.x, sp.y, rx, rz, bedFlowers[i % bedFlowers.Length], i % 3 == 0);
        }
        for (int i = 0; i < beds; i++)
            if (Spot(1.4f, 1.0f, out sp)) { float a = R(1.0f, 2.0f), b = R(0.8f, 1.4f); Meadow(sp.x, sp.y, a, b, (int)(a * b * 95), crocus); }
        for (int i = 0; i < beds; i++) if (Spot(0.7f, 1.0f, out sp)) { if (i % 3 == 0) Conifer(sp.x, sp.y, R(2.2f, 3.6f)); else Shrub(sp.x, sp.y, R(0.9f, 1.5f)); }
        for (int i = 0; i < beds / 2; i++) if (Spot(0.4f, 1.0f, out sp)) MushroomLamp(sp.x, sp.y, R(1.6f, 2.4f), Pick(Red, Blue, Yellow, Orange, White));
        // shrubs in front of the hedges
        for (float z = gz0 + 2f; z < gz1 - 1f; z += R(2.2f, 3.6f)) { Shrub(gx0 + R(1.1f, 1.7f), z, R(0.9f, 1.5f)); Shrub(gx1 - R(1.1f, 1.7f), z, R(0.9f, 1.5f)); }
        for (float x = gx0 + 2f; x < gx1 - 1f; x += R(2.2f, 3.6f)) Shrub(x, gz1 - R(1.1f, 1.7f), R(0.9f, 1.5f));

        // ---- fence along the street with the gate, hedges on the other three sides
        float ga = gateX - 1.15f, gb = gateX + 1.15f;
        Fence(new Vector3(gx0, 0, gz0), new Vector3(ga, 0, gz0)); Fence(new Vector3(gb, 0, gz0), new Vector3(gx1, 0, gz0));
        foreach (float g in new[] { ga, gb }) Box(g, 1.15f, gz0, 0.12f, 2.3f, 0.12f, WoodDark);
        Box(gateX, 2.42f, gz0, gb - ga + 0.5f, 0.34f, 0.06f, C(0.27f, 0.20f, 0.16f));
        const string gate = "MINIGOLF";
        float gw = TextWidth(gate, 0.15f, 0.07f, 0.03f);
        Text(gate, new Vector3(gateX - gw / 2, 2.33f, gz0 - 0.036f), Vector3.right, Vector3.up, 0.18f, 0.15f, 0.07f, 0.03f, White);
        Text(gate, new Vector3(gateX + gw / 2, 2.33f, gz0 + 0.036f), Vector3.left, Vector3.up, 0.18f, 0.15f, 0.07f, 0.03f, White);
        MushroomLamp(gateX + 1.7f, gz0 + 0.7f, 2.3f, Red); MushroomLamp(gateX - 1.7f, gz0 + 0.7f, 1.9f, Yellow);
        // chalk board at the gate
        {
            var ap = new Vector3(gateX + 1.75f, 0, gz0 - 0.9f);
            foreach (float s in new[] { -1f, 1f })
            {
                var q = Quaternion.Euler(s * 14f, 0, 0);
                Box(ap + new Vector3(0, 0.55f, s * 0.14f), new Vector3(0.62f, 1.12f, 0.025f), Red, q);
                Box(ap + new Vector3(0, 0.60f, s * 0.155f), new Vector3(0.50f, 0.80f, 0.01f), C(0.10f, 0.11f, 0.11f), q, 1);
                for (int i = 0; i < 6; i++)
                    Box(ap + q * new Vector3(R(-0.06f, 0.06f), 0.90f - i * 0.12f, 0) + new Vector3(0, 0, s * (0.165f + (0.90f - i * 0.12f - 0.55f) * -0.0f)), new Vector3(R(0.2f, 0.42f), 0.03f, 0.004f), C(0.88f, 0.90f, 0.88f), q, 1);
            }
            Shade(ap.x, ap.z, 0.5f, 0.5f, 0.4f, true);
        }
        // yellow perennials growing through the fence
        for (float x = gx0 + 0.6f; x < gx1 - 0.6f; x += 0.16f)
        {
            if (Mathf.Abs(x - gateX) < 1.5f || VNoise(x / 9f, 0.3f, 7, 51) < 0.42f) continue;
            float z = gz0 + R(-0.25f, 0.75f);
            Flower(x, z, R(0.55f, 1.15f), rnd.Next(7) == 0 ? C(0.95f, 0.55f, 0.15f) : Yellow);
            if (rnd.Next(3) == 0) Blob(new Vector3(x, R(0.2f, 0.45f), z), new Vector3(R(0.15f, 0.28f), R(0.15f, 0.3f), R(0.15f, 0.28f)), Pick(Leaf2, Leaf4, Leaf1), 1, 0.25f, 2);
        }
        HedgeRow(new Vector3(gx0, 0, gz0 + 0.6f), new Vector3(gx0, 0, gz1)); HedgeRow(new Vector3(gx1, 0, gz0 + 0.6f), new Vector3(gx1, 0, gz1)); HedgeRow(new Vector3(gx0, 0, gz1), new Vector3(gx1, 0, gz1));

        // ---- big trees outside, street trees, houses, clouds
        for (float x = gx0 - 3f; x < gx1 + 5f; x += R(5f, 7.5f)) Tree(x, gz1 + R(3f, 7f), R(10f, 14f), rnd.Next(5) == 0, 2);
        for (float z = gz0 + 3f; z < gz1 + 2f; z += R(5f, 7.5f)) { Tree(gx0 - R(3f, 6.5f), z, R(10f, 14f), rnd.Next(5) == 0, 2); Tree(gx1 + R(3f, 6.5f), z, R(10f, 14f), rnd.Next(5) == 0, 2); }
        for (float x = gx0 - 30f; x < gx1 + 32f; x += 11f) if (Mathf.Abs(x - gateX) > 4f) { Tree(x, gz0 - 2.9f, R(7.5f, 9.5f), false, 2); Box(x, 0.02f, gz0 - 2.9f, 1.4f, 0.03f, 1.4f, Soil, 0, 1); }
        float hx = gx0 - 46f;
        while (hx < gx1 + 40f) { float hw = R(15f, 23f); House(hx + hw / 2, gz1 + 30f, hw, 12f, 4 + rnd.Next(2), 0, Pick(Facades)); House(hx + hw / 2, gz0 - 19f, hw, 12f, 4 + rnd.Next(2), 180, Pick(Facades)); hx += hw + 0.3f; }
        float hz = gz0 - 6f;
        while (hz < gz1 + 22f) { float hw = R(15f, 23f); House(gx0 - 30f, hz + hw / 2, hw, 12f, 4 + rnd.Next(2), -90, Pick(Facades)); House(gx1 + 30f, hz + hw / 2, hw, 12f, 4 + rnd.Next(2), 90, Pick(Facades)); hz += hw + 0.3f; }
        for (int i = 0; i < 14; i++) { float a = i * 0.45f + R(0, 0.3f), d = R(180f, 420f); Cloud((gx0 + gx1) / 2 + Mathf.Cos(a) * d, R(95f, 150f), (gz0 + gz1) / 2 + Mathf.Sin(a) * d, R(22f, 40f)); }

        Flush();
        try { FlushShadows(defs, origins, yaws, n); } catch (Exception e) { Plugin.L.LogWarning("Park shadows: " + e); }
    }
}
