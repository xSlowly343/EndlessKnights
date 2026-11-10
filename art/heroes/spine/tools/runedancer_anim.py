import math

GUARD = {"torso": -4, "head": 3, "dy": 1.5, "uF": 30, "fF": 75, "sw": -50, "uB": -25, "fB": 50, "swB": 140, "footF": 6, "footB": -5}
BREATH = dict(GUARD, dy=2.6, torso=-6, head=4, uF=34, fF=82, sw=-44, uB=-21, fB=57, swB=135)

W1A = dict(GUARD, dx=-1, dy=2.5, torso=4, head=-2, uF=-20, fF=100, sw=20, uB=20, fB=45, swB=20, footB=-7)
W1 = dict(GUARD, tv="back", uv="short", dx=-2, dy=3.5, torso=12, head=-7, uF=-55, fF=115, sw=75, uB=45, fB=40, swB=-60, footF=4, footB=-8, liftF=2)
S1 = dict(GUARD, tv="side", dx=11, dy=5, torso=-28, head=12, uF=100, fF=0, sw=-100, uB=-80, fB=20, swB=125, footF=22, footB=0)
S1B = dict(S1, dx=12, dy=5.5, torso=-31, head=13, uF=72, fF=-6, sw=-132, uB=-88)
S1S = dict(S1B, dx=11.5, dy=5, torso=-26, head=11, uF=62, fF=4, sw=-136, uB=-76)
S1R = dict(S1S, dx=10, dy=4.5, torso=-18, head=8, uF=48, fF=30, sw=-112, uB=-50, fB=36, swB=150)

W2A = dict(S1R, dx=10, dy=5, torso=-22, head=8, uF=10, fF=60, sw=-60, uB=10, fB=30, swB=-175, footF=21, footB=3)
W2 = dict(W2A, dy=5.5, torso=-24, head=9, uF=-30, fF=70, sw=30, uB=35, fB=25, swB=-165)
S2 = dict(GUARD, tv="front", dx=15, dy=2, torso=6, head=-6, uF=-60, fF=40, sw=110, uB=150, fB=10, swB=-35, footF=24, footB=8)
S2B = dict(S2, dx=15.5, dy=1.5, torso=9, head=-8, uB=172, fB=14, swB=2, uF=-68)
S2S = dict(S2B, dy=2, torso=6, head=-5, uB=160, fB=20, swB=10, uF=-55)
S2R = dict(S2S, dx=15, dy=3, torso=0, head=0, uF=-20, fF=70, sw=40, uB=110, fB=40, swB=-20)

W3A = dict(S2R, tv="side", dx=14.5, dy=5.5, torso=-8, head=4, uF=30, fF=100, sw=-20, uB=60, fB=90, swB=-30, footF=23, footB=8)
W3 = dict(W3A, dy=7.5, torso=-12, head=6, uF=40, fF=115, sw=-10, uB=45, fB=115, swB=-15)
P1 = dict(GUARD, tv="back", dx=13.5, dy=-2, lift=5, liftF=2, torso=5, head=-4, uF=-85, fF=8, sw=95, uB=85, fB=8, swB=-92, footF=19, footB=9)
P2 = dict(GUARD, tv="side", dx=12.5, dy=-5.5, lift=8.5, liftF=3, torso=2, head=0, uF=84, fF=4, sw=-88, uB=-84, fB=6, swB=96, footF=16, footB=9)
P3 = dict(GUARD, tv="front", dx=11, dy=-3, lift=5.5, liftF=1, torso=-4, head=4, uF=-70, fF=20, sw=120, uB=70, fB=20, swB=-120, footF=15, footB=3)
S3 = dict(GUARD, tv="side", dx=9, dy=6.5, torso=-30, head=13, uF=86, fF=4, sw=-104, uB=97, fB=0, swB=-99, footF=21, footB=-4)
S3B = dict(S3, dx=9.5, dy=7.2, torso=-33, head=14, uF=80, sw=-110, uB=90, swB=-106)
S3S = dict(S3B, dy=6.5, torso=-27, head=11)
S3R = dict(GUARD, tv="side", dx=5, dy=4, torso=-12, head=6, uF=44, fF=50, sw=-80, uB=30, fB=60, swB=-140, footF=13, footB=-5)


def lerp(p, q, t):
    out = {}
    for k in set(p) | set(q):
        a, b = p.get(k), q.get(k)
        if isinstance(a, str) or isinstance(b, str):
            out[k] = (b if t >= 0.5 else a) or "front"
        else:
            out[k] = (a or 0) + ((b or 0) - (a or 0)) * t
    return out


def ease(t):
    return t * t * (3 - 2 * t)


def idle(n=12):
    return [(lerp(GUARD, BREATH, (1 - math.cos(2 * math.pi * i / n)) / 2), 80) for i in range(n)]


def exit_to_guard(src):
    return [(lerp(src, GUARD, ease(i / 4)), 70) for i in range(1, 5)]


def sequences():
    hit1 = [(W1A, 50), (W1, 70), (S1, 30), (S1B, 40), (S1S, 60), (S1R, 50)]
    hit2 = [(W2A, 40), (W2, 70), (S2, 30), (S2B, 40), (S2S, 60), (S2R, 50)]
    hit3 = [(W3A, 40), (W3, 70), (P1, 40), (P2, 45), (P3, 45), (S3, 30), (S3B, 50), (S3S, 70), (S3R, 60)]
    return {
        "Idle": {"frames": idle(), "loop": True},
        "Enter": {"frames": [(lerp(GUARD, W1A, 0.6), 50), (W1A, 50)]},
        "Hit1": {"frames": hit1, "events": [(2, "hit", "rune_slash")]},
        "Hit2": {"frames": hit2, "events": [(2, "hit", "rune_rise")]},
        "Hit3": {"frames": hit3, "events": [(2, "fx", "rune_spin"), (5, "hit", "rune_word")]},
        "Exit1": {"frames": exit_to_guard(S1R)},
        "Exit2": {"frames": exit_to_guard(S2R)},
        "Exit3": {"frames": exit_to_guard(S3R)},
    }
