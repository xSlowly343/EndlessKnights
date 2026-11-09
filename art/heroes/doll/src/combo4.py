import sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image, ImageDraw
import rig3 as R
import fx
from pal import OUTLINE
from rig3 import lerp, ease_io
import combo3 as C

SIZE = (96, 64)
IDLE = R.BASE_IDLE
IDLE_W = dict(IDLE, uF=720)
DUST = [(120, 100, 80), (176, 158, 130), (214, 200, 172), (240, 232, 214)]

W1A = dict(C.W1, dx=0, dy=3, torso=10, head=-4, uF=660, sw=80, footF=7, footB=-6)
W1 = dict(C.W1, dx=-2, dy=4, torso=18, head=-9, uB=40, fB=20, footF=5, footB=-9, liftF=3)
S1 = dict(C.S1, dx=12, dy=5.5, torso=-30, head=14, uB=-85, footF=23, footB=1)
S1B = dict(C.S1B, dx=13, dy=6, torso=-32, head=15, uB=-90, footF=23, footB=1)
S1S = dict(C.S1S, dx=12, dy=5.5, torso=-26, head=12, uB=-75, footF=23, footB=1)
S1R = dict(S1S, dx=11, dy=4.5, torso=-18, head=8, uF=805, sw=-100, uB=-50, footF=22, footB=2)

W2A = dict(C.W2, dx=11, dy=4, torso=2, head=-2, footF=22, footB=3, uF=880, sw=10, uB=-20)
W2 = dict(C.W2, dx=9, dy=4, torso=14, head=-8, uB=35, fB=25, footF=21, footB=2, liftF=2)
S2 = dict(C.S2, dx=18, dy=6.5, torso=-34, head=14, uB=-90, footF=29, footB=6)
S2B = dict(C.S2B, dx=18.5, dy=7, torso=-36, head=15, uB=-95, footF=29, footB=6)
S2S = dict(C.S2S, dx=17, dy=6, torso=-26, head=10, uB=-70, footF=29, footB=6)
S2R = dict(S2S, dx=16, dy=5.5, torso=-18, head=6, footF=28, footB=6)

W3A = dict(C.W3, dx=15, dy=7.5, torso=-20, head=6, footF=26, footB=6, uB=-30)
W3 = dict(C.W3, dx=14, dy=9, torso=-12, head=4, footF=24, footB=6, uF=785, sw=-125, uB=-50)
S3 = dict(C.S3, dx=12, dy=-6, lift=8, liftF=5, torso=12, head=-8, uB=-80, footF=19, footB=7)
S3B = dict(C.S3B, dx=10, dy=-10, lift=12, liftF=6, torso=18, head=-10, uB=-90, footF=17, footB=5)
S3S = dict(C.S3S, dx=7, dy=-5, lift=7, liftF=3, torso=14, head=-6, uB=-60, footF=12, footB=1)
S3L = dict(C.S3S, dx=4, dy=6.5, lift=0, torso=-10, head=6, uB=-40, footF=14, footB=-6)
S3R = dict(C.S3S, dx=2, dy=3.5, lift=0, torso=6, head=-4, uF=925, uB=-20, footF=10, footB=-5)

for _p, _v in ((W1A, dict(tv='back', uv='short')), (W1, dict(tv='back', uv='short')), (S1, dict(tv='side')), (S1B, dict(tv='side')),
               (S1S, dict(tv='side')), (W2, dict(tv='back')), (S2, dict(tv='side')), (S2B, dict(tv='side')), (S2S, dict(tv='side')),
               (W3, dict(tv='side')), (S3L, dict(tv='side'))):
    _p.update(_v)
END = dict(S3R, uF=565)

def tail_hook(doll, vel, phase):
    cols = doll.cols['scarf']
    def run(c, hip, P):
        tor = P.get('torso', 0)
        n = R.rot((0, -12), tor); start = (hip[0] + n[0] - 1.5, hip[1] + n[1] + 1)
        vx, vy = vel
        pts = [start]; d = (-0.35, 1.0)
        for i in range(4):
            drag = (-vx * (0.18 + 0.1 * i), -vy * 0.12 + math.sin(phase + i * 1.3) * 0.12)
            v = (d[0] + drag[0], d[1] + drag[1]); L = math.hypot(*v) or 1
            v = (v[0] / L, v[1] / L)
            p = pts[-1]; pts.append((p[0] + v[0] * 3.2, p[1] + v[1] * 3.2))
        dr = ImageDraw.Draw(c)
        dr.line(pts, fill=OUTLINE + (255,), width=4)
        dr.line(pts, fill=tuple(cols[1]) + (255,), width=2)
        dr.line(pts[:3], fill=tuple(cols[2]) + (255,), width=1)
    return run

def dust(c, x, y, stage):
    dr = ImageDraw.Draw(c)
    puffs = [(-4, 0, 1.6), (-8, -0.5, 1.2), (3, 0, 1.2)]
    k = (0.8, 1.2, 1.5)[stage]; col = DUST[3 - stage] if stage < 3 else DUST[1]
    for px, py, r in puffs:
        R_ = r * k; cx = x + px * (0.8 + 0.4 * stage); cy = y + py - stage * 0.8
        if stage == 2:
            dr.point([(cx, cy - 1), (cx + 2, cy)], fill=DUST[1] + (255,)); continue
        dr.ellipse([cx - R_ - 1, cy - R_ - 1, cx + R_ + 1, cy + R_ + 1], fill=DUST[0] + (255,))
        dr.ellipse([cx - R_, cy - R_, cx + R_, cy + R_], fill=col + (255,))

def ghost(img, col, alpha_step=1):
    a = np.array(img); h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    m = (a[..., 3] > 0) & ((xx + yy) % 2 == 0)
    g = np.zeros_like(a); g[m, :3] = col; g[m, 3] = 255
    return Image.fromarray(g)

def strip_fx(P):
    return {k: v for k, v in P.items() if not k.startswith('_')}

def build(doll):
    cols = doll.slash
    def H(P, spec, stage, imp=None, front=False):
        return C.fx_pose(P, spec, stage, cols, front, imp)
    hit1 = [(W1A, 60), (W1, 70),
            (dict(H(S1, C.HIT1, 0, 0), _ghost=W1, _dust=0), 30), (dict(H(S1B, C.HIT1, 1, 1), _dust=1), 40),
            (dict(H(S1S, C.HIT1, 2, 2), _dust=2), 50), (H(S1S, C.HIT1, 3), 60), (S1R, 50)]
    hit2 = [(W2A, 50), (W2, 70),
            (dict(H(S2, C.HIT2, 0, 0), _ghost=W2, _dust=0), 30), (dict(H(S2B, C.HIT2, 1, 1), _dust=1), 40),
            (dict(H(S2S, C.HIT2, 2, 2), _dust=2), 50), (H(S2S, C.HIT2, 3), 60), (S2R, 50)]
    hit3 = [(W3A, 50), (W3, 80),
            (dict(H(S3, C.HIT3, 0, 0), _ghost=W3, _dust=0), 30), (H(S3B, C.HIT3, 1, 1), 40),
            (H(S3S, C.HIT3, 2, 2), 50), (dict(H(S3L, C.HIT3, 3), _dust=0), 50), (dict(S3L, _dust=1), 50), (dict(S3R, _dust=2), 60)]
    enter = [(lerp(IDLE, dict(W1, uF=-40, sw=-260), 0.5), 60), (dict(W1, uF=-40, sw=-260), 60)]
    exits = {}
    for n, src in (('Exit1', S1R), ('Exit2', S2R), ('Exit3', S3R)):
        exits[n] = [(lerp(src, IDLE_W, ease_io(i / 4)), 70) for i in range(1, 5)]
    return {'Enter': enter, 'Hit1': hit1, 'Hit2': hit2, 'Hit3': hit3, **exits}

def render(doll, seq, prev_pose, phase0=0):
    out = []; prev = prev_pose; mom = (0.0, 0.0)
    for i, (P, ms) in enumerate(seq):
        raw = (P.get('dx', 0) - prev.get('dx', 0), P.get('dy', 0) - prev.get('dy', 0))
        mom = tuple(r if abs(r) > abs(m * 0.7) else m * 0.7 for r, m in zip(raw, mom))
        vel = mom
        tail = tail_hook(doll, vel, phase0 + i * 0.9)
        fxb = P.get('_fxb')
        def back(c, hip, P=P, fxb=fxb, tail=tail):
            tail(c, hip, P)
            if fxb: fxb(c, hip)
        Q = dict(P, _fxb=back, _vel=vel, _phase=phase0 + i * 0.9)
        img = doll.frame(Q, size=SIZE)
        if P.get('_ghost') is not None:
            mid = lerp(P['_ghost'], strip_fx(P), 0.45)
            g2 = ghost(doll.frame(mid, size=SIZE), doll.slash[2])
            base = Image.new('RGBA', SIZE); base.alpha_composite(g2); base.alpha_composite(img); img = base
        if '_dust' in P:
            x = 30 + P.get('footF', 2.5) - 2; dust(img, x, 61, P['_dust'])
        out.append((img, ms)); prev = strip_fx(P)
    return out

def idle_frames(doll):
    seq = R.idle_seq()
    return [(doll.frame(dict(P, _phase=i * 0.63, _fxb=lambda c, h, P=P, i=i: tail_hook(doll, (0, 0), i * 0.63)(c, h, P)), size=SIZE), 90) for i, P in enumerate(seq)]

if __name__ == '__main__':
    looks = json.load(open(sys.argv[1])); out = sys.argv[2]
    timing = {}
    for key, look in looks.items():
        d = R.Doll(look)
        if d.kind == 'staff': continue
        os.makedirs(f'{out}/{key}', exist_ok=True)
        seqs = build(d)
        prevs = {'Enter': IDLE, 'Hit1': END, 'Hit2': S1R, 'Hit3': S2R, 'Exit1': S1R, 'Exit2': S2R, 'Exit3': S3R}
        rendered = {'Idle': idle_frames(d)}
        for n, seq in seqs.items(): rendered[n] = render(d, seq, prevs[n])
        for n, fr in rendered.items():
            timing[n] = [ms for _, ms in fr]
            for i, (im, ms) in enumerate(fr): im.save(f'{out}/{key}/{n}_{i:02d}.png')
    json.dump(timing, open(f'{out}/timing.json', 'w'), indent=1)
