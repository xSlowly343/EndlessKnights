import math

import numpy as np
from PIL import Image, ImageDraw

MATERIALS = {
    "skin": [(150, 80, 70), (214, 138, 110), (244, 184, 150), (255, 214, 184)],
    "hair": [(132, 96, 92), (204, 166, 142), (246, 226, 190), (255, 250, 232)],
    "coat": [(20, 22, 66), (30, 44, 120), (44, 76, 180), (84, 130, 230)],
    "trim": [(120, 70, 20), (190, 120, 30), (240, 180, 50), (255, 230, 130)],
    "scarf": [(96, 16, 36), (160, 28, 48), (216, 52, 60), (250, 110, 90)],
    "cloth": [(22, 18, 36), (40, 34, 62), (62, 54, 92), (90, 82, 130)],
    "boot": [(60, 30, 24), (104, 56, 36), (150, 88, 52), (196, 130, 80)],
    "steel": [(50, 58, 84), (120, 134, 164), (190, 204, 224), (245, 250, 255)],
    "eye": [(40, 24, 40), (24, 110, 120), (60, 190, 180), (255, 255, 255)],
    "glow": [(40, 120, 150), (70, 200, 210), (150, 245, 235), (255, 255, 255)],
}
NAMES = list(MATERIALS)
OUTLINE = (58, 26, 40)


def tone(material, level):
    return NAMES.index(material) * 4 + level + 1


class Part:
    def __init__(self, w, h, pivot, angle=0.0):
        self.w, self.h = w, h
        self.pivot = pivot
        self.angle = angle
        self.im = Image.new("L", (w, h), 0)
        self.d = ImageDraw.Draw(self.im)

    def poly(self, pts, material, level=2):
        self.d.polygon([(x, y) for x, y in pts], fill=tone(material, level))
        return self

    def ell(self, box, material, level=2):
        self.d.ellipse(box, fill=tone(material, level))
        return self

    def rect(self, box, material, level=2):
        self.d.rectangle(box, fill=tone(material, level))
        return self

    def line(self, pts, material, level=2, width=1):
        self.d.line([(x, y) for x, y in pts], fill=tone(material, level), width=width)
        return self

    def dot(self, x, y, material, level=2):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.im.putpixel((x, y), tone(material, level))
        return self

    def erase(self, pts):
        self.d.polygon([(x, y) for x, y in pts], fill=0)
        return self

    def capsule(self, x0, x1, cy, r0, r1, material, level=2):
        self.d.polygon([(x0, cy - r0), (x1, cy - r1), (x1, cy + r1), (x0, cy + r0)], fill=tone(material, level))
        self.d.ellipse((x0 - r0, cy - r0, x0 + r0, cy + r0), fill=tone(material, level))
        self.d.ellipse((x1 - r1, cy - r1, x1 + r1, cy + r1), fill=tone(material, level))
        return self

    def shade(self, light=(1, -1), depth=2, only=None):
        a = np.array(self.im)
        solid = a > 0
        h, w = a.shape
        out = a.copy()
        for y in range(h):
            for x in range(w):
                v = a[y, x]
                if v == 0:
                    continue
                m, lv = divmod(v - 1, 4)
                if lv != 2 or (only and NAMES[m] not in only):
                    continue
                lit = dark = False
                for k in range(1, depth + 1):
                    lx, ly = x + light[0] * k, y + light[1] * k
                    if not (0 <= lx < w and 0 <= ly < h) or not solid[ly, lx]:
                        lit = True
                    sx, sy = x - light[0] * k, y - light[1] * k
                    if not (0 <= sx < w and 0 <= sy < h) or not solid[sy, sx]:
                        dark = True
                if dark:
                    out[y, x] = m * 4 + 2
                elif lit:
                    out[y, x] = m * 4 + 4
        self.im = Image.fromarray(out)
        self.d = ImageDraw.Draw(self.im)
        return self

    def outline(self):
        a = np.array(self.im)
        solid = np.pad(a > 0, 1)
        edge = ~(solid[:-2, 1:-1] & solid[2:, 1:-1] & solid[1:-1, :-2] & solid[1:-1, 2:]) & (a > 0)
        a[edge] = ((a[edge] - 1) // 4) * 4 + 1
        self.im = Image.fromarray(a)
        self.d = ImageDraw.Draw(self.im)
        return self

    def rgba(self):
        a = np.array(self.im)
        lut = np.zeros((len(NAMES) * 4 + 1, 4), np.uint8)
        for i, name in enumerate(NAMES):
            for lv in range(4):
                lut[i * 4 + lv + 1] = (*MATERIALS[name][lv], 255)
        return Image.fromarray(lut[a], "RGBA")


def scale2x(img):
    a = np.array(img).view(np.uint32)[..., 0]
    p = np.pad(a, 1, mode="edge")
    up, down, left, right = p[:-2, 1:-1], p[2:, 1:-1], p[1:-1, :-2], p[1:-1, 2:]
    ok = (up != down) & (left != right)
    out = np.zeros((a.shape[0] * 2, a.shape[1] * 2), np.uint32)
    out[0::2, 0::2] = np.where(ok & (left == up), left, a)
    out[0::2, 1::2] = np.where(ok & (up == right), right, a)
    out[1::2, 0::2] = np.where(ok & (left == down), left, a)
    out[1::2, 1::2] = np.where(ok & (down == right), right, a)
    return Image.fromarray(out.view(np.uint8).reshape(out.shape[0], out.shape[1], 4), "RGBA")


def texture(img, steps=2):
    for _ in range(steps):
        img = scale2x(img)
    return img


def limb(length, r0, r1, material, pad=2):
    r = max(r0, r1)
    p = Part(length + 2 * r + 2 * pad, 2 * r + 1 + 2 * pad, (pad + r, pad + r))
    p.capsule(pad + r, pad + r + length, pad + r, r0, r1, material)
    return p


def thigh():
    p = limb(15, 5, 4, "cloth")
    return p.shade((0, -1), 2).outline()


def shin():
    p = limb(15, 4, 4, "cloth")
    x0 = p.pivot[0]
    p.capsule(x0 + 5, x0 + 15, p.pivot[1], 4, 4, "boot")
    p.rect((x0 + 4, p.pivot[1] - 5, x0 + 6, p.pivot[1] + 5), "boot", 3)
    p.shade((0, -1), 2)
    p.rect((x0 + 4, p.pivot[1] - 5, x0 + 5, p.pivot[1] + 5), "trim", 2)
    return p.outline()


def foot():
    p = Part(15, 9, (4, 3))
    p.poly([(1, 1), (7, 1), (9, 3), (13, 5), (13, 7), (1, 7)], "boot")
    p.shade((1, -1), 1)
    p.rect((1, 7, 13, 7), "boot", 0)
    p.dot(11, 4, "boot", 3).dot(10, 4, "boot", 3)
    return p.outline()


def upper_arm():
    p = limb(11, 4, 3, "coat")
    x0, cy = p.pivot
    p.ell((x0 - 5, cy - 5, x0 + 5, cy + 5), "coat")
    p.shade((0, -1), 2)
    p.line([(x0 + 1, cy - 4), (x0 + 4, cy + 4)], "trim", 2)
    return p.outline()


def forearm():
    p = limb(10, 3, 4, "coat")
    x0, cy = p.pivot
    p.capsule(x0 + 6, x0 + 10, cy, 3, 3, "boot")
    p.shade((0, -1), 2)
    p.rect((x0 + 4, cy - 4, x0 + 5, cy + 4), "trim", 2)
    return p.outline()


def hand():
    p = Part(11, 11, (3, 5))
    p.ell((2, 2, 8, 8), "skin")
    p.shade((1, -1), 1)
    p.line([(5, 4), (5, 7)], "skin", 1)
    return p.outline()


def pelvis():
    p = Part(22, 14, (11, 4))
    p.poly([(4, 2), (17, 2), (19, 8), (14, 12), (11, 9), (8, 12), (2, 8)], "cloth")
    p.shade((1, -1), 2)
    p.rect((4, 2, 17, 3), "coat", 1)
    return p.outline()


def waist():
    p = Part(18, 16, (9, 13))
    p.poly([(3, 14), (14, 14), (14, 3), (3, 3)], "coat")
    p.shade((1, -1), 2)
    p.rect((3, 10, 14, 12), "boot", 2)
    p.rect((3, 10, 14, 10), "boot", 3)
    p.rect((9, 9, 12, 13), "trim", 2)
    p.rect((10, 10, 11, 12), "trim", 3)
    p.line([(10, 3), (10, 8)], "trim", 2)
    return p.outline()


def chest():
    p = Part(26, 20, (13, 17))
    p.poly([(5, 18), (19, 18), (22, 9), (20, 4), (15, 2), (9, 2), (4, 5), (3, 10)], "coat")
    p.shade((1, -1), 2)
    p.line([(14, 3), (14, 18)], "trim", 2)
    p.line([(15, 3), (15, 18)], "trim", 1)
    p.dot(17, 8, "trim", 3).dot(17, 12, "trim", 3).dot(17, 16, "trim", 3)
    p.poly([(9, 2), (15, 2), (13, 5), (11, 5)], "skin", 1)
    p.line([(6, 4), (4, 9)], "trim", 2)
    p.line([(19, 4), (21, 9)], "trim", 2)
    return p.outline()


def neck():
    p = Part(9, 9, (4, 6))
    p.rect((2, 1, 6, 7), "skin", 1)
    return p


def head():
    p = Part(34, 32, (15, 28))
    p.ell((5, 7, 28, 28), "skin")
    p.poly([(20, 22), (28, 20), (26, 26), (20, 29), (12, 28)], "skin")
    p.shade((1, -1), 2)
    p.ell((6, 15, 10, 21), "skin", 1)
    p.dot(22, 25, "skin", 1).dot(23, 25, "skin", 1)
    p.dot(13, 23, "scarf", 3).dot(14, 23, "scarf", 3).dot(15, 24, "scarf", 3)
    return p.outline()


def eye(kind, far=False):
    w = 3 if far else 4
    p = Part(w + 2, 9, (1, 4))
    if kind == "open":
        p.rect((1, 1, w, 6), "eye", 0)
        p.rect((1, 3, w, 5), "eye", 2)
        p.rect((1, 5, w, 6), "eye", 1)
        p.dot(w, 2, "eye", 3).dot(w, 3, "eye", 3)
    elif kind == "fierce":
        p.rect((1, 3, w, 6), "eye", 0)
        p.rect((1, 4, w, 5), "eye", 2)
        p.dot(w, 4, "eye", 3)
        p.line([(0, 1), (w, 3)], "hair", 0)
    elif kind == "half":
        p.rect((1, 3, w, 6), "eye", 0)
        p.rect((1, 5, w, 6), "eye", 1)
    else:
        p.rect((1, 5, w, 5), "eye", 0)
    return p


def mouth(kind):
    p = Part(6, 5, (2, 2))
    if kind == "shout":
        p.rect((1, 1, 3, 3), "scarf", 0)
        p.dot(2, 2, "scarf", 3)
    elif kind == "grit":
        p.rect((1, 2, 4, 2), "eye", 0)
    else:
        p.rect((2, 2, 3, 2), "skin", 0)
    return p


def hair_back():
    p = Part(40, 40, (21, 34))
    p.ell((2, 1, 30, 27), "hair")
    p.poly([(2, 14), (16, 10), (18, 30), (12, 37), (6, 33), (1, 26)], "hair")
    p.poly([(4, 30), (2, 36), (8, 33)], "hair")
    p.poly([(10, 34), (11, 39), (15, 33)], "hair")
    p.shade((1, -1), 3)
    p.line([(6, 18), (7, 30)], "hair", 1)
    p.line([(11, 20), (12, 33)], "hair", 1)
    return p.outline()


def hair_front():
    p = Part(40, 34, (19, 31))
    p.ell((6, 1, 33, 21), "hair")
    p.poly([(6, 10), (16, 8), (15, 20), (11, 27), (7, 22)], "hair")
    p.poly([(17, 12), (21, 22), (23, 13)], "hair")
    p.poly([(23, 12), (27, 20), (29, 12)], "hair")
    p.poly([(29, 11), (33, 19), (34, 10)], "hair")
    p.shade((1, -1), 3)
    p.line([(14, 4), (24, 3)], "hair", 3)
    p.line([(12, 6), (10, 12)], "hair", 1)
    p.line([(20, 7), (21, 14)], "hair", 1)
    p.line([(27, 6), (28, 13)], "hair", 1)
    p.outline()
    p.line([(9, 9), (31, 7)], "scarf", 2)
    p.line([(9, 10), (31, 8)], "scarf", 1)
    return p


def hair_lock():
    p = Part(9, 18, (4, 2))
    p.poly([(2, 1), (6, 1), (7, 9), (4, 16), (2, 10)], "hair")
    p.shade((1, -1), 1)
    return p.outline()


def bow():
    p = Part(13, 13, (8, 6))
    p.poly([(7, 6), (1, 1), (2, 6), (1, 11)], "scarf")
    p.poly([(7, 6), (11, 2), (11, 10)], "scarf")
    p.shade((1, -1), 1)
    p.rect((6, 5, 8, 7), "scarf", 3)
    return p.outline()


def strip(length, widths, material, pad=2, tip=None, stripe=None):
    r = int(max(widths))
    p = Part(length + 2 * pad, 2 * r + 1 + 2 * pad, (pad, pad + r))
    n = len(widths) - 1
    top, bot = [], []
    for i, wd in enumerate(widths):
        x = pad + length * i / n
        top.append((x, pad + r - wd))
        bot.append((x, pad + r + wd))
    p.poly(top + bot[::-1], material)
    if tip:
        tip(p, pad, r)
    p.shade((0, -1), 2)
    if stripe:
        stripe(p, pad, r)
    return p.outline()


def ponytail():
    def tip(p, pad, r):
        p.erase([(pad + 36, pad + r), (pad + 43, pad + r - 1), (pad + 43, pad + r + 1)])

    def stripe(p, pad, r):
        p.line([(pad + 3, pad + r + 1), (pad + 30, pad + r + 2)], "hair", 1)
        p.line([(pad + 6, pad + r - 3), (pad + 24, pad + r - 2)], "hair", 3)

    return strip(42, [4, 6, 7, 7, 6, 5, 4, 2, 1], "hair", tip=tip, stripe=stripe)


def scarf_tail():
    def tip(p, pad, r):
        p.erase([(pad + 26, pad + r), (pad + 35, pad + r - 1), (pad + 35, pad + r + 1)])

    def stripe(p, pad, r):
        p.line([(pad + 2, pad + r + 2), (pad + 26, pad + r + 3)], "scarf", 1)
        p.rect((pad + 27, pad + r - 5, pad + 28, pad + r + 5), "trim", 2)

    return strip(33, [3, 4, 4, 5, 5, 5], "scarf", tip=tip, stripe=stripe)


def scarf_knot():
    p = Part(18, 12, (9, 8))
    p.poly([(2, 3), (15, 2), (16, 7), (10, 10), (3, 8)], "scarf")
    p.shade((1, -1), 2)
    p.line([(4, 6), (13, 5)], "scarf", 1)
    return p.outline()


def skirt(length, w0, w1):
    base = strip(length, [w0, (w0 + w1) / 2, w1], "coat")
    mask = np.array(base.im) > 0
    hem = base.pivot[0] + length
    base.rect((hem - 3, 0, hem - 2, base.h - 1), "trim", 2)
    a = np.array(base.im)
    a[~mask] = 0
    base.im = Image.fromarray(a)
    base.d = ImageDraw.Draw(base.im)
    return base.outline()


def sword():
    p = Part(48, 13, (5, 6))
    p.rect((1, 5, 8, 7), "boot", 2)
    p.rect((0, 4, 1, 8), "trim", 2)
    p.rect((9, 1, 11, 11), "trim", 2)
    p.rect((10, 2, 10, 10), "trim", 3)
    p.poly([(12, 3), (40, 3), (46, 6), (40, 9), (12, 9)], "steel")
    p.line([(12, 4), (41, 4)], "steel", 3)
    p.line([(12, 6), (44, 6)], "steel", 1)
    p.line([(12, 8), (41, 8)], "glow", 2)
    p.line([(12, 9), (40, 9)], "glow", 1)
    p.outline()
    p.line([(13, 8), (42, 8)], "glow", 2)
    return p


def shadow():
    im = Image.new("RGBA", (36, 7), (0, 0, 0, 0))
    ImageDraw.Draw(im).ellipse((0, 0, 35, 6), fill=(20, 14, 30, 255))
    return im


def crescent(w, h, a0, a1, thick, colors, streaks=0, seed=1):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = im.load()
    cx, cy = w / 2, h / 2
    rx, ry = w / 2 - 1, h / 2 - 1
    rnd = np.random.RandomState(seed)
    gaps = sorted(rnd.uniform(0.15, 0.85, streaks))
    for y in range(h):
        for x in range(w):
            dx, dy = (x + 0.5 - cx) / rx, (cy - y - 0.5) / ry
            d = math.hypot(dx, dy)
            if d > 1 or d < 0.05:
                continue
            ang = math.degrees(math.atan2(dy, dx))
            span = (ang - a0) % 360
            total = (a1 - a0) % 360 or 360
            if span > total:
                continue
            u = span / total
            width = thick * math.sin(math.pi * min(1, u * 1.15)) ** 0.7
            depth = (1 - d)
            if depth > width:
                continue
            v = depth / max(width, 1e-6)
            if any(abs(v - g) < 0.07 for g in gaps):
                continue
            idx = min(len(colors) - 1, int(v * len(colors)))
            px[x, y] = (*colors[idx], 255)
    return im
