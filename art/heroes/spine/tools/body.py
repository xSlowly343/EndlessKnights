import math
from svgkit import Part, capsule, ellipse, catmull
from palette import MATERIALS, OUTLINE, SKIN_LINE, BLUSH, LASH, TEETH


def v(a, b):
    return (b[0] - a[0], b[1] - a[1])


def norm(d):
    n = math.hypot(*d)
    return (d[0] / n, d[1] / n)


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def frame(origin, along):
    d = norm(along)
    f = (-d[1], d[0])
    if f[0] < 0:
        f = (-f[0], -f[1])

    def to(u, w):
        return (origin[0] + d[0] * u + f[0] * w, origin[1] + d[1] * u + f[1] * w)
    return to


def shift(pts, dx=0.0, dy=0.0):
    return [(x + dx, y + dy) for x, y in pts]


def inflate(pts, amount):
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    out = []
    for x, y in pts:
        d = math.hypot(x - cx, y - cy) or 1
        out.append((x + (x - cx) / d * amount, y + (y - cy) / d * amount))
    return out


class Body:
    def hs(self, pts):
        k = self.hk
        return [((x - 0.4) * k + 0.4 + self.ho[0], (y - 40.5) * k + 40.5 + self.ho[1]) for x, y in pts]

    def __init__(self, sex, prof):
        self.sex = sex
        self.p = prof
        self.j = {k: tuple(v_) for k, v_ in prof["joints"].items()}
        self.r = prof["radius"]
        self.ho = tuple(prof.get("head_offset", [0, 0]))
        self.hk = prof.get("head_scale", 1.0)
        self.p = dict(prof)
        self.p["head"] = self.hs([tuple(x) for x in prof["head"]])
        face = dict(prof["face"])
        for key in ("eye_r", "eye_l", "nose", "mouth", "ear"):
            face[key] = self.hs([tuple(face[key])])[0]
        face["brow_y"] = (face["brow_y"] - 40.5) * self.hk + 40.5 + self.ho[1]
        self.p["face"] = face
        self.parts = []

    def new(self, name, ow=1.0):
        return Part(name, MATERIALS, OUTLINE, ow)

    def add(self, part, slot, layer, tint=1.0, pivots=None):
        self.parts.append({"part": part, "slot": slot, "layer": layer, "tint": tint, "pivots": pivots or {}})

    def side_tint(self, side):
        return 1.0 if side == "r" else 0.82

    def leg_shapes(self, side, grow=0.0):
        hip, knee, ankle = self.j[f"hip_{side}"], self.j[f"knee_{side}"], self.j[f"ankle_{side}"]
        t0, t1 = self.r["thigh"]
        s0, s1 = self.r["shin"]
        thigh = capsule(hip, knee, t0 + grow, t1 + grow)
        shin = capsule(knee, ankle, s0 + grow, s1 + grow)
        shapes = [(catmull(thigh, tension=0.2), thigh), (catmull(shin, tension=0.2), shin)]
        if self.r["calf"] > 0:
            c = lerp(knee, ankle, 0.32)
            calf = ellipse((c[0] - s0 * 0.45, c[1]), s0 * 0.75 + self.r["calf"] + grow, 2.4 + grow)
            shapes.append((catmull(calf), calf))
        return shapes

    def arm_shapes(self, side, grow=0.0, upper_to=1.0, fore_from=0.0, fore_to=1.0, include_upper=True, include_fore=True):
        sh, el, wr = self.j[f"shoulder_{side}"], self.j[f"elbow_{side}"], self.j[f"wrist_{side}"]
        u0, u1 = self.r["upper_arm"]
        f0, f1 = self.r["forearm"]
        shapes = []
        if include_upper:
            e = lerp(sh, el, upper_to)
            ue = u0 + (u1 - u0) * upper_to
            up = capsule(sh, e, u0 + grow, ue + grow)
            shapes.append((catmull(up, tension=0.2), up))
            if self.r["bicep"] > 0 and upper_to > 0.6:
                c = lerp(sh, el, 0.45)
                b = ellipse((c[0] + 0.25, c[1]), u0 * 0.8 + self.r["bicep"] + grow, 2.1 + grow)
                shapes.append((catmull(b), b))
        if include_fore:
            a, b_ = lerp(el, wr, fore_from), lerp(el, wr, fore_to)
            ra, rb = f0 + (f1 - f0) * fore_from, f0 + (f1 - f0) * fore_to
            fo = capsule(a, b_, ra + grow, rb + grow)
            shapes.append((catmull(fo, tension=0.2), fo))
        return shapes

    def foot_pts(self, side, grow=0.0):
        ax, ay = self.j[f"ankle_{side}"]
        f = self.p["foot"]
        h, toe, heel = f["height"], f["toe"], f["heel"]
        pts = [(ax - heel, ay + 0.2), (ax + 0.6, ay + 0.6), (ax + toe * 0.55, h * 0.75), (ax + toe, h * 0.32),
               (ax + toe + 0.1, 0.05), (ax + toe * 0.4, -0.05), (ax - heel - 0.15, 0.0), (ax - heel - 0.35, h * 0.5)]
        return inflate(pts, grow) if grow else pts

    def build_legs(self):
        for side in ("l", "r"):
            p = self.new(f"leg_{side}")
            p.group(self.leg_shapes(side), self.p["skin"])
            knee = self.j[f"knee_{side}"]
            p.line([(knee[0] + 0.75, knee[1] + 0.45), (knee[0] + 0.95, knee[1] - 0.15)], SKIN_LINE, 0.45, smooth=False)
            self.add(p, f"leg_{side}", 20 if side == "l" else 22, self.side_tint(side),
                     {"hip": self.j[f"hip_{side}"], "knee": knee, "ankle": self.j[f"ankle_{side}"]})
            fp = self.new(f"foot_{side}")
            fp.smooth(self.foot_pts(side), self.p["skin"], shade=0.6, light=0.4)
            ax, ay = self.j[f"ankle_{side}"]
            self.add(fp, f"foot_{side}", 21 if side == "l" else 23, self.side_tint(side),
                     {"ankle": (ax, ay), "toe": (ax + self.p["foot"]["toe"] * 0.62, 0.6)})

    def build_arms(self):
        for side in ("l", "r"):
            p = self.new(f"arm_{side}")
            p.group(self.arm_shapes(side), self.p["skin"])
            el = self.j[f"elbow_{side}"]
            p.line([(el[0] - 0.9, el[1] + 0.2), (el[0] - 0.3, el[1] - 0.1)], SKIN_LINE, 0.5)
            layer = 10 if side == "l" else 80
            self.add(p, f"arm_{side}", layer, self.side_tint(side),
                     {"shoulder": self.j[f"shoulder_{side}"], "elbow": el, "wrist": self.j[f"wrist_{side}"]})
            self.build_hands(side, layer + 1)

    def build_hands(self, side, layer):
        el, wr = self.j[f"elbow_{side}"], self.j[f"wrist_{side}"]
        to = frame(wr, v(el, wr))
        k = 1.0 if self.sex == "male" else 0.88
        skin = self.p["skin"]
        tint = self.side_tint(side)

        relaxed = self.new(f"hand_{side}_relaxed")
        palm = [to(-0.4 * k, -0.95 * k), to(0.7 * k, -1.15 * k), to(2.2 * k, -1.05 * k), to(3.1 * k, -0.5 * k), to(3.3 * k, 0.3 * k),
                to(2.8 * k, 0.95 * k), to(1.6 * k, 1.05 * k), to(0.4 * k, 1.0 * k), to(-0.4 * k, 0.85 * k)]
        relaxed.smooth(palm, skin, shade=0.55, light=0.35)
        thumb = capsule(to(0.8 * k, 0.75 * k), to(2.1 * k, 1.35 * k), 0.5 * k, 0.4 * k)
        relaxed.smooth(thumb, skin, shade=0.4, light=0.3)
        relaxed.line([to(2.0 * k, -0.6 * k), to(2.9 * k, -0.2 * k)], SKIN_LINE, 0.45)
        self.add(relaxed, f"hand_{side}", layer, tint, {"wrist": wr})

        fist = self.new(f"hand_{side}_fist")
        ball = ellipse(to(1.55 * k, 0.05 * k), 1.5 * k, 1.35 * k, rot=math.atan2(*reversed(norm(v(el, wr)))))
        fist.smooth(ball, skin, shade=0.55, light=0.35)
        fist.smooth(capsule(to(0.7 * k, 0.85 * k), to(2.0 * k, 1.0 * k), 0.45 * k, 0.42 * k), skin, shade=0.35, light=0.3)
        for u in (1.2, 1.8, 2.4):
            fist.line([to(u * k, -1.0 * k), to(u * k, -0.35 * k)], SKIN_LINE, 0.4)
        self.add(fist, f"hand_{side}", layer, tint, {"wrist": wr})

        back = self.new(f"hand_{side}_grip_back")
        back.smooth(ellipse(to(1.5 * k, -0.25 * k), 1.35 * k, 1.15 * k), skin, shade=0.55, light=0.3)
        self.add(back, f"hand_{side}", layer, tint, {"wrist": wr, "grip": to(1.6 * k, 0.0)})
        front = self.new(f"hand_{side}_grip_front")
        band = [to(0.9 * k, -0.2 * k), to(2.4 * k, -0.2 * k), to(2.5 * k, 0.6 * k), to(2.1 * k, 1.35 * k), to(1.0 * k, 1.35 * k), to(0.7 * k, 0.6 * k)]
        front.smooth(band, skin, shade=0.45, light=0.3)
        front.line([to(1.45 * k, 0.1 * k), to(1.45 * k, 1.1 * k)], SKIN_LINE, 0.4)
        front.line([to(1.95 * k, 0.1 * k), to(1.95 * k, 1.1 * k)], SKIN_LINE, 0.4)
        self.add(front, f"hand_{side}_front", layer + 2, tint, {"wrist": wr, "grip": to(1.6 * k, 0.0)})

        openp = self.new(f"hand_{side}_open")
        openp.smooth([to(-0.4 * k, -0.95 * k), to(2.2 * k, -1.05 * k), to(2.4 * k, 0.0), to(2.2 * k, 1.0 * k), to(-0.4 * k, 0.9 * k)], skin, shade=0.5, light=0.3)
        self.add(openp, f"hand_{side}", layer, tint, {"wrist": wr})
        fingers = {"index": (0.85, 1.75), "middle": (0.28, 1.95), "ring": (-0.28, 1.8), "pinky": (-0.82, 1.45)}
        for name, (w, length) in fingers.items():
            base = to(2.1 * k, w * k)
            tip = to((2.1 + length) * k, w * 1.15 * k)
            fp = self.new(f"finger_{side}_{name}", ow=0.55)
            fp.smooth(capsule(base, tip, 0.36 * k, 0.3 * k), skin, shade=0.3, light=0.0)
            joints = {f"{name}_{i + 1}": lerp(base, tip, i / 3) for i in range(3)}
            joints[f"{name}_tip"] = tip
            self.add(fp, f"finger_{side}_{name}", layer + (1 if name in ("index", "middle") else -1), tint, joints)
        tb, tt = to(0.7 * k, 0.85 * k), to(1.9 * k, 2.0 * k)
        tp = self.new(f"finger_{side}_thumb", ow=0.55)
        tp.smooth(capsule(tb, tt, 0.45 * k, 0.33 * k), skin, shade=0.3, light=0.0)
        tj = {f"thumb_{i + 1}": lerp(tb, tt, i / 3) for i in range(3)}
        tj["thumb_tip"] = tt
        self.add(tp, f"finger_{side}_thumb", layer + 1, tint, tj)

    def build_torso(self):
        t = self.new("torso")
        pts = [tuple(x) for x in self.p["torso"]]
        t.smooth(pts, self.p["skin"], shade=0.8, light=0.4)
        ny = pts[0][1]
        t.line([(-0.6, ny - 0.9), (-2.4, ny - 0.5)], SKIN_LINE, 0.5)
        t.line([(0.8, ny - 0.9), (2.6, ny - 0.5)], SKIN_LINE, 0.5)
        if self.sex == "male":
            t.line([(-3.4, 27.6), (-1.8, 26.9), (-0.4, 27.3)], SKIN_LINE, 0.5)
            t.line([(0.6, 27.3), (2.0, 26.9), (3.6, 27.6)], SKIN_LINE, 0.5)
            t.line([(0.1, 26.2), (0.1, 23.6)], SKIN_LINE, 0.45)
            t.line([(0.1, 21.9), (0.1, 21.4)], SKIN_LINE, 0.6)
        else:
            t.line([(-3.2, 26.5), (-1.9, 25.6), (-0.5, 26.2)], SKIN_LINE, 0.55)
            t.line([(0.7, 26.2), (2.1, 25.6), (3.4, 26.5)], SKIN_LINE, 0.55)
            t.line([(0.1, 21.4), (0.1, 20.9)], SKIN_LINE, 0.6)
        self.add(t, "torso", 30, 1.0, {k: self.j[k] for k in ("pelvis", "spine_lower", "spine_mid", "chest", "neck")})
        n = self.new("neck")
        nb, hb = self.j["neck"], self.j["head"]
        neck = capsule((nb[0], nb[1] - 0.6), (hb[0], hb[1] + 0.8), self.r["neck"], self.r["neck"] * 0.95)
        n.smooth(neck, self.p["skin"], shade=0.7, light=0.0)
        self.add(n, "neck", 28, 1.0, {"neck": nb, "head": hb})

    def build_head(self):
        f = self.p["face"]
        h = self.new("head")
        h.smooth(self.p["head"], self.p["skin"], shade=0.7, light=0.35)
        self.add(h, "head", 50, 1.0, {"head": self.j["head"], "jaw": (self.j["head"][0] + 1.2, self.j["head"][1] + 2.2)})
        e = self.new("ear")
        ex, ey = f["ear"]
        e.smooth(ellipse((ex, ey), 1.05, 1.55), self.p["skin"], shade=0.5, light=0.3)
        e.line([(ex + 0.3, ey + 0.6), (ex - 0.1, ey), (ex + 0.3, ey - 0.6)], SKIN_LINE, 0.4)
        self.add(e, "ear", 51, 1.0, {"ear": (ex, ey)})
        if f["blush"]:
            b = self.new("blush", ow=0)
            b.flat(ellipse((f["eye_r"][0] - 0.9, f["eye_r"][1] - 2.6), 0.9, 0.45), BLUSH)
            b.flat(ellipse((f["eye_l"][0] + 0.7, f["eye_l"][1] - 2.6), 0.6, 0.42), BLUSH)
            self.add(b, "blush", 57)
        self.build_eyes()
        self.build_mouth()
        nx, ny = f["nose"]
        n = self.new("nose", ow=0)
        n.line([(nx, ny + 0.35), (nx + 0.25, ny - 0.3), (nx - 0.2, ny - 0.45)], SKIN_LINE, 0.5, smooth=False)
        self.add(n, "nose", 55, 1.0, {"nose": (nx, ny)})

    def build_eyes(self):
        f = self.p["face"]
        iris = self.p["iris"]
        lash_w = 0.95 if f["lashes"] else 0.75
        for idx, side in enumerate(("r", "l")):
            cx, cy = f[f"eye_{side}"]
            w = f["eye_w"][idx]
            hgt = f["eye_h"]
            for state in ("open", "half", "closed", "squint"):
                p = self.new(f"eye_{side}_{state}", ow=0)
                if state in ("open", "half", "squint"):
                    top = {"open": cy + hgt / 2, "half": cy + 0.1, "squint": cy - 0.25}[state]
                    bottom = cy - hgt / 2
                    rect = [(cx - w / 2, top), (cx + w / 2, top), (cx + w / 2, bottom + 0.4), (cx + w / 2 - 0.3, bottom), (cx - w / 2 + 0.3, bottom), (cx - w / 2, bottom + 0.4)]
                    p.poly(rect, "sclera", shade=0.0, light=0.0, outline=False)
                    ir = [(cx - w / 2 + 0.25, top), (cx + w / 2 - 0.05, top), (cx + w / 2 - 0.05, bottom + 0.2), (cx - w / 2 + 0.25, bottom + 0.2)]
                    p.poly(ir, iris, shade=0.0, light=0.0, outline=False, tone=1)
                    lower = [(cx - w / 2 + 0.25, cy - 0.2), (cx + w / 2 - 0.05, cy - 0.2), (cx + w / 2 - 0.05, bottom + 0.2), (cx - w / 2 + 0.25, bottom + 0.2)]
                    if top > cy - 0.2:
                        p.poly(lower, iris, shade=0.0, light=0.0, outline=False, tone=2)
                    if state == "open":
                        p.flat(ellipse((cx - w / 2 + 0.75, top - 0.8), 0.38, 0.45), MATERIALS["sclera"][3])
                    p.line([(cx - w / 2 - 0.25, top + 0.05), (cx + w / 2 + 0.25, top + 0.15 + (0.25 if f["lashes"] else 0))], LASH, lash_w, smooth=False)
                    if f["lashes"]:
                        p.line([(cx + w / 2, top + 0.35), (cx + w / 2 + 0.5, top + 0.8)], LASH, 0.5, smooth=False)
                elif state == "closed":
                    p.line([(cx - w / 2 - 0.2, cy - 0.2), (cx, cy - 0.65), (cx + w / 2 + 0.25, cy - 0.15)], LASH, lash_w)
                self.add(p, f"eye_{side}", 56, 1.0, {"eye": (cx, cy)})
            b = self.new(f"brow_{side}", ow=0)
            by = f["brow_y"]
            bw = w + 0.4
            pts = [(cx - bw / 2, by - 0.15), (cx, by + 0.25), (cx + bw / 2, by + 0.05)]
            b.line(pts, MATERIALS[self.p["hair"]][0], f["brow_w"])
            self.add(b, f"brow_{side}", 58, 1.0, {"brow": (cx, by)})

    def build_mouth(self):
        mx, my = self.p["face"]["mouth"]
        dark = MATERIALS["mouth"][0]
        shapes = {
            "neutral": lambda p: p.line([(mx - 0.7, my), (mx + 0.6, my + 0.05)], dark, 0.7, smooth=False),
            "smile": lambda p: p.line([(mx - 0.9, my + 0.3), (mx - 0.1, my - 0.25), (mx + 0.8, my + 0.35)], dark, 0.7),
            "frown": lambda p: p.line([(mx - 0.8, my - 0.3), (mx, my + 0.15), (mx + 0.7, my - 0.25)], dark, 0.7),
            "open": lambda p: (p.flat(ellipse((mx, my - 0.3), 0.8, 0.85), dark), p.flat(ellipse((mx + 0.1, my - 0.75), 0.5, 0.3), MATERIALS["mouth"][2])),
            "o": lambda p: p.flat(ellipse((mx, my - 0.1), 0.5, 0.6), dark),
            "grit": lambda p: (p.flat([(mx - 0.95, my + 0.35), (mx + 0.9, my + 0.4), (mx + 0.8, my - 0.5), (mx - 0.85, my - 0.5)], dark, smooth=False),
                               p.flat([(mx - 0.75, my + 0.2), (mx + 0.7, my + 0.25), (mx + 0.65, my - 0.1), (mx - 0.7, my - 0.1)], TEETH, smooth=False)),
        }
        for name, draw in shapes.items():
            p = self.new(f"mouth_{name}", ow=0)
            draw(p)
            self.add(p, "mouth", 57, 1.0, {"mouth": (mx, my)})

    def hair_pivots(self, pivots):
        return {k: self.hs([v_])[0] for k, v_ in pivots.items()}

    def build_hair(self):
        hair = self.p["hair"]
        light = MATERIALS[hair][3]
        if self.sex == "male":
            front = [(-6.4, 37.4), (-7.0, 41.0), (-6.8, 44.6), (-7.3, 46.6), (-5.6, 47.4), (-5.0, 49.3), (-3.0, 48.9), (-1.6, 50.6), (0.2, 49.6),
                     (1.3, 51.7), (2.2, 49.6), (4.4, 49.4), (6.2, 47.8), (7.2, 45.2), (7.4, 42.4), (7.0, 39.8), (6.5, 41.4), (5.8, 40.4), (5.0, 41.9),
                     (3.9, 40.3), (3.0, 42.1), (1.8, 40.5), (0.7, 42.3), (-0.6, 40.7), (-1.8, 41.9), (-2.6, 39.6), (-3.6, 37.6), (-4.8, 36.8), (-5.8, 36.6)]
            back = [(-6.6, 36.0), (-7.4, 40.0), (-7.0, 44.6), (-4.6, 47.6), (-1.0, 48.4), (-1.0, 40.0), (-3.0, 35.0)]
            p = self.new("hair_back")
            p.smooth(self.hs(back), hair, shade=0.8, light=0.0)
            self.add(p, "hair_back", 5, 1.0, self.hair_pivots({"hair_root": (0.4, 46.0)}))
            p = self.new("hair_front")
            p.smooth(self.hs(front), hair, shade=0.8, light=0.4)
            p.line(self.hs([(-3.6, 47.4), (-0.6, 48.6), (2.6, 48.4)]), light, 0.5)
            self.add(p, "hair_front", 60, 1.0, self.hair_pivots({"hair_root": (0.4, 46.0), "bangs": (2.5, 42.0)}))
        else:
            front = [(-6.4, 35.2), (-7.0, 40.0), (-6.6, 44.4), (-4.8, 47.4), (-1.6, 49.0), (1.6, 49.2), (4.6, 48.0), (6.6, 45.6), (7.4, 42.4),
                     (7.4, 38.6), (7.0, 35.6), (6.4, 37.4), (6.2, 40.0), (5.6, 41.8), (4.8, 40.2), (3.8, 42.0), (2.6, 40.0), (1.4, 41.8), (0.2, 40.2),
                     (-1.0, 41.8), (-2.2, 39.8), (-3.2, 37.0), (-4.4, 35.0), (-5.4, 34.2)]
            back = [(-6.6, 44.0), (-7.6, 39.0), (-7.8, 33.0), (-7.4, 27.0), (-6.6, 23.0), (-5.0, 21.4), (-3.2, 21.8), (-1.4, 24.0), (1.0, 24.4),
                    (3.2, 23.0), (5.0, 22.0), (6.4, 23.6), (7.2, 27.4), (7.6, 32.0), (7.8, 37.0), (7.4, 42.0), (5.4, 46.6), (1.2, 48.6), (-3.2, 48.2)]
            side_l = [(-7.0, 41.5), (-5.2, 41.0), (-5.0, 36.0), (-5.3, 32.5), (-5.8, 29.6), (-6.6, 29.4), (-7.2, 32.0), (-7.4, 36.5)]
            side_r = [(6.4, 42.0), (7.8, 41.0), (8.0, 36.0), (7.6, 32.0), (6.9, 29.8), (6.2, 31.0), (6.6, 35.5), (6.2, 39.5)]
            p = self.new("hair_back")
            p.smooth(self.hs(back), hair, shade=0.8, light=0.0)
            p.line(self.hs([(-6.6, 38.0), (-6.8, 30.0), (-5.8, 24.0)]), MATERIALS[hair][1], 0.5)
            p.line(self.hs([(6.6, 36.0), (6.6, 28.0), (5.6, 24.4)]), MATERIALS[hair][1], 0.5)
            self.add(p, "hair_back", 5, 1.0, self.hair_pivots({"hair_root": (0.4, 46.0), "hair_back_1": (0.0, 42.0), "hair_back_2": (0.0, 35.0), "hair_back_3": (0.0, 28.0), "hair_back_tip": (0.0, 22.0)}))
            p = self.new("hair_side_r")
            p.smooth(self.hs(side_r), hair, shade=0.6, light=0.0)
            self.add(p, "hair_side_r", 45, 1.0, self.hair_pivots({"hair_side_r_1": (7.1, 41.0), "hair_side_r_2": (7.3, 36.0), "hair_side_r_3": (7.1, 32.0), "hair_side_r_tip": (6.6, 30.0)}))
            p = self.new("hair_front")
            p.smooth(self.hs(front), hair, shade=0.8, light=0.4)
            p.line(self.hs([(-3.6, 46.8), (-0.4, 48.2), (2.8, 47.8)]), light, 0.5)
            self.add(p, "hair_front", 60, 1.0, self.hair_pivots({"hair_root": (0.4, 46.0), "bangs": (2.5, 42.0)}))
            p = self.new("hair_side_l")
            p.smooth(self.hs(side_l), hair, shade=0.6, light=0.4)
            self.add(p, "hair_side_l", 62, 1.0, self.hair_pivots({"hair_side_l_1": (-6.2, 41.0), "hair_side_l_2": (-6.2, 36.0), "hair_side_l_3": (-6.3, 32.0), "hair_side_l_tip": (-6.2, 29.6)}))

    def build_under(self):
        mat = "under"
        if self.sex == "male":
            pts = [(-4.65, 21.3), (4.65, 21.3), (4.8, 18.5), (4.4, 15.6), (1.0, 15.0), (0.0, 16.0), (-1.0, 15.0), (-4.4, 15.6), (-4.8, 18.5)]
        else:
            pts = [(-3.8, 21.0), (3.8, 21.0), (4.8, 18.4), (4.4, 16.0), (1.0, 15.6), (0.0, 16.2), (-1.0, 15.6), (-4.4, 16.0), (-4.8, 18.4)]
            top = [(-4.4, 28.4), (-1.5, 28.6), (0.0, 27.6), (1.5, 28.6), (4.3, 28.4), (4.0, 25.3), (0.0, 24.9), (-4.0, 25.3)]
            t = self.new("under_top")
            t.smooth(top, mat, shade=0.8, light=0.5)
            self.add(t, "under_top", 33, 1.0)
        b = self.new("under_bottom")
        b.smooth(pts, mat, shade=0.8, light=0.5)
        self.add(b, "under_bottom", 32, 1.0)

    def build_outfit(self):
        male = self.sex == "male"
        cloth = "crimson" if male else "violet"
        trim = MATERIALS["gold"][2]
        tp = [tuple(x) for x in self.p["torso"]]
        top = tp[0][1]
        hem = 15.0 if male else 14.6
        sh = max(abs(x) for x, y in tp if y > top - 3.0)
        waist = 4.3 if male else 3.5
        flare = 5.0 if male else 5.7
        pts = [(-1.6, top + 0.2), (0.1, top - 3.4), (1.8, top + 0.2), (sh - 1.2, top - 0.4), (sh + 0.2, top - 1.5), (sh, top - 3.9), (waist + 0.5, 24.2),
               (waist + 0.3, 22.4), (waist + 0.9, 19.8), (flare - 0.4, 17.4), (flare, hem + 0.6), (2.8, hem), (0.2, hem + 0.4), (-2.6, hem), (-flare, hem + 0.6),
               (-flare + 0.4, 17.4), (-waist - 0.9, 19.8), (-waist - 0.3, 22.4), (-waist - 0.5, 24.2), (-sh, top - 3.9), (-sh - 0.2, top - 1.5), (-sh + 1.2, top - 0.4)]
        t = self.new("tunic")
        t.smooth(pts, cloth, shade=1.0, light=0.55)
        t.line([(0.1, top - 3.4), (0.15, hem + 0.5)], MATERIALS[cloth][0], 0.5)
        t.line([(-flare + 0.3, hem + 1.0), (-2.6, hem + 0.5), (0.2, hem + 0.9), (2.8, hem + 0.5), (flare - 0.3, hem + 1.0)], trim, 0.55)
        t.line([(-1.5, top - 0.2), (0.1, top - 3.0), (1.7, top - 0.2)], trim, 0.5)
        self.add(t, "torso_cloth", 34, 1.0)
        belt = self.new("belt")
        by = 21.4 if male else 21.0
        bw = waist + 0.55
        belt.poly([(-bw, by + 0.85), (bw, by + 0.85), (bw + 0.1, by - 0.8), (-bw - 0.1, by - 0.8)], "leather", shade=0.5, light=0.4)
        belt.poly([(0.0, by + 0.95), (1.4, by + 0.95), (1.4, by - 0.9), (0.0, by - 0.9)], "steel", shade=0.4, light=0.4)
        belt.flat([(0.45, by + 0.4), (0.95, by + 0.4), (0.95, by - 0.35), (0.45, by - 0.35)], MATERIALS["leather"][0], smooth=False)
        pouch = [(-3.6, by - 0.6), (-1.8, by - 0.6), (-1.7, by - 2.6), (-3.7, by - 2.6)]
        belt.poly(pouch, "leather", shade=0.5, light=0.3)
        self.add(belt, "belt", 36, 1.0)
        for side in ("l", "r"):
            tint = self.side_tint(side)
            arm_layer = 10 if side == "l" else 80
            s = self.new(f"sleeve_{side}")
            s.group(self.arm_shapes(side, grow=0.35, upper_to=0.5, include_fore=False), cloth, shade=0.9, light=0.5)
            sh_j, el_j = self.j[f"shoulder_{side}"], self.j[f"elbow_{side}"]
            a = lerp(sh_j, el_j, 0.5)
            s.line([(a[0] - 1.7, a[1] - 0.4), (a[0] + 1.7, a[1] - 0.3)], trim, 0.5)
            self.add(s, f"sleeve_{side}", arm_layer + 0.5, tint)
            br = self.new(f"bracer_{side}")
            br.group(self.arm_shapes(side, grow=0.3, include_upper=False, fore_from=0.35, fore_to=0.95), "leather", shade=0.8, light=0.5)
            el, wr = self.j[f"elbow_{side}"], self.j[f"wrist_{side}"]
            for tt in (0.55, 0.78):
                c = lerp(el, wr, tt)
                br.line([(c[0] - 1.5, c[1] + 0.2), (c[0] + 1.5, c[1] - 0.1)], MATERIALS["leather"][0], 0.45)
            self.add(br, f"bracer_{side}", arm_layer + 0.6, tint)
            leg_layer = 20 if side == "l" else 22
            pn = self.new(f"pants_{side}")
            pn.group(self.leg_shapes(side, grow=0.25), "slate", shade=0.9, light=0.5)
            self.add(pn, f"pants_{side}", leg_layer + 0.5, tint)
            bt = self.new(f"boot_{side}")
            ankle, knee = self.j[f"ankle_{side}"], self.j[f"knee_{side}"]
            s0, s1 = self.r["shin"]
            top_t = 0.55 if male else 0.7
            shaft_top = lerp(ankle, knee, top_t)
            shaft = capsule((ankle[0], ankle[1] - 0.6), shaft_top, s1 + 0.45, s1 + (s0 - s1) * top_t + 0.5)
            foot = self.foot_pts(side, grow=0.35)
            bt.group([(catmull(shaft, tension=0.2), shaft), (catmull(foot), foot)], "leather", shade=0.8, light=0.5)
            cuff_r = s1 + (s0 - s1) * top_t + 0.6
            cuff = [(shaft_top[0] - cuff_r, shaft_top[1] + 0.5), (shaft_top[0] + cuff_r, shaft_top[1] + 0.5), (shaft_top[0] + cuff_r, shaft_top[1] - 0.7), (shaft_top[0] - cuff_r, shaft_top[1] - 0.7)]
            bt.poly(cuff, "leather", shade=0.4, light=0.4, tone=2)
            bt.line([(ankle[0] - 1.6, 0.35), (ankle[0] + self.p["foot"]["toe"] + 0.3, 0.35)], MATERIALS["leather"][0], 0.5, smooth=False)
            self.add(bt, f"boot_{side}", leg_layer + 1.5, tint)

    def build_base(self):
        self.build_legs()
        self.build_arms()
        self.build_torso()
        self.build_head()
        self.build_hair()
        self.build_under()
        return self

    def build_all(self):
        self.build_base()
        self.build_outfit()
        return self
