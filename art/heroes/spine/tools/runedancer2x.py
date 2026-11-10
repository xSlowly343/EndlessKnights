import numpy as np

from pix import Canvas
from armor2x import piece, BODY_PIVOT
from forge2x import torso_canvas, torso_piece, side_piece, back_piece, pauldron_piece

SEX = "female"
EYE = [(60, 30, 40), (214, 140, 40), (250, 204, 96), (255, 250, 232)]
RUNES = {"flame": (255, 120, 48), "frost": (96, 216, 255), "thunder": (255, 226, 80), "shadow": (176, 104, 255), "light": (255, 248, 214)}

PAL = {
    "#": (44, 20, 34),
    "x": (170, 112, 88), "S": (204, 158, 128), "s": (230, 200, 170), "Z": (244, 224, 200),
    "X": (26, 16, 40), "H": (46, 28, 66), "h": (72, 44, 98), "l": (122, 78, 150),
    "D": (98, 22, 46), "T": (152, 34, 58), "t": (198, 58, 70), "g": (236, 112, 98),
    "V": (130, 84, 30), "Y": (196, 146, 56), "y": (236, 196, 96), "v": (252, 236, 170),
    "J": (150, 128, 124), "K": (204, 188, 172), "k": (234, 224, 206), "j": (250, 246, 236),
    "N": (52, 30, 44), "B": (88, 52, 66), "b": (124, 78, 88), "n": (164, 112, 112),
    "A": (60, 64, 88), "M": (112, 120, 146), "m": (172, 182, 204), "w": (222, 230, 242), "W": (250, 252, 255),
    "e": (40, 150, 190), "E": (96, 216, 255), "L": (214, 250, 255),
    "R": (190, 36, 64), "r": (255, 124, 124),
}

SKIN3 = ("x", "S", "s")
HAIR3 = ("H", "h", "h")
CLOTH3 = ("D", "T", "t")
GOLD3 = ("V", "Y", "y")
IVORY3 = ("J", "K", "k")
WRAP3 = ("N", "B", "b")
STEEL3 = ("M", "m", "w")
HEAD_OFF = (8, 8)


def char_scale2x(rows):
    a = np.array([[ord(ch) for ch in r] for r in rows])
    h, w = a.shape
    p = np.pad(a, 1, mode="edge")
    A, B, C, D, E = p[:-2, 1:-1], p[1:-1, 2:], p[1:-1, :-2], p[2:, 1:-1], p[1:-1, 1:-1]
    o = np.zeros((h * 2, w * 2), a.dtype)
    o[0::2, 0::2] = np.where((C == A) & (C != D) & (A != B), A, E)
    o[0::2, 1::2] = np.where((A == B) & (A != C) & (B != D), B, E)
    o[1::2, 0::2] = np.where((D == C) & (D != B) & (C != A), C, E)
    o[1::2, 1::2] = np.where((B == D) & (B != A) & (D != C), D, E)
    return ["".join(chr(v) for v in r) for r in o]


def head_piece(c):
    bx, by = BODY_PIVOT["helmet"]
    return c.text(), (bx + HEAD_OFF[0], by + HEAD_OFF[1])


def hair_front(base_hair):
    ox, oy = HEAD_OFF
    c = Canvas(32 + ox + 2, 38 + oy)
    grid = char_scale2x(base_hair)
    for y, row in enumerate(grid[:36]):
        for x, ch in enumerate(row):
            if ch == ".":
                continue
            if y >= 26 and x < 12:
                limit = 12 - (y - 26) * 1.1
                if x >= limit:
                    continue
            c.put(ox + x, oy + y, ch)
    for y in range(26, 36):
        xs = [x for x in range(c.w) if c.get(x, oy + y) != "."]
        if not xs:
            continue
        c.put(max(xs), oy + y, "o")
        if len(xs) > 1:
            c.put(max(xs) - 1, oy + y, "o")
    last = max(y for y in range(c.h) if any(c.get(x, y) != "." for x in range(c.w)))
    for x in range(c.w):
        if c.get(x, last) != ".":
            c.put(x, last, "o")
    knot = c.mask_ellipse(ox + 5, oy + 1.5, 6.5, 6)
    c.shade(knot, HAIR3)
    for x, y in knot:
        if (x - ox - 5) ** 2 / 20 + (y - oy - 1) ** 2 / 14 < 0.25:
            c.put(x, y, "l")
    c.outline(knot, "o")
    ring = c.mask_poly([(ox + 5, oy + 7), (ox + 12, oy + 3), (ox + 13.5, oy + 5.5), (ox + 7, oy + 10)])
    c.shade(ring, GOLD3, edge="V")
    for x in range(ox + 3, ox + 28):
        y = oy + 15 + (1 if x < ox + 10 or x > ox + 26 else 0)
        c.put(x, y, "y" if x % 3 else "v")
        c.put(x, y + 1, "V")
    gx, gy = ox + 20, oy + 16
    c.stamp(gx - 2, gy, "VyyV\nyRry\nVRRV\n VV ")
    for x, y in ((ox + 14, oy + 7), (ox + 15, oy + 8), (ox + 16, oy + 8), (ox + 24, oy + 10), (ox + 25, oy + 11)):
        c.put(x, y, "l")
    return head_piece(c)


def stroke(c, pts, radii, steps=14):
    m = set()
    for (p0, r0), (p1, r1) in zip(zip(pts, radii), zip(pts[1:], radii[1:])):
        for i in range(steps + 1):
            t = i / steps
            m |= c.mask_ellipse(p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, r0 + (r1 - r0) * t, r0 + (r1 - r0) * t)
    return m


PONYTAIL_ROOT = (56, 22)
PONYTAIL_PATH = [(56, 22), (51, 12), (42, 6), (30, 5), (18, 8), (8, 14), (3, 21)]
PONYTAIL_RADII = [3.5, 4.5, 5, 5, 4.2, 3, 1.4]


def ponytail():
    c = Canvas(60, 27)
    m = stroke(c, PONYTAIL_PATH, PONYTAIL_RADII)
    c.shade(m, ("X", "H", "h"), light=(0.2, 0.8))
    for x, y in m:
        if (x, y - 2) in m and (x, y - 3) not in m and x % 7 < 4:
            c.put(x, y, "l")
    c.outline(m, "o")
    for bx in (44, 22):
        band = {(x, y) for x, y in m if bx <= x < bx + 3}
        for x, y in band:
            edge = (x, y - 1) not in m or (x, y + 1) not in m
            c.put(x, y, "o" if edge else ("y" if x == bx + 1 else "Y"))
    return c.text(), (PONYTAIL_ROOT[0] - 16.5, PONYTAIL_ROOT[1] + 2.5)


def skin(c, m, edge="x"):
    c.shade(m, SKIN3, edge=edge)


def crimson(c, m):
    c.shade(m, CLOTH3, edge="D")


def gold(c, m):
    c.shade(m, GOLD3, edge="V")


def ivory(c, m, edge="J"):
    c.shade(m, IVORY3, edge=edge)


def coins(c, y, x0, x1, step=4):
    for x in range(x0, x1, step):
        c.put(x, y, "y")
        c.put(x, y + 1, "V")


def sash(c, x0, x1, y):
    m = c.mask_rect(x0, y, x1, y + 4)
    crimson(c, m)
    for x in range(x0 + 1, x1 - 1):
        c.put(x, y, "y" if x % 2 else "Y")
    coins(c, y + 4, x0 + 2, x1 - 1)
    return m


def torso_front():
    c, o = torso_canvas()
    waist = c.mask_poly([(o + 3, 13), (o + 19, 13), (o + 18, 18), (o + 19, 22), (o + 3, 22), (o + 4, 18)])
    skin(c, waist)
    c.put(o + 11, 18, "x")
    c.put(o + 11, 19, "S")
    top = c.mask_poly([(o + 1, 0), (o + 21, 0), (o + 21, 12), (o + 18, 14), (o + 4, 14), (o + 1, 12)])
    crimson(c, top)
    neck = c.mask_poly([(o + 7, 0), (o + 15, 0), (o + 11, 9)])
    skin(c, neck, edge=None)
    for i in range(0, 9):
        c.put(o + 7 + i // 2, i, "y")
        c.put(o + 15 - i // 2, i, "y")
    c.put(o + 11, 9, "v")
    c.stamp(o + 10, 10, "yRy\n y ")
    for x in range(o + 4, o + 19):
        c.put(x, 13, "y" if x % 2 else "Y")
    coins(c, 14, o + 5, o + 18, 3)
    hips = c.mask_poly([(o + 1, 22), (o + 21, 22), (o + 22, 33), (o, 33)])
    ivory(c, hips)
    for x in (o + 6, o + 15):
        c.line(x, 27, x + 1, 32, "J", hips)
    sash(c, o + 1, o + 21, 21)
    knot = c.mask_ellipse(o + 5.5, 23.5, 3, 3)
    crimson(c, knot)
    c.put(o + 5, 23, "g")
    return torso_piece(c, o)


def torso_side():
    c, o = torso_canvas(22)
    waist = c.mask_rect(6, 13, 16, 22)
    skin(c, waist)
    top = c.mask_poly([(4, 0), (18, 0), (18, 12), (16, 14), (6, 14), (4, 12)])
    crimson(c, top)
    for x in range(6, 16):
        c.put(x, 13, "y" if x % 2 else "Y")
    coins(c, 14, 7, 15, 3)
    for y in range(0, 9):
        c.put(14 + y // 3, y, "y")
    hips = c.mask_rect(4, 22, 18, 33)
    ivory(c, hips)
    sash(c, 4, 18, 21)
    return side_piece(c, 0)


def torso_back():
    c, o = torso_canvas()
    waist = c.mask_poly([(o + 3, 13), (o + 19, 13), (o + 18, 18), (o + 19, 22), (o + 3, 22), (o + 4, 18)])
    skin(c, waist)
    for y in range(14, 21):
        c.put(o + 11, y, "x" if y % 3 else "S")
    top = c.mask_poly([(o + 1, 0), (o + 21, 0), (o + 21, 12), (o + 18, 14), (o + 4, 14), (o + 1, 12)])
    crimson(c, top)
    for y in range(2, 12, 3):
        c.line(o + 9, y, o + 13, y + 2, "y", top)
        c.line(o + 13, y, o + 9, y + 2, "Y", top)
    for x in range(o + 4, o + 19):
        c.put(x, 13, "y" if x % 2 else "Y")
    hips = c.mask_poly([(o + 1, 22), (o + 21, 22), (o + 22, 33), (o, 33)])
    ivory(c, hips)
    sash(c, o + 1, o + 21, 21)
    return back_piece(c, o)


def pelvis():
    c = Canvas(20, 7)
    m = c.mask_poly([(0, 0), (20, 0), (20, 5), (17, 7), (11, 7), (10, 5), (9, 7), (3, 7), (0, 5)])
    ivory(c, m)
    return c.text(), (10.5, 0.5)


def upper_arm(short):
    c = Canvas(14, 18 if short else 22)
    arm = c.mask_poly([(4, 1), (10, 1), (10, c.h - 1), (9, c.h), (5, c.h), (4, c.h - 1)])
    skin(c, arm)
    cap = c.mask_poly([(3, 3), (5, 0), (9, 0), (11, 3), (11, 6), (3, 6)])
    crimson(c, cap)
    for x in range(3, 11):
        c.put(x, 6, "y" if x % 2 else "Y")
    band = c.mask_rect(4, 10 if short else 12, 10, 12 if short else 14)
    gold(c, band)
    c.put(7, 10 if short else 12, "R")
    return pauldron_piece(c, 4, 1)


def elbow_cap():
    c = Canvas(6, 6)
    m = c.mask_ellipse(3, 3, 3, 3)
    c.shade(m, SKIN3)
    return c.text(), (2.5, 2.5)


def forearm():
    c = Canvas(8, 12)
    m = c.mask_rect(1, 0, 7, 12)
    skin(c, m)
    wrap = c.mask_rect(1, 4, 7, 12)
    c.shade(wrap, WRAP3, edge="N")
    for y in (6, 9):
        c.line(1, y, 6, y + 1, "n", wrap)
    return piece(c, "forearm", -1, 0)


def hand():
    c = Canvas(10, 10)
    m = c.mask_poly([(2, 1), (8, 1), (9, 3), (9, 8), (7, 10), (3, 10), (1, 7)])
    skin(c, m)
    for x, y in m:
        if y <= 3:
            c.put(x, y, "B" if x < 5 else "b")
    c.outline({p for p in m if p[1] <= 3}, "N")
    return piece(c, "hand", -2, -1)


def bangle():
    c = Canvas(10, 4)
    m = c.mask_poly([(1, 0), (9, 0), (9, 4), (1, 4)])
    gold(c, m)
    c.put(6, 1, "v")
    return piece(c, "bracer", -2, 2)


def thigh():
    c = Canvas(12, 20)
    m = c.mask_poly([(2, 0), (10, 0), (12, 6), (12, 15), (10, 20), (2, 20), (0, 15), (0, 6)])
    ivory(c, m)
    c.line(4, 3, 3, 16, "J", m)
    c.line(8, 5, 9, 17, "K", m)
    return piece(c, "thigh", -2, -1)


def shin():
    c = Canvas(12, 19)
    ankle = c.mask_rect(4, 14, 8, 19)
    skin(c, ankle)
    m = c.mask_poly([(0, 0), (12, 0), (12, 9), (9, 14), (3, 14), (0, 9)])
    ivory(c, m)
    c.line(3, 1, 4, 11, "J", m)
    c.line(8, 2, 8, 12, "K", m)
    cuff = c.mask_rect(3, 13, 9, 16)
    gold(c, cuff)
    return piece(c, "shin", -2, 0)


def knee_cap():
    c = Canvas(10, 10)
    m = c.mask_ellipse(5, 5, 5, 5)
    c.shade(m, IVORY3)
    return c.text(), (4.5, 4.5)


def foot():
    c = Canvas(20, 8)
    m = c.mask_poly([(0, 1), (9, 1), (13, 3), (16, 4.5), (18, 3), (20, 1), (20, 4), (18, 7), (15, 8), (0, 8)])
    crimson(c, m)
    for x in range(0, 16):
        if (x, 7) in m:
            c.put(x, 7, "#")
    for x in range(1, 10):
        c.put(x, 1, "y" if x % 2 else "Y")
    c.put(19, 1, "v")
    c.put(18, 2, "y")
    return piece(c, "foot", -3, -1)


def sash_tail():
    c = Canvas(10, 26)
    m = c.mask_poly([(1, 0), (8, 0), (9, 12), (10, 23), (1, 23), (0, 12)])
    crimson(c, m)
    c.line(4, 2, 3, 21, "D", m)
    for x in range(1, 10):
        c.put(x, 21, "y" if x % 2 else "Y")
    for x in range(1, 10, 2):
        c.put(x, 23, "y")
        c.put(x, 24, "Y")
        c.put(x, 25, "V")
    return piece(c, "coat_tail", 0, 0)


def veil():
    w, h = 14, 44
    c = Canvas(w, h)
    m = set()
    for y in range(h):
        ww = round(6 + 6 * y / (h - 1))
        x0 = (w - ww) // 2
        m |= {(x, y) for x in range(x0, x0 + ww)}
    c.shade(m, ("D", "T", "t"), edge="D")
    c.line(5, 3, 4, h - 4, "D", m)
    c.line(9, 6, 10, h - 4, "g", m)
    for x, y in m:
        if y >= h - 3:
            c.put(x, y, "y" if (x + y) % 2 else "Y")
    return c.text(), ((w - 1) / 2 + 9, 0.5)


def rune_glyph(c, x, y, glyph):
    c.stamp(x, y, glyph)


DAGGER_RUNE = "E\nLE\nE\n E\nLE\nE"
DAGGER_GRIP = (5.5, 29.5)


def dagger():
    c = Canvas(12, 38)
    blade = c.mask_poly([(6, 0), (8, 5), (9.5, 13), (9, 21), (8, 25), (4, 25), (3, 21), (2.5, 13), (4, 5)])
    c.shade(blade, STEEL3, spec="W", edge="A")
    c.put(5, 1, "W")
    c.put(6, 1, "w")
    rune_glyph(c, 5, 8, DAGGER_RUNE)
    for y in range(16, 24):
        c.put(5, y, "e")
    guard = c.mask_poly([(0, 24), (2, 25), (10, 25), (12, 24), (12, 26), (9, 28), (3, 28), (0, 26)])
    gold(c, guard)
    c.put(6, 26, "R")
    c.put(5, 26, "r")
    grip = c.mask_rect(4, 28, 8, 34)
    c.shade(grip, CLOTH3, edge="D")
    for y in (29, 31, 33):
        c.line(4, y, 7, y, "#", grip)
    pommel = c.mask_ellipse(6, 35.5, 2.6, 2.5)
    gold(c, pommel)
    c.put(6, 35, "v")
    return c.text(), DAGGER_GRIP


def build(base_hair):
    outfit = {"torso": torso_front(), "torso_side": torso_side(), "torso_back": torso_back(), "pelvis": pelvis(),
              "upper_arm": upper_arm(False), "upper_arm_short": upper_arm(True), "elbow_cap": elbow_cap(),
              "forearm": forearm(), "hand": hand(), "bracer": bangle(), "thigh": thigh(), "knee_cap": knee_cap(),
              "shin": shin(), "foot": foot(), "coat_tail": sash_tail(), "cape": veil()}
    blade = dagger()
    return {"sex": SEX, "eye": EYE, "palette": PAL,
            "skins": {"hair/runedancer": {"hair_front": hair_front(base_hair), "hair_fly": ponytail()},
                      "outfit/runedancer": outfit,
                      "weapon/rune_daggers": {"weapon": blade, "weapon_far": blade}},
            "preset": ["body/female", "hair/runedancer", "eyes/runedancer", "outfit/runedancer", "weapon/rune_daggers"]}
