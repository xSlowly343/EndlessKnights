import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig3 as R
import fx
from rig3 import lerp, ease_io, ease_out

SIZE = (96, 64)
IDLE = R.BASE_IDLE
IDLE_W = dict(IDLE, uF=720)

W1 = dict(IDLE, dx=-1, dy=2, torso=8, head=-4, uF=680, fF=10, sw=100, uB=25, fB=30, footF=2, footB=-4)
S1 = dict(IDLE, dx=6, dy=3, torso=-22, head=10, uF=815, fF=0, sw=-90, uB=-70, fB=20, footF=10, footB=-5)
S1B = dict(S1, dx=6.5, torso=-24, uF=818, sw=-88)
S1S = dict(S1, dx=5, dy=2.5, torso=-18, head=8, uF=810, fF=6, sw=-96, uB=-55)

W2 = dict(IDLE, dx=4, dy=1.5, torso=4, head=-3, uF=918, fF=22, sw=46, uB=-30, fB=40, footF=8, footB=-5)
S2 = dict(IDLE, dx=7, dy=3.5, torso=-22, head=8, uF=775, fF=0, sw=-105, uB=-65, fB=30, footF=11, footB=-4)
S2B = dict(S2, dx=7.5, dy=4, torso=-24, uF=770, sw=-110)
S2S = dict(S2, dx=6, dy=3, torso=-17, head=6, uF=762, fF=12, sw=-128, uB=-50)

W3 = dict(S2S, dy=4.5, torso=-12, head=4, uF=790, fF=10, sw=-120, footF=9)
S3 = dict(IDLE, dx=5, dy=-3, lift=4, torso=6, head=-4, uF=900, fF=10, sw=30, uB=-40, fB=50, footF=6, footB=-2)
S3B = dict(S3, dy=-4, lift=5, torso=10, head=-6, uF=925, fF=18, sw=52)
S3S = dict(S3B, dx=4, dy=1, lift=0, torso=8, head=-4, footF=5, footB=-3)
END = dict(S3S, uF=565)

HIT1 = dict(c=(5, -9), rx=32, ry=8, tilt=-5, a0=200, a1=-12, w=6, split=True)
HIT2 = dict(c=(9, -12), rx=24, ry=17, tilt=28, a0=-150, a1=62, w=5, hit=10)
HIT3 = dict(c=(8, -10), rx=21, ry=22, tilt=0, a0=80, a1=-160, w=5, hit=0)

def fx_pose(P, spec, stage, cols, front, imp=None):
    def run(c, hip):
        fx.slash(c, hip, spec, stage, cols)
    def hit(c, hip):
        if imp is None: return
        if 'hit' in spec:
            s2 = dict(spec, a1=spec['hit'])
            fx.impact(c, fx.tip(hip, s2), imp)
        else:
            fx.impact(c, fx.tip(hip, spec), imp)
    if spec.get('split'):
        return dict(P, _fxb=lambda c, h: fx.slash(c, h, spec, stage, cols, 'far'),
                    _fxf=lambda c, h: (fx.slash(c, h, spec, stage, cols, 'near'), hit(c, h)))
    if front:
        return dict(P, _fxf=lambda c, h: (run(c, h), hit(c, h)))
    return dict(P, _fxb=run, _fxf=hit)

def hit_seq(prev, W, S, SB, SS, spec, cols, front):
    return [lerp(prev, W, 0.55), W,
            fx_pose(S, spec, 0, cols, front, 0), fx_pose(SB, spec, 1, cols, front, 1),
            fx_pose(SS, spec, 2, cols, front, 2), fx_pose(SS, spec, 3, cols, front)]

def combo(cols):
    h1 = hit_seq(END, W1, S1, S1B, S1S, HIT1, cols, True)
    h2 = hit_seq(S1S, W2, S2, S2B, S2S, HIT2, cols, False)
    h3 = hit_seq(S2S, W3, S3, S3B, S3S, HIT3, cols, False)
    h3[-1] = dict(h3[-1], **{k: v for k, v in S3S.items()})
    return h1, h2, h3

def enter():
    w = dict(W1, uF=-40, sw=-260)
    return [lerp(IDLE, w, ease_io(0.5)), w]

def enter_bridge():
    return dict(W1)

def exit_from(P):
    return [lerp(P, IDLE_W, ease_io(i / 4)) for i in range(1, 5)]

def render(d, seq):
    return [d.frame(P, size=SIZE) for P in seq]

if __name__ == '__main__':
    looks = json.load(open(sys.argv[1])); out = sys.argv[2]
    for key, look in looks.items():
        d = R.Doll(look)
        if d.kind == 'staff': continue
        os.makedirs(f'{out}/{key}', exist_ok=True)
        h1, h2, h3 = combo(d.slash)
        ent = enter()
        seqs = {'Idle': R.idle_seq(), 'Enter': ent, 'Hit1': h1, 'Hit2': h2, 'Hit3': h3,
                'Exit1': exit_from(S1S), 'Exit2': exit_from(S2S), 'Exit3': exit_from(S3S)}
        for name, seq in seqs.items():
            for i, f in enumerate(render(d, seq)): f.save(f'{out}/{key}/{name}_{i:02d}.png')
