import json
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DOLL_SRC = os.path.normpath(os.path.join(ROOT, "..", "doll", "src"))
sys.path.insert(0, DOLL_SRC)

import front as FR
import body as BD
from pal import MAT, LETTER, OUTLINE

OUT = os.path.join(ROOT, "base", "hero")
SCALE = 8
HAIR = set("HhlX")
EYE = set("qeiE")
BLUSH = set("A")


def grid(txt):
    rows = txt.strip("\n").split("\n")
    w = max(len(r) for r in rows)
    return [r.ljust(w, ".") for r in rows]


LOOKS = json.load(open(os.path.join(DOLL_SRC, "looks.json")))


def palette(look=None):
    pal = {k: [tuple(c) for c in v] for k, v in MAT.items()}
    if look:
        for k, v in LOOKS[look].get("pal", {}).items():
            pal[k] = [tuple(c) for c in v]
    return pal


def color(ch, pal=None):
    pal = pal or MAT
    if ch == "o":
        c = OUTLINE
    elif ch in LETTER:
        m, i = LETTER[ch]
        c = pal[m][i]
    else:
        return None
    return tuple(c) + (255,)


def image(rows, pal=None):
    im = Image.new("RGBA", (len(rows[0]), len(rows)))
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            c = color(ch, pal)
            if c:
                im.putpixel((x, y), c)
    return im


def neighbours(rows, x, y):
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = x + dx, y + dy
        if 0 <= ny < len(rows) and 0 <= nx < len(rows[0]):
            yield rows[ny][nx]


def split_head(rows):
    base, hair, eyes, blush = [], [], [], []
    for y, r in enumerate(rows):
        b, h, e, bl = [], [], [], []
        for x, ch in enumerate(r):
            near = set(neighbours(rows, x, y))
            b.append("s" if ch in HAIR | EYE | BLUSH else ch)
            h.append(ch if ch in HAIR or (ch == "o" and near & HAIR) else ".")
            e.append(ch if ch in EYE else ".")
            bl.append(ch if ch in BLUSH else ".")
        base.append("".join(b))
        hair.append("".join(h))
        eyes.append("".join(e))
        blush.append("".join(bl))
    return base, hair, eyes, blush


def eye_boxes(eyes):
    cols = sorted({x for r in eyes for x, ch in enumerate(r) if ch != "."})
    groups, cur = [], [cols[0]]
    for c in cols[1:]:
        if c == cur[-1] + 1:
            cur.append(c)
        else:
            groups.append(cur)
            cur = [c]
    groups.append(cur)
    ys = [y for y, r in enumerate(eyes) if any(ch != "." for ch in r)]
    return [(g[0], ys[0], g[-1], ys[-1]) for g in groups]


def eye_states(eyes, box):
    x0, y0, x1, y1 = box
    w, h = x1 - x0 + 1, y1 - y0 + 1
    open_ = [eyes[y][x0:x1 + 1] for y in range(y0, y1 + 1)]
    half = ["." * w] * (h - 2) + ["q" * w, open_[-1]]
    closed = ["." * w] * (h - 1) + ["x" * w]
    squint = ["." * w] * (h - 2) + ["q" * w, "e" * w]
    return {"open": open_, "half": half, "closed": closed, "squint": squint}, (x0, y0)


def scale2x(a):
    h, w = a.shape[:2]
    p = np.pad(a, ((1, 1), (1, 1), (0, 0)), mode="edge")
    A, B, C, D, E = p[:-2, 1:-1], p[1:-1, 2:], p[1:-1, :-2], p[2:, 1:-1], p[1:-1, 1:-1]
    eq = lambda x, y: np.all(x == y, axis=-1)
    o = np.zeros((h * 2, w * 2, a.shape[2]), a.dtype)
    c1 = eq(C, A) & ~eq(C, D) & ~eq(A, B)
    c2 = eq(A, B) & ~eq(A, C) & ~eq(B, D)
    c3 = eq(D, C) & ~eq(D, B) & ~eq(C, A)
    c4 = eq(B, D) & ~eq(B, A) & ~eq(D, C)
    o[0::2, 0::2] = np.where(c1[..., None], A, E)
    o[0::2, 1::2] = np.where(c2[..., None], B, E)
    o[1::2, 0::2] = np.where(c3[..., None], C, E)
    o[1::2, 1::2] = np.where(c4[..., None], D, E)
    return o


def upscale(im):
    a = np.array(im)
    for _ in range(int(math.log2(SCALE))):
        a = scale2x(a)
    return Image.fromarray(a)


ELBOW_CAP = """
TT
TT
"""


KNEE_CAP = """
.pp.
Pppu
Pppu
.pu.
"""


def outfit_parts():
    return {
        "torso": (FR.TORSO, [5, 13]),
        "torso_side": (FR.TORSO_SIDE, [5, 13]),
        "torso_back": (FR.TORSO_BACK, [5, 13]),
        "pelvis": (FR.PELVIS, [5, 0]),
        "upper_arm": (FR.UARM, [1, 0]),
        "upper_arm_short": (FR.UARM_SHORT, [1, 0]),
        "elbow_cap": (ELBOW_CAP, [0.5, 0.5]),
        "forearm": (FR.FARM, [1, 0]),
        "hand": (FR.HAND, [1, 1]),
        "thigh": (FR.THIGH, [2, 0]),
        "knee_cap": (KNEE_CAP, [1.5, 1.5]),
        "shin": (FR.SHIN, [2, 0]),
        "foot": (FR.FOOT, [1, 0]),
        "coat_tail": (FR.COAT_TAIL, [2, 0]),
    }


WEAPONS = {"sword": (BD.SWORD, [4, 20]), "axe": (BD.AXE, [6, 15]), "staff": (BD.STAFF, [4, 11])}
SHOULDERS = {"torso": (-1.3, 11.3), "torso_side": (5.0, 7.0), "torso_back": (0.5, 9.0)}
HEAD_PIVOT = [9, 17]
HAIR_FLY_ON_HEAD = [3, 9]


def head_split(sex):
    rows = grid(FR.HEAD_F if sex == "female" else FR.HEAD_M)
    base, hair, eyes, blush = split_head(rows)
    return rows, base, hair, eyes, blush


def hair_fly(sex):
    return grid(FR.HAIR_F_FLY if sex == "female" else FR.HAIR_M_FLY)


def save(path, rows, pal):
    im = image(rows, pal)
    for folder, out in (("parts_1x", im), (f"parts_{SCALE}x", upscale(im))):
        full = os.path.join(OUT, folder, path + ".png")
        os.makedirs(os.path.dirname(full), exist_ok=True)
        out.save(full)
    return list(im.size)


def export():
    manifest = {"source": "art/heroes/doll/src (front.py, body.py, pal.py, looks.json)", "scale": SCALE, "outline": list(OUTLINE), "attachments": {}}
    att = manifest["attachments"]

    def put(path, rows, pal, pivot, **extra):
        att[path] = dict({"size": save(path, rows, pal), "pivot": pivot}, **extra)

    hair_shape = {"male": "short", "female": "long"}
    for sex in ("male", "female"):
        rows, base, hair, eyes, blush = head_split(sex)
        put(f"body/{sex}/head", base, palette(), HEAD_PIVOT)
        put(f"body/{sex}/blush", blush, palette(), HEAD_PIVOT)
        for look in LOOKS:
            pal = palette(look)
            shape = hair_shape[sex]
            put(f"hair/{shape}_{look}/hair_front", hair, pal, HEAD_PIVOT)
            fly = hair_fly(sex)
            put(f"hair/{shape}_{look}/hair_fly", fly, pal, [len(fly[0]) - 1, 1])
        for look in LOOKS:
            pal = palette(look)
            for side, box in zip(("near", "far"), eye_boxes(eyes)):
                states, origin = eye_states(eyes, box)
                for state, g in states.items():
                    pivot = [HEAD_PIVOT[0] - origin[0], HEAD_PIVOT[1] - origin[1]]
                    put(f"eyes/{look}_{sex}/eye_{side}_{state}", g, pal, pivot)
    for look in LOOKS:
        pal = palette(look)
        for name, (txt, pivot) in outfit_parts().items():
            put(f"outfit/{look}/{name}", grid(txt), pal, pivot)
    for name, (txt, grip) in WEAPONS.items():
        put(f"weapon/{name}/weapon", grid(txt), palette(), grip)
    with open(os.path.join(OUT, "attachments.json"), "w") as f:
        json.dump(manifest, f, indent=1)
    return manifest


def check_heads():
    report = {}
    for sex in ("male", "female"):
        rows, base, hair, eyes, blush = head_split(sex)
        canvas = image(base)
        canvas.alpha_composite(image(hair))
        canvas.alpha_composite(image(blush))
        canvas.alpha_composite(image(eyes))
        report[sex] = np.array_equal(np.array(canvas), np.array(image(rows)))
    return report


def main():
    manifest = export()
    print("attachments", len(manifest["attachments"]), "head split exact:", check_heads())


if __name__ == "__main__":
    main()
