import sys, math, json
import numpy as np
from PIL import Image, ImageDraw
from pal import MAT, LETTER, OUTLINE
import front as FR, body as BD
from view import sprite

def scale2x(a):
    h, w = a.shape[:2]
    p = np.pad(a, ((1, 1), (1, 1), (0, 0)), mode='edge')
    A = p[:-2, 1:-1]; B = p[1:-1, 2:]; C = p[1:-1, :-2]; D = p[2:, 1:-1]; E = p[1:-1, 1:-1]
    eq = lambda x, y: np.all(x == y, axis=-1)
    o = np.zeros((h * 2, w * 2, a.shape[2]), a.dtype)
    c1 = eq(C, A) & ~eq(C, D) & ~eq(A, B); c2 = eq(A, B) & ~eq(A, C) & ~eq(B, D)
    c3 = eq(D, C) & ~eq(D, B) & ~eq(C, A); c4 = eq(B, D) & ~eq(B, A) & ~eq(D, C)
    o[0::2, 0::2] = np.where(c1[..., None], A, E); o[0::2, 1::2] = np.where(c2[..., None], B, E)
    o[1::2, 0::2] = np.where(c3[..., None], C, E); o[1::2, 1::2] = np.where(c4[..., None], D, E)
    return o

class Part:
    def __init__(self, im):
        self.im = im
        a = np.array(im)
        for _ in range(3): a = scale2x(a)
        self.big = Image.fromarray(a)

def rotsprite(part, piv, ang):
    K = 8
    big = part.big
    pad = int(max(big.size) * 1.5) + 8
    cv = Image.new('RGBA', (pad * 2, pad * 2))
    px, py = piv[0] * K + K / 2, piv[1] * K + K / 2
    cv.alpha_composite(big, (int(pad - px), int(pad - py)))
    r = cv.rotate(ang, resample=Image.NEAREST, center=(pad, pad))
    return r, pad

def place(canvas, part, piv, pos, ang):
    if abs(ang) < 2:
        canvas.alpha_composite(part.im, (int(round(pos[0] - piv[0] - 0.5)), int(round(pos[1] - piv[1] - 0.5))))
        return
    K = 8
    r, pad = rotsprite(part, piv, ang)
    ox = pos[0] * K - pad; oy = pos[1] * K - pad
    sx = int(math.floor(ox)) % K
    off = (int(round(ox)) % K, int(round(oy)) % K)
    big = Image.new('RGBA', (canvas.width * K, canvas.height * K))
    big.alpha_composite(r, (int(round(ox)), int(round(oy))))
    small = big.resize(canvas.size, Image.NEAREST, reducing_gap=None) if False else Image.fromarray(np.array(big)[K // 2::K, K // 2::K])
    canvas.alpha_composite(small)

def rot(v, a):
    r = math.radians(a); x, y = v
    return (x * math.cos(r) + y * math.sin(r), -x * math.sin(r) + y * math.cos(r))

def ang_of(v): return math.degrees(math.atan2(v[0], v[1]))

WEAP = {'sword': ('SWORD', (4, 20)), 'axe': ('AXE', (6, 15)), 'staff': ('STAFF', (4, 11))}

class Doll:
    def __init__(self, look):
        pal = {k: [tuple(c) for c in v] for k, v in look.get('pal', {}).items()}
        dark = {k: [tuple(int(x * 0.9) for x in c) for c in (pal.get(k) or MAT[k])] for k in MAT}
        soft = str.maketrans({})
        mk = lambda txt, d=False, e=True: Part(sprite(txt if e else txt.translate(soft), dark if d else pal))
        self.cols = {k: (pal.get(k) or MAT[k]) for k in MAT}; self.dcols = dark
        self.p = {'torso': mk(FR.TORSO, e=False), 'pelvis': mk(FR.PELVIS, e=False),
                  'head': mk(FR.HEAD_F if look.get('female') else FR.HEAD_M),
                  'uF': mk(FR.UARM, e=False), 'fF': mk(FR.FARM, e=False), 'hF': mk(FR.HAND, e=False),
                  'uB': mk(FR.UARM, True, False), 'fB': mk(FR.FARM, True, False), 'hB': mk(FR.HAND, True, False),
                  'thF': mk(FR.THIGH, e=False), 'shF': mk(FR.SHIN, e=False), 'ftF': mk(FR.FOOT, e=False),
                  'thB': mk(FR.THIGH, True, False), 'shB': mk(FR.SHIN, True, False), 'ftB': mk(FR.FOOT, True, False)}
        self.p['torso_side'] = mk(FR.TORSO_SIDE, e=False); self.p['torso_back'] = mk(FR.TORSO_BACK, e=False)
        self.p['uF_short'] = mk(FR.UARM_SHORT, e=False)
        self.p['hairfly'] = mk(FR.HAIR_F_FLY if look.get('female') else FR.HAIR_M_FLY)
        self.p['coat'] = mk(FR.COAT_TAIL, True)
        wn, self.grip = WEAP[look.get('weapon', 'sword')]
        self.p['w'] = mk(getattr(BD, wn))
        self.wlen = self.p['w'].im.height
        g = (pal.get('gem') or MAT['gem']); self.burst = (g[2] + (255,), g[3] + (255,))
        self.kind = look.get('weapon', 'sword')
        self.slash = [tuple(c) for c in SLASH[look.get('slash', 'steel')]]

    def reach(self, hip, ank, L=15.9):
        dx, dy = ank[0] - hip[0], ank[1] - hip[1]; d = math.hypot(dx, dy)
        if d <= L: return ank
        return (hip[0] + dx * L / d, hip[1] + dy * L / d)

    def ik(self, hip, ank, L1=8.0, L2=8.0):
        dx, dy = ank[0] - hip[0], ank[1] - hip[1]; d = max(1e-3, min(math.hypot(dx, dy), L1 + L2 - 0.05))
        base = math.atan2(dy, dx)
        k = math.acos(max(-1, min(1, (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d))))
        a = base - k
        knee = (hip[0] + L1 * math.cos(a), hip[1] + L1 * math.sin(a))
        return knee

    def frame(self, P, size=(64, 64), root=(30, 42), ground=62):
        c = Image.new('RGBA', size)
        hip = (root[0] + P.get('dx', 0), root[1] + P.get('dy', 0))
        tor = P.get('torso', 0)
        def at(base, ang, piv, att): 
            d = rot((att[0] - piv[0], att[1] - piv[1]), ang); return (base[0] + d[0], base[1] + d[1])
        pel_piv = (5, 0)
        tor_piv = (5, 13); neck = at(hip, tor, tor_piv, (5, 0))
        tv = P.get('tv', 'front')
        sx = {'front': (-1.3, 11.3), 'side': (5.0, 7.0), 'back': (0.5, 9.0)}[tv]
        shF = at(hip, tor, tor_piv, (sx[0], 1.5)); shB = at(hip, tor, tor_piv, (sx[1], 1.5))
        hipF = at(hip, 0, pel_piv, (7.5, 1)); hipB = at(hip, 0, pel_piv, (2, 1))
        legs = {}
        for s, hp_, fx in (('F', hipF, P.get('footF', 2.5)), ('B', hipB, P.get('footB', -3))):
            ank = (root[0] + fx, ground - 3 - P.get('lift', 0) - P.get('lift' + s, 0))
            ank = self.reach(hp_, ank)
            knee = self.ik(hp_, ank)
            legs[s] = (hp_, knee, ank)
        arms = {}
        for s, sh in (('F', shF), ('B', shB)):
            el = 4 if (s == 'F' and P.get('uv') == 'short') else 7
            a1 = tor + P.get('u' + s, 0); elb = at(sh, a1, (1, 0), (1, el))
            a2 = a1 + P.get('f' + s, 0); wr = at(elb, a2, (1, 0), (1, 4))
            arms[s] = (sh, a1, elb, a2, wr)
        def disc(cv, pos, col, r=1.6):
            ImageDraw.Draw(cv).ellipse([pos[0] - r + 0.5, pos[1] - r + 0.5, pos[0] + r - 0.5, pos[1] + r - 0.5], fill=tuple(col) + (255,))
        def draw_leg(cv, s):
            hp_, knee, ank = legs[s]
            ta = ang_of((knee[0] - hp_[0], knee[1] - hp_[1])); sa = ang_of((ank[0] - knee[0], ank[1] - knee[1]))
            place(cv, self.p['th' + s], (2, 0), hp_, ta)
            place(cv, self.p['sh' + s], (2, 0), knee, sa)
            place(cv, self.p['ft' + s], (1, 0), (ank[0], ank[1] + 0.5), 0)
        def draw_arm(cv, s, weapon=False):
            sh, a1, elb, a2, wr = arms[s]
            if weapon:
                place(cv, self.p['w'], self.grip, wr, P.get('sw', 0))
            place(cv, self.p['uF_short' if (s == 'F' and P.get('uv') == 'short') else 'u' + s], (1, 0), sh, a1)
            disc(cv, elb, (self.cols if s == 'F' else self.dcols)['tunic'][1], 1.2)
            place(cv, self.p['f' + s], (1, 0), elb, a2)
            place(cv, self.p['h' + s], (1, 1), wr, a2)
        def outlined(cv):
            a = np.array(cv); al = a[..., 3] > 0
            p = np.pad(al, 1); ring = (p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]) & ~al
            a[ring] = OUTLINE + (255,); a[..., 3] = np.where(a[..., 3] > 0, 255, 0)
            return Image.fromarray(a)
        smear = P.get('_smear')
        back = Image.new('RGBA', size); draw_leg(back, 'B'); draw_arm(back, 'B')
        vx, vy = P.get('_vel', (0, 0)); ph = P.get('_phase', 0)
        sway = math.sin(ph) * 4
        coat_a = -max(0, min(65, vx * 6)) + sway + max(-25, min(25, vy * 5))
        place(back, self.p['coat'], (2, 0), at(hip, tor, tor_piv, (0.5, 10)), coat_a + tor * 0.5)
        hair_a = 75 - max(0, min(75, vx * 7)) + sway * 1.5 + max(-30, min(30, vy * 6))
        hang = round(tor * 0.85 + P.get('head', 0)); hb = at(neck, hang, (9, 17), (3, 9))
        place(back, self.p['hairfly'], (self.p['hairfly'].im.width - 1, 1), hb, hair_a + hang)
        body = Image.new('RGBA', size)
        place(body, self.p['pelvis'], pel_piv, hip, 0)
        draw_leg(body, 'F')
        place(body, self.p['torso' if tv == 'front' else 'torso_' + tv], tor_piv, hip, tor)
        place(body, self.p['head'], (9, 17), neck, round(tor * 0.85 + P.get('head', 0)))
        front = Image.new('RGBA', size); draw_arm(front, 'F', True)
        def over(base, layer):
            b = np.array(base); l = np.array(layer); al = l[..., 3] > 0
            p = np.pad(al, 1); ring = (p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]) & ~al
            inner = ring & (b[..., 3] > 0)
            b[al] = l[al]
            b[ring & ~inner] = OUTLINE + (255,)
            b[inner, :3] = np.array(OUTLINE, np.uint8)
            return Image.fromarray(b)
        c = over(Image.new('RGBA', size), back)
        if P.get('_fxb'): P['_fxb'](c, hip)
        if smear: self.draw_slash(c, *smear)
        c = over(c, body)
        c = over(c, front)
        if P.get('_fxf'): P['_fxf'](c, hip)
        if P.get('_burst'):
            sh, a1, elb, a2, wr = arms['F']; tip = at2(wr, P.get('sw', 0), self.grip, (4, 2)); r = P['_burst']
            d = ImageDraw.Draw(c); col = self.burst
            d.ellipse([tip[0] - r, tip[1] - r, tip[0] + r, tip[1] + r], outline=col[0], width=1)
            if r < 6: d.ellipse([tip[0] - r / 2, tip[1] - r / 2, tip[0] + r / 2, tip[1] + r / 2], fill=col[1])
        a = np.array(c)
        self._wr = arms['F'][4]
        return Image.fromarray(a)

    def blade(self, P, root=(30, 42)):
        self.frame(dict(P, _smear=None), root=root)
        return self._wr, P.get('sw', 0)

    def draw_slash(self, c, path, fade=0.0):
        R = self.grip[1] + 0.5
        I = np.zeros((c.height, c.width))
        n = len(path) - 1
        for i in range(n * 12 + 1):
            u = i / 12; k = min(int(u), n - 1); f = u - k
            (wa, aa), (wb, ab) = path[k], path[k + 1]
            t = u / n
            if t < fade * 0.6: continue
            w = (wa[0] + (wb[0] - wa[0]) * f, wa[1] + (wb[1] - wa[1]) * f)
            d = rot((0, -1), aa + (ab - aa) * f)
            lo = 1 - (0.08 + 0.42 * t ** 0.9) * (1 - fade * 0.75)
            for j in range(40):
                rr = lo + (1 + 1.5 / R - lo) * j / 39
                v = t ** 1.1 * (0.3 + 0.7 * j / 39) * (1 - fade * 0.55)
                x, y = int(round(w[0] + d[0] * rr * R)), int(round(w[1] + d[1] * rr * R))
                if 0 <= x < c.width and 0 <= y < c.height: I[y, x] = max(I[y, x], v)
        a = np.array(c); cols = self.slash
        for lim, col in zip((0.08, 0.32, 0.56, 0.8), cols):
            m = I > lim; a[m, :3] = col; a[m, 3] = 255
        wl, al = path[-1]; d = rot((0, -1), al); tip = (wl[0] + d[0] * (R + 2), wl[1] + d[1] * (R + 2))
        rng = np.random.default_rng(int(fade * 10) + 7)
        if fade == 0:
            self.spark(a, tip, 2, cols)
        else:
            for _ in range(3):
                t = rng.uniform(fade, 1); k = min(int(t * n), n - 1); f = t * n - k
                (wa, aa), (wb, ab) = path[k], path[k + 1]
                w = (wa[0] + (wb[0] - wa[0]) * f, wa[1] + (wb[1] - wa[1]) * f)
                d = rot((0, -1), aa + (ab - aa) * f); s = R + 1 + fade * 4
                self.spark(a, (w[0] + d[0] * s, w[1] + d[1] * s), 1, cols)
        c.paste(Image.fromarray(a))

    def spark(self, a, p, r, cols):
        x, y = int(round(p[0])), int(round(p[1]))
        pts = [(0, 0, 3)] + [(dx * i, dy * i, 2 if i < r else 1) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) for i in range(1, r + 1)]
        for dx, dy, ci in pts:
            if 0 <= x + dx < a.shape[1] and 0 <= y + dy < a.shape[0]:
                a[y + dy, x + dx, :3] = cols[ci]; a[y + dy, x + dx, 3] = 255

def at2(base, ang, piv, att):
    d = rot((att[0] - piv[0], att[1] - piv[1]), ang); return (base[0] + d[0], base[1] + d[1])

def ease_io(t): return t * t * (3 - 2 * t)
def ease_out(t): return 1 - (1 - t) ** 3
def ease_in(t): return t ** 2
def lerp(p, q, t):
    out = {}
    for k in set(p) | set(q):
        if k.startswith('_'): continue
        a, b = p.get(k), q.get(k)
        if isinstance(a, str) or isinstance(b, str): out[k] = (b if t >= 0.5 else a) or 'front'
        else: out[k] = (a or 0) + ((b or 0) - (a or 0)) * t
    return out

IDLE = {'torso': 0, 'uF': 0, 'fF': 4, 'sw': -140, 'uB': 0, 'fB': 2, 'footF': 2.5, 'footB': -3}
BREATH = dict(IDLE, dy=0.6, uF=1, fF=6, sw=-137, head=2)
HIGH = dict(IDLE, dy=1.5, torso=8, head=-4, uF=198, fF=22, sw=46, uB=-30, fB=40, footF=2)
MID = dict(IDLE, dy=1, dx=2, torso=-6, head=2, uF=120, fF=5, sw=-20, uB=-45, fB=30, footF=5)
LOW = dict(IDLE, dx=4, dy=2.5, torso=-20, head=8, uF=55, fF=0, sw=-105, uB=-60, fB=30, footF=8, footB=-3)
LOWSET = dict(LOW, dx=3, dy=2, torso=-15, head=6, uF=50, fF=8, sw=-118, uB=-50)
DIP = dict(LOWSET, dy=2.5, torso=-17, uF=42, fF=12, sw=-128)
UPMID = dict(IDLE, dx=3, dy=1, torso=-2, head=0, uF=128, fF=8, sw=-8, uB=-40, fB=35, footF=6)
UP = dict(IDLE, dx=1, dy=0.5, torso=10, head=-6, uF=205, fF=18, sw=52, uB=-25, fB=45, footF=3)

BASE_IDLE, BASE_BREATH = IDLE, BREATH

def idle_seq(n=10):
    return [lerp(IDLE, BREATH, (1 - math.cos(2 * math.pi * i / n)) / 2) for i in range(n)]

def swing(a, b, t1=1.0, k=8):
    return ('swing', a, b, t1, k)

def enter_seq():
    return [lerp(IDLE, HIGH, ease_io(i / 3)) for i in (1, 2)] + [HIGH]

def slash_a():
    return [dict(MID, _sw=swing(HIGH, MID)), dict(LOW, _sw=swing(HIGH, LOW)),
            dict(LOW, _sw=swing(HIGH, LOW), _fade=0.45), dict(LOWSET, _sw=swing(HIGH, LOW), _fade=0.8), DIP]

def slash_b():
    return [dict(UPMID, _sw=swing(DIP, UPMID)), dict(UP, _sw=swing(DIP, UP)),
            dict(UP, _sw=swing(DIP, UP), _fade=0.45), dict(lerp(UP, HIGH, 0.5), _sw=swing(DIP, UP), _fade=0.8), HIGH]

def exit_seq(src):
    return [lerp(src, IDLE, ease_io(i / 4)) for i in range(1, 5)]

CA1 = dict(IDLE, dy=1, torso=6, head=-3, uF=190, fF=20, sw=30, uB=-20, fB=50)
CA2 = dict(IDLE, dx=2, dy=1.5, torso=-12, head=6, uF=80, fF=-5, sw=-75, uB=-50, fB=30, footF=8)
CA2SET = dict(CA2, dx=1.5, torso=-9, uF=70, fF=0, sw=-85)
def cast_enter():
    return [lerp(IDLE, CA1, ease_io(i / 3)) for i in (1, 2)] + [CA1]
def cast_a():
    return [dict(lerp(CA1, CA2, 0.6), _sw=swing(CA1, lerp(CA1, CA2, 0.6))), dict(CA2, _burst=3, _sw=swing(CA1, CA2)),
            dict(CA2, _burst=5, _sw=swing(CA1, CA2), _fade=0.45), dict(CA2SET, _burst=7, _sw=swing(CA1, CA2), _fade=0.8), CA2SET]
def cast_b():
    return [dict(lerp(CA2SET, CA1, 0.5), _sw=swing(CA2SET, lerp(CA2SET, CA1, 0.5))), dict(CA1, _sw=swing(CA2SET, CA1)),
            dict(CA1, _sw=swing(CA2SET, CA1), _fade=0.45), dict(CA1, _sw=swing(CA2SET, CA1), _fade=0.8), CA1]

def render_seq(doll, seq):
    frames = []
    for P in seq:
        if '_sw' in P:
            _, a, b, t1, k = P['_sw']
            path = [doll.blade(lerp(a, b, t1 * i / k)) for i in range(k + 1)]
            P = dict(P, _smear=(path, P.get('_fade', 0.0)))
        frames.append(doll.frame(P))
    return frames

SLASH = json.load(open(__import__('os').path.join(__import__('os').path.dirname(__file__), 'slashes.json')))

if __name__ == '__main__':
    looks = json.load(open(sys.argv[1])); out = sys.argv[2]
    import os
    for key, look in looks.items():
        d = Doll(look); os.makedirs(f'{out}/{key}', exist_ok=True)
        g = globals(); g['IDLE'], g['BREATH'] = BASE_IDLE, BASE_BREATH
        staff = d.kind == 'staff'
        if staff:
            g['IDLE'] = dict(BASE_IDLE, sw=-18, fF=10); g['BREATH'] = dict(BASE_BREATH, sw=-16, fF=12)
        seqs = {'Idle': idle_seq()}
        if staff:
            seqs.update(Enter=cast_enter(), SlashA=cast_a(), SlashB=cast_b(), ExitA=exit_seq(CA2SET), ExitB=exit_seq(CA1))
        else:
            seqs.update(Enter=enter_seq(), SlashA=slash_a(), SlashB=slash_b(), ExitA=exit_seq(DIP), ExitB=exit_seq(HIGH))
        for name, seq in seqs.items():
            for i, f in enumerate(render_seq(d, seq)): f.save(f'{out}/{key}/{name}_{i:02d}.png')
