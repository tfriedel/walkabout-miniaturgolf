# Generates the Liegende Schleife (Eternit lane 5) as one welded model:
#   lanes/eternit_05_liegende_schleife.obj  + lanes/eternit_05_liegende_schleife.json
# Layout traced from the norm drawing (NormMiniatur Bahn 5, plan view) and the Vorsfelde video frames / minigolfbau
# product photo (shape of walls, tunnel, island, ramp). Lane coordinates: x to the player's right, z down the lane, y up.
import math, json, os, sys
import numpy as np

# ------------------------------------------------------------------ dimensions
Z0 = 2.50                    # obstacle start (norm: 200-300 cm)
EX, EZ = 0.0, 2.99           # centre of the first (outer) half turn
EA, EB = 0.365, 0.24         # its half axes: across the lane / along the lane
CX, CZ = -0.14, 2.99         # centre of the island and of the second (inner) half turn
R2A, R2B = 0.225, 0.19       # radius of the inner half turn, start / end
XE = CX + R2B                # x of the straight exit channel
H = 0.125                    # floor height of the exit channel where it bridges the entry
DECK = H - 0.066             # the tunnel under the bridge is 6.6 cm high; the body above it is solid
ZT0, ZT1 = 3.128, 3.312      # the tunnel under the exit channel (z range of the open underside)
ZR = 3.74                    # end of the exit ramp
FL = 0.002                   # thickness of the moulding's floor sheet where it lies on the lane plate
ISL_FOOT, ISL_CREST = 0.127, 0.095   # island: radius of the foot / of the crest of its ridge
PIT = 0.06                  # island: height of the pit floor
STEP = 0.012                 # station spacing along the path

def sstep(u):
    u = min(1.0, max(0.0, u)); return u * u * (3 - 2 * u)

def keys(s, pts, smooth=True):
    """piecewise interpolation through (s, value) keys"""
    if s <= pts[0][0]: return pts[0][1]
    for (a, va), (b, vb) in zip(pts, pts[1:]):
        if s <= b:
            u = (s - a) / (b - a) if b > a else 1.0
            return va + (vb - va) * (sstep(u) if smooth else u)
    return pts[-1][1]

# ------------------------------------------------------------------ path of the ball (plan view)
def dense_path():
    P = []   # x, z, tag
    n = 60
    for i in range(n): P.append((EA, Z0 + (EZ - Z0) * i / n, 'A'))
    n = 400
    for i in range(n):
        t = math.pi * i / n
        P.append((EX + EA * math.cos(t), EZ + EB * math.sin(t), 'B'))
    n = 300
    for i in range(n):
        u = i / n; psi = math.pi * (1 + u); r = R2A + (R2B - R2A) * sstep(u)
        P.append((CX + r * math.cos(psi), CZ + r * math.sin(psi), 'C'))
    n = 100
    for i in range(n + 1): P.append((XE, CZ + (ZR - CZ) * i / n, 'D'))
    return P

def resample(P, step):
    xs = np.array([p[0] for p in P]); zs = np.array([p[1] for p in P])
    d = np.hypot(np.diff(xs), np.diff(zs)); s = np.concatenate([[0], np.cumsum(d)])
    n = int(round(s[-1] / step)); out = []
    for i in range(n + 1):
        si = s[-1] * i / n
        j = min(len(P) - 2, int(np.searchsorted(s, si, side='right')) - 1)
        u = (si - s[j]) / max(1e-9, s[j + 1] - s[j])
        out.append([xs[j] + (xs[j + 1] - xs[j]) * u, zs[j] + (zs[j + 1] - zs[j]) * u, si, P[j][2]])
    return out

ST = resample(dense_path(), STEP)
N = len(ST)
X = np.array([p[0] for p in ST]); Z = np.array([p[1] for p in ST]); S = np.array([p[2] for p in ST]); TAG = [p[3] for p in ST]
TX = np.gradient(X); TZ = np.gradient(Z); L = np.hypot(TX, TZ); TX /= L; TZ /= L
RX, RZ = TZ, -TX            # unit vector to the right of travel

def s_where(tag, cond):
    """arclength of the first station with this tag fulfilling cond(x, z)"""
    for i in range(N):
        if TAG[i] == tag and cond(X[i], Z[i]): return S[i]
    raise ValueError(tag)

sB = s_where('B', lambda x, z: True)
sMouth = s_where('B', lambda x, z: x < 0.215)      # the entry dives under the exit channel's right wall
sIn = s_where('B', lambda x, z: x < 0.145)
sIn2 = s_where('B', lambda x, z: x < 0.12)
sOut = s_where('B', lambda x, z: x < -0.105)       # ... and comes out from under the ridge arm
sFree = s_where('B', lambda x, z: x < -0.19)
sApex = s_where('B', lambda x, z: x < 0.0)
sC = s_where('C', lambda x, z: True)
sCm = s_where('C', lambda x, z: x > CX)            # tee side of the inner turn
sD = s_where('D', lambda x, z: True)
sT0 = s_where('D', lambda x, z: z >= ZT0)
sT1 = s_where('D', lambda x, z: z >= ZT1)
sEnd = S[-1]

def floor_y(s):
    if s < sD: return keys(s, [(0, -0.004), (0.10, FL), (sOut, FL), (sD, H)])   # front edge buried in the plate: no edge for the ball to hit
    if s <= sT1: return H
    return keys(s, [(sT1, H), (sEnd - 0.03, 0.003), (sEnd, -0.004)])

FY = np.array([floor_y(s) for s in S])

# ------------------------------------------------------------------ mesh container
class Mesh:
    def __init__(self): self.v = []; self.f = []; self.key = {}
    def vert(self, p, group):
        k = (group, round(p[0], 4), round(p[1], 4), round(p[2], 4))
        if k not in self.key: self.key[k] = len(self.v); self.v.append((float(p[0]), float(p[1]), float(p[2])))
        return self.key[k]
    def skin(self, rings, group, flip=False, closed=False):
        """rings[i][k] = (x, y, z); profile ordered left to right over the top when looking along the stations"""
        idx = [[self.vert(p, group) for p in r] for r in rings]
        n = len(rings); m = len(rings[0])
        for i in range(n if closed else n - 1):
            j = (i + 1) % n
            for k in range(m - 1):
                a, b, c, d = idx[i][k], idx[j][k], idx[j][k + 1], idx[i][k + 1]
                for t in ((a, b, c), (a, c, d)):
                    if len(set(t)) == 3: self.f.append(t[::-1] if flip else t)
    def write(self, path):
        with open(path, 'w') as o:
            o.write('# Liegende Schleife - generated by tools/gen_liegende_schleife.py\n')
            for v in self.v: o.write('v %.4f %.4f %.4f\n' % (min(0.449, max(-0.449, v[0])), v[1], v[2]))   # nothing reaches past the Banden
            for f in self.f: o.write('f %d %d %d\n' % (f[0] + 1, f[1] + 1, f[2] + 1))

M = Mesh()
def at(i, off, y): return (X[i] + RX[i] * off, y, Z[i] + RZ[i] * off)

def foot_right(s):
    return keys(s, [(0, 0.065), (sC, 0.065), (sCm, 0.078), (sD, 0.065)]) if s < sD else 0.065

# ------------------------------------------------------------------ outer wall: one skin along the whole right side
def wall_right(i):
    s = S[i]; f = FY[i]; o0 = foot_right(s)
    if s < sD:
        bw = keys(s, [(0, 0.004), (sOut, 0.006), (sFree + 0.10, 0.07), (sC - 0.12, 0.05), (sC, 0.02), (sCm, 0.045), (sD - 0.10, 0.03), (sD, 0.02)])
        T = keys(s, [(0, 0.0), (0.10, 0.05), (sB + 0.10, 0.05), (sMouth - 0.07, 0.15), (sIn, 0.15), (sIn2, 0.061), (sOut, 0.061),
                     (sFree + 0.08, 0.22), (sC, 0.29), (sCm, 0.33), (sD - 0.05, H + 0.05), (sD, H + 0.045)])
        tw = keys(s, [(0, 0.012), (sOut, 0.012), (sFree + 0.10, 0.04), (sD, 0.035)])
        sk = keys(s, [(0, 0.0), (sB, 0.0), (sMouth - 0.05, 0.07), (sIn, 0.10), (sIn2, 0.02), (sOut, 0.02), (sApex + 0.25, 0.04), (sC - 0.05, 0.0), (sC + 0.10, 0.0),
                      (sCm, 0.07), (sD, 0.10)])
        base = 0.0
    else:
        bw, tw = 0.02, 0.035
        T = f + 0.045 * (1 - sstep((s - (sEnd - 0.28)) / 0.27))        # the wall tapers out towards the end of the ramp
        over = sT0 <= s <= sT1
        base = H - DECK if over else 0.0
        sk = keys(s, [(sD, 0.10), (sT1, 0.10), (sEnd, 0.012)])
    T = max(T, f + 0.002)
    pr = [(o0 - 0.02, f - 0.006), (o0, f)]
    for u in (0.12, 0.25, 0.4, 0.55, 0.7, 0.85, 1.0): pr.append((o0 + bw * u, f + (T - f) * u * u))
    c = o0 + bw
    pr += [(c + 0.25 * tw, T + 0.004), (c + 0.5 * tw, T + 0.005), (c + 0.75 * tw, T + 0.004), (c + tw, T)]
    e = c + tw
    # outside: a cone down to the plate; over the tunnel the same cone, cut off at the underside of the deck
    for q in (0.4, 0.75, 1.0):
        y = T - (T - base) * q
        pr.append((e + 0.004 * q + sk * (T - y) / max(T, 1e-4), y))
    return [at(i, o, y) for o, y in pr]

M.skin([wall_right(i) for i in range(N)], 'W')

# ------------------------------------------------------------------ ridge arm: left wall of the exit channel
def ridge(lo, hi, w):
    """rounded ridge: hi at w = 0, lo at w = 1, horizontal tangents at both ends"""
    return lo + (hi - lo) * (1 + math.cos(math.pi * w)) / 2

iD = [i for i in range(N) if TAG[i] == 'D']
def arm(i):
    s = S[i]; f = FY[i]
    Yc = f + 0.055 * (1 - sstep((s - (sEnd - 0.28)) / 0.27))
    Yc = max(Yc, f + 0.002)
    over = sT0 <= s <= sT1
    base = H - DECK if over else 0.0
    crest = XE - CX - ISL_CREST            # distance of the crest to the left of the path
    foot = XE - CX - ISL_FOOT
    # between the island and the arm the foot is drawn out, so the two flow into each other without a notch
    run = 0.04 + 0.07 * sstep((Z[i] - CZ) / 0.05) * sstep((CZ + 0.13 - Z[i]) / 0.06)
    pr = []
    wmax = 1.0 if not over else math.acos(2 * (base / Yc) - 1) / math.pi     # cut off at the underside of the deck
    for w in (1.0, 0.85, 0.7, 0.5, 0.3, 0.15): pr.append((-(crest + run * w * wmax), ridge(0.0, Yc, w * wmax)))
    pr.append((-crest, Yc))
    for w in (0.15, 0.3, 0.5, 0.7, 0.85, 1.0): pr.append((-(crest - (crest - foot) * w), ridge(f, Yc, w)))
    pr.append((-(foot - 0.02), f - 0.006))
    return [at(i, o, y) for o, y in pr]

M.skin([arm(i) for i in iD], 'W')

# ------------------------------------------------------------------ island: ring ridge with a pit, around (CX, CZ)
def path_floor_at_angle(phi):
    """floor height of the channel next to the island at island angle phi (rad, 0 = +x, pi/2 = target side)"""
    best = None
    for i in range(N):
        if TAG[i] not in ('B', 'C') or S[i] < sOut: continue
        a = math.atan2(Z[i] - CZ, X[i] - CX) % (2 * math.pi)
        d = abs((a - phi + math.pi) % (2 * math.pi) - math.pi)
        if best is None or d < best[0]: best = (d, FY[i])
    return best[1]

def island_ring(phi):
    deg = math.degrees(phi) % 360
    if deg < 40: f = H
    elif deg < 75: f = H + (FL - H) * sstep((deg - 40) / 35)
    elif deg < 100: f = FL
    else: f = path_floor_at_angle(phi)
    if deg < 95: Yc = H + 0.055
    elif deg < 180: Yc = keys(deg, [(95, H + 0.055), (180, path_floor_at_angle(math.pi) + 0.055)])
    else: Yc = f + 0.055
    c, s = math.cos(phi), math.sin(phi)
    pr = [(0.0, PIT), (0.03, PIT)]
    for w in (1.0, 0.85, 0.7, 0.5, 0.3, 0.15): pr.append((ISL_CREST - 0.035 * w, ridge(PIT, Yc, w)))
    pr.append((ISL_CREST, Yc))
    for w in (0.15, 0.3, 0.5, 0.7, 0.85, 1.0): pr.append((ISL_CREST + (ISL_FOOT - ISL_CREST) * w, ridge(f, Yc, w)))
    pr.append((ISL_FOOT + 0.02, f - 0.006))
    return [(CX + r * c, y, CZ + r * s) for r, y in pr]

NPHI = 120
M.skin([island_ring(2 * math.pi * k / NPHI) for k in range(NPHI)], 'W', closed=True)

# ------------------------------------------------------------------ inner wall of the tunnel (tee side), a closed low block
iT = [i for i in range(N) if TAG[i] == 'B' and sMouth - 0.04 <= S[i] <= sFree + 0.10]
def tunnel_wall(i):
    s = S[i]; f = FY[i]
    T = keys(s, [(sOut - 0.02, 0.061), (sFree + 0.10, f + 0.001)])     # full height from its start inside the tub wall: a clean jamb
    o = keys(s, [(sOut - 0.04, 0.066), (sFree + 0.10, 0.10)])
    back = o + 0.07
    pr = [(-back, 0.0), (-back, T), (-(o + 0.02), T), (-(o + 0.008), T - 0.003)]
    for u in (1.0, 0.8, 0.55, 0.3, 0.12): pr.append((-(o + 0.008 * u), f + (T - 0.003 - f) * u * u))
    pr += [(-o, f), (-(o - 0.02), f - 0.006)]
    return [at(i, a, y) for a, y in pr]

M.skin([tunnel_wall(i) for i in iT], 'W')

# ------------------------------------------------------------------ floor of the channel
def floor_ring(i):
    s = S[i]; f = FY[i]; x, z = X[i], Z[i]
    right = at(i, foot_right(s) + 0.016, f)
    if TAG[i] == 'A':
        left = (0.17 + (0.255 - 0.17) * (z - Z0) / (EZ - Z0), f, z)     # diagonal edge of the entry sheet (norm drawing)
    elif TAG[i] == 'D':
        left = at(i, -(XE - CX - ISL_FOOT + 0.02), f)
    else:
        dx, dz = x - CX, z - CZ; d = math.hypot(dx, dz)
        radial = (CX + dx / d * 0.112, f, CZ + dz / d * 0.112)          # under the island's foot
        if TAG[i] == 'C': left = radial
        else:
            a = keys(s, [(sB, 0.0), (sMouth - 0.10, 1.0)])
            tun = at(i, -0.085, f)                                       # under the tunnel wall
            p0 = ((1 - a) * 0.255 + a * tun[0], f, (1 - a) * z + a * tun[2])
            b = keys(s, [(sOut, 0.0), (sFree + 0.10, 1.0)])
            left = tuple((1 - b) * p0[k] + b * radial[k] for k in range(3))
    L3 = np.array(left); R3 = np.array(right)
    return [tuple(L3 + (R3 - L3) * u) for u in (0.0, 0.33, 0.67, 1.0)]

M.skin([floor_ring(i) for i in range(N)], 'F')

# ------------------------------------------------------------------ underside of the bridge deck (seen only from inside the tunnel)
yb = H - DECK
M.skin([[(CX + ISL_CREST - 0.02, yb, z), (XE + 0.155, yb, z)] for z in (ZT0 - 0.004, ZT1 + 0.004)], 'U', flip=True)

here = os.path.dirname(os.path.abspath(__file__))
preview = len(sys.argv) > 1
M.write(sys.argv[1] if preview else os.path.join(here, '..', 'lanes', 'eternit_05_liegende_schleife.obj'))

red = [0.80, 0.13, 0.12]
# repositioning line (norm drawing): 30 cm behind the exit ramp, 20 cm from the Banden, running into the circle line
_hw = 0.25; _zj = 5.55 - math.sqrt(0.25 - _hw * _hw); _a0 = math.atan2(_zj - 5.55, _hw)
ablege = [[-_hw, round(_zj, 3)], [-_hw, round(ZR + 0.30, 3)], [_hw, round(ZR + 0.30, 3)], [_hw, round(_zj, 3)]] +          [[round(0.5 * math.cos(_a0 + (math.pi - 2 * _a0) * k / 40), 3), round(5.55 + 0.5 * math.sin(_a0 + (math.pi - 2 * _a0) * k / 40), 3)] for k in range(1, 41)]
lane = {"name": "Eternit 05 Liegende Schleife", "system": "Eternit", "length": 6.25, "width": 0.90, "targetCircle": 1.40,
        "tee": [0, 0.20], "cup": [0, 5.55],
        "parts": [
            {"name": "Schleife", "shape": "mesh", "file": "eternit_05_liegende_schleife.obj", "slick": True, "color": red},
            {"name": "Grenzlinie", "shape": "line", "points": [[round(XE - 0.065, 3), ZT1], [round(XE + 0.065, 3), ZT1]],
             "pos": [0, H + 0.001, 0], "thickness": 0.01, "color": [0.80, 0.08, 0.08]},
            {"name": "Ablegelinie", "shape": "line", "points": ablege, "thickness": 0.006, "color": [0.13, 0.13, 0.14]}]}
lane["circleLine"] = False
hdr = """// Eternit lane 5 - Liegende Schleife.
// OFFICIAL (NormMiniatur 01/2026): the obstacle starts 200-300 cm from the lane start (using 250); the ball must run
//   through the entrance and the whole channel of the loop up to the Grenzlinie; mirrored versions are allowed.
// MEASURED FROM THE NORM DRAWING (plan view, lane width as scale): the moulding spans the full lane width and about
//   0.9 m of its length; entry along the player's right Bande; the channel turns left around the target side, comes
//   back along the player's left and the tee side around an island, and leaves straight down the lane slightly right
//   of the centre, crossing over the entry; the Grenzlinie is at the end of that bridge.
// FROM PHOTOS (Vorsfelde video frames, minigolfbau.eu product photo, Springe - estimates): one-piece moulding like a
//   tub; high outer wall (using 33 cm on the tee side; photo estimates range from 25 to 40), the entry passes through a low tunnel under the exit channel,
//   island = rounded ridge around a pit whose arm continues as the left wall of the exit ramp.
// MY ESTIMATES: all heights (bridge floor 12.5 cm, walls), the climb distribution and the ramp length.
// Generated by tools/gen_liegende_schleife.py - edit the generator, not this file.
"""
if not preview:
    open(os.path.join(here, '..', 'lanes', 'eternit_05_liegende_schleife.json'), 'w', encoding='utf-8').write(hdr + json.dumps(lane, indent=1) + '\n')
print('verts', len(M.v), 'tris', len(M.f), 'stations', N, 'sMouth %.2f sOut %.2f sC %.2f sD %.2f sEnd %.2f' % (sMouth, sOut, sC, sD, sEnd))
