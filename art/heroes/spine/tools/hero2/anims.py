import math

FPS = 60
CATALOG = {}

EASE = {
    "lin": lambda t: t,
    "in": lambda t: t * t,
    "in3": lambda t: t * t * t,
    "out": lambda t: 1 - (1 - t) ** 2,
    "out3": lambda t: 1 - (1 - t) ** 3,
    "io": lambda t: t * t * (3 - 2 * t),
    "back": lambda t: 1 + 2.2 * (t - 1) ** 3 + 1.2 * (t - 1) ** 2,
    "hold": lambda t: 0.0 if t < 1 else 1.0,
}

DEFAULTS = {"sq_x": 1.0, "sq_y": 1.0, "wsx": 1.0, "sw": 0.0, "sh": 1.0}

ROTATE = {
    "cog_r": "cog", "pelvis": "pelvis", "spine": "spine", "chest": "chest", "neck": "neck", "head": "head",
    "ua_n": "upper_arm_n", "fa_n": "forearm_n", "hd_n": "hand_n", "wp": "weapon",
    "ua_f": "upper_arm_f", "fa_f": "forearm_f", "hd_f": "hand_f",
    "fn_r": "foot_n", "ff_r": "foot_f",
    "skb": "skirt_b", "skf": "skirt_f", "lock": "hair_lock",
    "pony": "pony_1", "scarf": "scarf_1",
}
CURL = {"pony_c": ["pony_2", "pony_3", "pony_4"], "scarf_c": ["scarf_2", "scarf_3"]}
TRANSLATE = {"cog": ("cog_x", "cog_y"), "ik_foot_n": ("fn_x", "fn_y"), "ik_foot_f": ("ff_x", "ff_y"), "shadow": ("cog_x", None)}
SCALE = {"cog": ("sq_x", "sq_y"), "weapon": ("wsx", None), "shadow": ("sh", "sh")}

STANCE = {
    "cog_x": -2, "cog_y": -6, "spine": -8, "chest": -5, "neck": 3, "head": 9,
    "fn_x": -9, "ff_x": 10,
    "ua_n": -38, "fa_n": 28, "sw": -152,
    "ua_f": 30, "fa_f": 62, "hd_f": 10,
    "pony": 6, "pony_c": -6, "scarf": 4, "scarf_c": -5, "skb": -4, "skf": 4,
}


def pose(*layers):
    p = {}
    for layer in layers:
        p.update(layer)
    return p


def track(keys):
    full, current = [], {}
    for t, ease, values in keys:
        current = pose(current, values)
        full.append((t, ease, dict(current)))
    return full


def sample(full, t):
    if t <= full[0][0]:
        return dict(full[0][2])
    for (t0, _e, a), (t1, ease, b) in zip(full, full[1:]):
        if t <= t1:
            k = EASE[ease]((t - t0) / (t1 - t0)) if t1 > t0 else 1.0
            names = set(a) | set(b)
            return {n: a.get(n, DEFAULTS.get(n, 0.0)) + (b.get(n, DEFAULTS.get(n, 0.0)) - a.get(n, DEFAULTS.get(n, 0.0))) * k for n in names}
    return dict(full[-1][2])


def bake(name, duration, keys, loop=False, layer=None, slots=None, fx=None, events=None):
    full = track(keys)
    steps = int(round(duration * FPS))
    frames = []
    for i in range(steps + 1):
        t = i / FPS
        p = sample(full, t)
        if layer:
            layer(t, p)
        get = lambda n: p.get(n, DEFAULTS.get(n, 0.0))
        p["wp"] = get("sw") - sum(get(n) for n in ("cog_r", "spine", "chest", "ua_n", "fa_n", "hd_n"))
        p["sh"] = max(0.45, 1 - max(0.0, get("cog_y") + 6) / 50)
        frames.append((round(t, 4), p))
    bones = {}

    def channel(n):
        return [f[1].get(n, DEFAULTS.get(n, 0.0)) for f in frames]

    def add(bone, kind, keys_out):
        bones.setdefault(bone, {})[kind] = keys_out

    def thin(values, build):
        out = []
        for i, v in enumerate(values):
            if 0 < i < len(values) - 1 and values[i - 1] == v == values[i + 1]:
                continue
            out.append(build(frames[i][0], v))
        return out

    for ch, bone in ROTATE.items():
        vals = [round(v, 2) for v in channel(ch)]
        if any(vals):
            add(bone, "rotate", thin(vals, lambda t, v: {"time": t, "value": v}))
    for ch, chain in CURL.items():
        vals = [round(v, 2) for v in channel(ch)]
        if any(vals):
            for bone in chain:
                add(bone, "rotate", thin(vals, lambda t, v: {"time": t, "value": v}))
    for bone, (cx, cy) in TRANSLATE.items():
        xs = channel(cx) if cx else [0.0] * len(frames)
        ys = channel(cy) if cy else [0.0] * len(frames)
        pairs = [(round(x, 2), round(y, 2)) for x, y in zip(xs, ys)]
        if any(a or b for a, b in pairs):
            add(bone, "translate", thin(pairs, lambda t, v: {"time": t, "x": v[0], "y": v[1]}))
    for bone, (cx, cy) in SCALE.items():
        xs = channel(cx) if cx else [1.0] * len(frames)
        ys = channel(cy) if cy else [1.0] * len(frames)
        pairs = [(round(x, 3), round(y, 3)) for x, y in zip(xs, ys)]
        if any(a != 1 or b != 1 for a, b in pairs):
            add(bone, "scale", thin(pairs, lambda t, v: {"time": t, "x": v[0], "y": v[1]}))
    slot_tl = {}
    for slot, items in (slots or {}).items():
        slot_tl[slot] = {"attachment": [{"time": t, "name": n} for t, n in items]}
    for item in fx or []:
        t0, dt = item["t"], item.get("dt", 0.05)
        keys_fx = [{"time": 0, "name": None}] if t0 > 0 else []
        for i, n in enumerate(item["frames"]):
            keys_fx.append({"time": round(t0 + i * dt, 4), "name": n})
        keys_fx.append({"time": round(t0 + len(item["frames"]) * dt, 4), "name": None})
        slot_tl[item.get("slot", "fx_front")] = {"attachment": keys_fx}
        fxb = bones.setdefault("fx", {})
        fxb["translate"] = [{"time": 0, "x": item["x"], "y": item["y"], "curve": "stepped"}]
        fxb["rotate"] = [{"time": 0, "value": item.get("rot", 0), "curve": "stepped"}]
        end = round(t0 + len(item["frames"]) * dt, 4)
        grow = item.get("grow", 1.12)
        fxb["scale"] = [{"time": 0, "x": item.get("sx", 1), "y": item.get("sy", 1), "curve": "stepped"},
                        {"time": t0, "x": item.get("sx", 1), "y": item.get("sy", 1)},
                        {"time": end, "x": item.get("sx", 1) * grow, "y": item.get("sy", 1) * grow}]
    anim = {"bones": bones}
    if slot_tl:
        anim["slots"] = slot_tl
    if events:
        anim["events"] = [{"time": t, "name": n} for t, n in events]
    CATALOG[name] = {"duration": duration, "loop": loop, "data": anim}
    return anim


def breathe(period, amount=1.0):
    def layer(t, p):
        s = math.sin(2 * math.pi * t / period)
        c = math.sin(2 * math.pi * t / period - 0.9)
        p["cog_y"] = p.get("cog_y", 0) + 1.3 * s * amount
        p["chest"] = p.get("chest", 0) + 2.0 * c * amount
        p["head"] = p.get("head", 0) - 2.2 * c * amount
        p["ua_n"] = p.get("ua_n", 0) + 3.0 * c * amount
        p["sw"] = p.get("sw", 0) + 4.0 * s * amount
        p["ua_f"] = p.get("ua_f", 0) - 3.0 * c * amount
        p["fa_f"] = p.get("fa_f", 0) + 4.0 * s * amount
        p["sq_y"] = p.get("sq_y", 1) + 0.012 * c * amount
        p["pony"] = p.get("pony", 0) + 4.0 * c * amount
        p["pony_c"] = p.get("pony_c", 0) + 3.0 * s * amount
        p["scarf"] = p.get("scarf", 0) + 5.0 * s * amount
        p["scarf_c"] = p.get("scarf_c", 0) + 6.0 * c * amount
    return layer


def idle():
    return bake("Idle", 2.4, [(0, "lin", STANCE), (2.4, "lin", {})], loop=True, layer=breathe(1.2),
                slots={"eye_n": [(0, "open"), (1.7, "half"), (1.75, "closed"), (1.83, "half"), (1.88, "open")],
                       "eye_f": [(0, "open"), (1.7, "half"), (1.75, "closed"), (1.83, "half"), (1.88, "open")]})


FIERCE = {"eye_n": [(0, "fierce")], "eye_f": [(0, "fierce")]}


def face(shout_at, shout_end):
    return pose(FIERCE, {"mouth": [(0, "grit"), (shout_at, "shout"), (shout_end, "grit")]})


def hit1():
    keys = [
        (0, "lin", STANCE),
        (0.09, "out", {"cog_x": -6, "cog_y": -11, "spine": -18, "chest": -12, "head": 18, "ua_n": -62, "fa_n": 14, "sw": -176,
                       "ua_f": 8, "fa_f": 80, "sq_y": 0.95, "sq_x": 1.04, "fn_x": -10, "pony": 20, "scarf": 14}),
        (0.12, "in", {"cog_x": 2, "cog_y": -8, "spine": -8, "chest": 0, "ua_n": 20, "fa_n": 6, "sw": -70, "wsx": 1.3, "sq_y": 1.0, "sq_x": 1.0}),
        (0.15, "lin", {"cog_x": 15, "cog_y": -8, "spine": 4, "chest": 14, "head": -6, "ua_n": 118, "fa_n": 12, "sw": 72, "wsx": 1.15,
                       "ff_x": 25, "fn_x": -5, "fn_y": 2, "fn_r": -32, "ua_f": -40, "fa_f": 30, "sq_y": 1.05, "sq_x": 0.97,
                       "pony": -28, "pony_c": -12, "scarf": -24, "scarf_c": -10, "skb": -18, "skf": -14}),
        (0.21, "out", {"cog_x": 17, "cog_y": -6, "spine": 9, "chest": 15, "ua_n": 140, "sw": 108, "wsx": 1.0, "sq_y": 1.02, "sq_x": 1.0}),
        (0.30, "io", {"cog_x": 14, "cog_y": -7, "ua_n": 132, "sw": 100}),
        (0.52, "io", pose(STANCE, {"fn_y": 0, "fn_r": 0, "wsx": 1.0, "sq_x": 1.0, "sq_y": 1.0})),
    ]
    return bake("Hit1", 0.52, keys, slots=face(0.12, 0.3), events=[(0.15, "hit"), (0.3, "chain")],
                fx=[{"t": 0.12, "frames": ["fx_rise_1", "fx_rise_2", "fx_rise_3", "fx_rise_4"], "dt": 0.04, "x": 16, "y": 46}])


def hit2():
    keys = [
        (0, "lin", STANCE),
        (0.09, "out", {"cog_x": -3, "cog_y": -3, "spine": 8, "chest": 10, "head": -2, "ua_n": 196, "fa_n": 42, "sw": 150,
                       "ua_f": 70, "fa_f": 20, "sq_y": 1.04, "sq_x": 0.97, "fn_x": -8, "ff_x": 8, "ff_y": 3, "pony": -14, "scarf": -10}),
        (0.12, "in", {"cog_x": 4, "cog_y": -7, "spine": -8, "chest": -2, "ua_n": 128, "fa_n": 14, "sw": 58, "wsx": 1.3, "ff_x": 14, "ff_y": 2}),
        (0.15, "lin", {"cog_x": 16, "cog_y": -14, "spine": -28, "chest": -14, "head": 24, "ua_n": 58, "fa_n": 4, "sw": -38, "wsx": 1.15,
                       "ff_x": 26, "ff_y": 0, "fn_x": -8, "fn_y": 2, "fn_r": -34, "ua_f": -50, "fa_f": 40, "sq_y": 0.95, "sq_x": 1.04,
                       "pony": 30, "pony_c": 12, "scarf": 26, "scarf_c": 10, "skb": 16, "skf": 10}),
        (0.21, "out", {"cog_y": -15, "spine": -32, "ua_n": 44, "sw": -62, "wsx": 1.0, "sq_y": 0.97, "sq_x": 1.02}),
        (0.30, "io", {"cog_y": -13, "spine": -28, "sw": -56, "sq_y": 1.0, "sq_x": 1.0}),
        (0.52, "io", pose(STANCE, {"fn_y": 0, "fn_r": 0, "ff_y": 0, "wsx": 1.0, "sq_x": 1.0, "sq_y": 1.0})),
    ]
    return bake("Hit2", 0.52, keys, slots=face(0.12, 0.3), events=[(0.15, "hit"), (0.3, "chain")],
                fx=[{"t": 0.12, "frames": ["fx_rise_1", "fx_rise_2", "fx_rise_3", "fx_rise_4"], "dt": 0.04, "x": 16, "y": 40, "sy": -1}])


def hit3():
    keys = [
        (0, "lin", STANCE),
        (0.10, "out", {"cog_x": -3, "cog_y": -15, "spine": -22, "chest": -10, "head": 26, "ua_n": -55, "fa_n": 20, "sw": -165,
                       "ua_f": -10, "fa_f": 60, "sq_y": 0.9, "sq_x": 1.08, "pony": 22, "scarf": 16}),
        (0.20, "out3", {"cog_x": 5, "cog_y": 20, "spine": 14, "chest": 14, "head": -10, "ua_n": 205, "fa_n": 48, "sw": 138,
                        "ua_f": 120, "fa_f": 30, "sq_y": 1.1, "sq_x": 0.93,
                        "fn_x": -6, "fn_y": 24, "fn_r": -40, "ff_x": 10, "ff_y": 30, "ff_r": -20,
                        "pony": -30, "pony_c": -14, "scarf": -26, "scarf_c": -12, "skb": -20, "skf": -16}),
        (0.30, "io", {"cog_x": 8, "cog_y": 23, "ua_n": 212, "sw": 148, "sq_y": 1.0, "sq_x": 1.0, "fn_y": 27, "ff_y": 32,
                      "pony": 4, "scarf": 4, "skb": 0, "skf": 0}),
        (0.34, "in", {"cog_x": 11, "cog_y": 6, "spine": -6, "chest": 0, "ua_n": 130, "fa_n": 16, "sw": 50, "wsx": 1.35,
                      "fn_y": 10, "ff_y": 10, "ff_x": 18, "fn_x": -10, "sq_y": 1.1, "sq_x": 0.93}),
        (0.37, "lin", {"cog_x": 15, "cog_y": -18, "spine": -36, "chest": -18, "head": 34, "ua_n": 52, "fa_n": 2, "sw": -64, "wsx": 1.2,
                       "fn_x": -9, "fn_y": 0, "fn_r": 0, "ff_x": 26, "ff_y": 0, "ff_r": 0, "ua_f": -60, "fa_f": 50,
                       "sq_y": 0.86, "sq_x": 1.12, "pony": 44, "pony_c": 16, "scarf": 36, "scarf_c": 14, "skb": 24, "skf": 16}),
        (0.45, "back", {"cog_y": -15, "sq_y": 1.0, "sq_x": 1.0, "wsx": 1.0, "sw": -60}),
        (0.60, "io", {"cog_y": -14, "spine": -32}),
        (0.84, "io", pose(STANCE, {"fn_y": 0, "fn_r": 0, "ff_y": 0, "ff_r": 0, "wsx": 1.0, "sq_x": 1.0, "sq_y": 1.0})),
    ]
    return bake("Hit3", 0.84, keys, slots=face(0.2, 0.5), events=[(0.37, "hit"), (0.55, "chain")],
                fx=[{"t": 0.33, "frames": ["fx_slam_1", "fx_slam_1", "fx_slam_2", "fx_slam_3", "fx_slam_4"], "dt": 0.04, "x": 14, "y": 42, "grow": 1.06}])


def flourish():
    keys = [
        (0, "lin", STANCE),
        (0.10, "out", {"cog_y": -3, "spine": -2, "chest": 4, "ua_n": 60, "fa_n": 50, "sw": -152 - 80, "ua_f": 10, "fa_f": 40}),
        (0.34, "lin", {"ua_n": 70, "fa_n": 60, "sw": -152 - 620}),
        (0.46, "out", {"cog_y": -4, "ua_n": 10, "fa_n": 40, "sw": -152 - 720}),
        (0.70, "back", pose(STANCE, {"sw": -152 - 720})),
    ]
    return bake("Flourish", 0.70, keys, slots={"eye_n": [(0, "half"), (0.5, "open")], "eye_f": [(0, "half"), (0.5, "open")]})


def run():
    period = 0.42

    def layer(t, p):
        ph = 2 * math.pi * t / period
        for side, off in (("fn", 0.0), ("ff", math.pi)):
            a = ph + off
            p[side + "_x"] = 13 * math.cos(a) + 1
            p[side + "_y"] = max(0.0, 10 * math.sin(a)) + (3 if math.sin(a) > 0 else 0)
            p[side + "_r"] = -30 * math.sin(a) if math.sin(a) > 0 else 8 * math.cos(a)
        p["cog_y"] = -7 + 2.2 * abs(math.sin(ph))
        p["sq_y"] = 1 + 0.03 * math.sin(2 * ph)
        p["chest"] = -8 + 5 * math.sin(ph)
        p["ua_f"] = 20 + 40 * math.cos(ph)
        p["fa_f"] = 70 + 15 * math.cos(ph)
        p["ua_n"] = -50 - 10 * math.cos(ph)
        p["sw"] = -168 + 6 * math.sin(2 * ph)
        p["head"] = 22 - 3 * math.sin(2 * ph)
        p["pony_c"] = -4 + 5 * math.sin(2 * ph)
        p["scarf_c"] = -4 + 7 * math.sin(2 * ph + 1)

    base = {"cog_x": 2, "spine": -24, "neck": 4, "fa_n": 20, "pony": -34, "scarf": -30, "skb": -22, "skf": -16}
    return bake("Run", period * 2, [(0, "lin", base), (period * 2, "lin", {})], loop=True, layer=layer,
                events=[(0.0, "step"), (period / 2, "step"), (period, "step"), (period * 1.5, "step")])


def cast():
    keys = [
        (0, "lin", STANCE),
        (0.12, "out", {"cog_x": -5, "cog_y": -9, "spine": -2, "chest": 10, "ua_f": -40, "fa_f": 90, "ua_n": -50, "sw": -140, "head": 4}),
        (0.20, "in", {"cog_x": 5, "cog_y": -4, "spine": -14, "chest": -10, "ua_f": 96, "fa_f": 6, "hd_f": 20, "head": 16,
                      "ff_x": 14, "sq_y": 1.04, "pony": -18, "scarf": -16, "skb": -10}),
        (0.26, "out", {"cog_x": 7, "ua_f": 104, "sq_y": 1.0}),
        (0.50, "io", {"cog_x": 5, "ua_f": 98}),
        (0.72, "io", STANCE),
    ]
    return bake("Cast", 0.72, keys, slots=pose(FIERCE, {"mouth": [(0, "grit"), (0.2, "shout"), (0.45, "calm")]}), events=[(0.2, "hit")])


def hurt():
    closed = [(0, "closed"), (0.26, "half"), (0.34, "open")]
    keys = [
        (0, "lin", STANCE),
        (0.05, "out3", {"cog_x": -9, "cog_y": -3, "spine": 16, "chest": 12, "head": -16, "ua_n": -10, "fa_n": 50, "sw": -120,
                        "ua_f": 80, "fa_f": 30, "fn_x": -13, "ff_x": 6, "ff_y": 4, "ff_r": 20, "sq_x": 0.94, "sq_y": 1.05,
                        "pony": -36, "scarf": -30, "skb": -16, "skf": -14}),
        (0.16, "io", {"cog_x": -7, "cog_y": -8, "spine": 6, "ff_y": 0, "ff_r": 0, "sq_x": 1.0, "sq_y": 1.0}),
        (0.40, "io", STANCE),
    ]
    return bake("Hurt", 0.40, keys, slots={"eye_n": closed, "eye_f": closed, "mouth": [(0, "shout"), (0.3, "calm")]})


def build_all():
    CATALOG.clear()
    for make in (idle, hit1, hit2, hit3, flourish, run, cast, hurt):
        make()
    return {name: a["data"] for name, a in CATALOG.items()}
