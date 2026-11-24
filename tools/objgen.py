# Small helpers for the moulded-obstacle generators (they write OBJ files in lane coordinates: x right, y up, z down
# the lane, metres). Triangles are wound so that Cross(b - a, c - a) is the outward normal, as the plugin expects.
import math, json, os, re

def sstep(u):
    u = min(1.0, max(0.0, u)); return u * u * (3 - 2 * u)

def cosramp(u):
    """0 at u = 0, 1 at u = 1, horizontal tangents at both ends"""
    u = min(1.0, max(0.0, u)); return (1 - math.cos(math.pi * u)) / 2

class Mesh:
    def __init__(self): self.v = []; self.f = []; self.key = {}
    def vert(self, p, group=0):
        k = (group, round(p[0], 4), round(p[1], 4), round(p[2], 4))
        if k not in self.key: self.key[k] = len(self.v); self.v.append((float(p[0]), float(p[1]), float(p[2])))
        return self.key[k]
    def tri(self, a, b, c):
        if len({a, b, c}) == 3: self.f.append((a, b, c))
    def skin(self, rings, group=0, flip=False, closed=False):
        """rings[i][k] = (x, y, z); profile ordered left to right over the top when looking along the stations"""
        idx = [[self.vert(p, group) for p in r] for r in rings]
        n = len(rings); m = len(rings[0])
        for i in range(n if closed else n - 1):
            j = (i + 1) % n
            for k in range(m - 1):
                a, b, c, d = idx[i][k], idx[j][k], idx[j][k + 1], idx[i][k + 1]
                for t in ((a, b, c), (a, c, d)): self.tri(*(t[::-1] if flip else t))
    def heightfield(self, fn, x0, x1, z0, z1, step, group=0, skirt=None):
        """surface y = fn(x, z) on a regular grid; skirt = y level the outer edge is dropped to (None: open edge)"""
        nx = int(round((x1 - x0) / step)); nz = int(round((z1 - z0) / step))
        xs = [x0 + (x1 - x0) * i / nx for i in range(nx + 1)]; zs = [z0 + (z1 - z0) * j / nz for j in range(nz + 1)]
        rings = [[(x, fn(x, z), z) for x in xs] for z in zs]       # stations along +z, profile left (-x) to right
        self.skin(rings, group)
        if skirt is not None:
            edge = [rings[0][i] for i in range(nx + 1)] + [rings[j][nx] for j in range(1, nz + 1)] + \
                   [rings[nz][i] for i in range(nx - 1, -1, -1)] + [rings[j][0] for j in range(nz - 1, 0, -1)]
            # walking the edge clockwise seen from above (x right, z up): outside is on the left -> top/bottom order
            self.skin([[(p[0], skirt, p[2]), p] for p in edge], group + 1000, closed=True)
    def write(self, path, title):
        with open(path, 'w') as o:
            o.write('# %s\n' % title)
            for v in self.v: o.write('v %.4f %.4f %.4f\n' % v)
            for f in self.f: o.write('f %d %d %d\n' % (f[0] + 1, f[1] + 1, f[2] + 1))
        print(os.path.basename(path), 'verts', len(self.v), 'tris', len(self.f))

def save_lane(path, header, lane):
    body = json.dumps(lane, indent=1)
    body = re.sub(r'\[\s+(-?[\d.]+),\s+(-?[\d.]+)\s+\]', r'[\1, \2]', body)
    body = re.sub(r'\[\s+(-?[\d.]+),\s+(-?[\d.]+),\s+(-?[\d.]+)\s+\]', r'[\1, \2, \3]', body)
    open(path, 'w', encoding='utf-8').write(header.rstrip('\n') + '\n' + body + '\n')

def lanes_dir(): return os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'lanes')

def line(name, a, b, color=(0.80, 0.08, 0.08), th=0.01, lift=0.0):
    p = {"name": name, "shape": "line", "points": [list(a), list(b)], "thickness": th, "color": list(color)}
    if lift: p["pos"] = [0, lift, 0]
    return p
