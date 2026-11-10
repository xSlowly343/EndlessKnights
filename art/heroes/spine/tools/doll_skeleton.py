import json
import math
import os
import sys

from PIL import Image

import doll_base as DB
from doll_spine_check import capture

sys.argv = sys.argv[:1] + [DB.DOLL_SRC]
import rig3 as R
import combo4 as C4
from rig3 import lerp

EXPORT = os.path.join(DB.ROOT, "base", "hero", "export")
GROUND = 62
HIP_X = 30
SIDES = {"near": "F", "far": "B"}
FAR_TINT = "e6e6e6ff"
LIMB, UPRIGHT, LEVEL = -90.0, 90.0, 0.0

SLOTS = [
    ("back", "cape", "cape", "cape", LIMB),
    ("back", "back_item", "back_item", "back_item", UPRIGHT),
    ("back", "thigh_far", "thigh_far", "thigh", LIMB),
    ("back", "knee_far", "shin_far", "knee_cap", LIMB),
    ("back", "shin_far", "shin_far", "shin", LIMB),
    ("back", "foot_far", "foot_far", "foot", LEVEL),
    ("back", "upper_arm_far", "upper_arm_far", "upper_arm", LIMB),
    ("back", "pauldron_deco_far", "upper_arm_far", "pauldron_deco", LIMB),
    ("back", "elbow_far", "forearm_far", "elbow_cap", LIMB),
    ("back", "forearm_far", "forearm_far", "forearm", LIMB),
    ("back", "bracer_far", "forearm_far", "bracer", LIMB),
    ("back", "hand_far", "hand_far", "hand", LIMB),
    ("back", "coat_tail", "coat_tail", "coat_tail", LIMB),
    ("back", "hair_fly", "hair_fly", "hair_fly", LIMB),
    ("body", "pelvis", "pelvis", "pelvis", LEVEL),
    ("body", "thigh_near", "thigh_near", "thigh", LIMB),
    ("body", "knee_near", "shin_near", "knee_cap", LIMB),
    ("body", "shin_near", "shin_near", "shin", LIMB),
    ("body", "foot_near", "foot_near", "foot", LEVEL),
    ("body", "torso", "torso", "torso", UPRIGHT),
    ("body", "belt_front", "belt", "belt_front", UPRIGHT),
    ("body", "belt_hang", "belt_hang", "belt_hang", LIMB),
    ("body", "head", "head", "head", UPRIGHT),
    ("body", "blush", "head", "blush", UPRIGHT),
    ("body", "eye_far", "head", "eye_open", UPRIGHT),
    ("body", "eye_near", "head", "eye_open", UPRIGHT),
    ("body", "hair_front", "head", "hair_front", UPRIGHT),
    ("body", "helmet", "head", "helmet", UPRIGHT),
    ("body", "crest", "crest", "crest", UPRIGHT),
    ("front", "offhand", "offhand", "offhand", LEVEL),
    ("front", "weapon", "weapon", "weapon", UPRIGHT),
    ("front", "upper_arm_near", "upper_arm_near", "upper_arm", LIMB),
    ("front", "pauldron_deco_near", "upper_arm_near", "pauldron_deco", LIMB),
    ("front", "elbow_near", "forearm_near", "elbow_cap", LIMB),
    ("front", "forearm_near", "forearm_near", "forearm", LIMB),
    ("front", "bracer_near", "forearm_near", "bracer", LIMB),
    ("front", "hand_near", "hand_near", "hand", LIMB),
]


SHOULDERS_SETUP = {side: -(DB.SHOULDERS["torso"][i] - 5) for i, side in enumerate(("near", "far"))}


def to_spine(p):
    return (p[0] - HIP_X, GROUND - p[1])


def pose_values(doll, pose):
    calls, _ = capture(doll, dict(pose))
    k = {key: (pos, ang) for _, key, _, pos, ang in calls}
    view = next(v for v in ("torso", "torso_side", "torso_back") if v in k)
    T = k[view][1]
    Hh = k["head"][1]
    short = "uF_short" in k
    v = {"view": view, "short": short, "hip": to_spine(k["pelvis"][0]), "torso": UPRIGHT + T, "head": Hh - T}
    for side, s in SIDES.items():
        a1 = k["uF_short" if (s == "F" and short) else "u" + s][1]
        a2 = k["f" + s][1]
        v[f"upper_arm_{side}"] = -180 + a1 - T
        v[f"forearm_{side}"] = a2 - a1
        v[f"forearm_{side}_x"] = 4.0 if (s == "F" and short) else 7.0
        sx = DB.SHOULDERS[view][0 if s == "F" else 1]
        v[f"shoulder_{side}"] = (11.5, -(sx - 5))
        fpos = k["ft" + s][0]
        v[f"ik_foot_{side}"] = to_spine((fpos[0], fpos[1] - 0.5))
        v[f"thigh_{side}"] = LIMB + k["th" + s][1]
        v[f"shin_{side}"] = k["sh" + s][1] - k["th" + s][1]
        if s == "F":
            v["weapon"] = 180 + k["w"][1] - a2
    v["coat_tail"] = -180 + k["coat"][1] - T
    v["hair_fly"] = k["hairfly"][1] - 180 - Hh
    return v


def rotate(vec, deg):
    r = math.radians(deg)
    return (vec[0] * math.cos(r) - vec[1] * math.sin(r), vec[0] * math.sin(r) + vec[1] * math.cos(r))


def bones(setup):
    hip_off = {"near": (2.5, -1.0), "far": (-3.0, -1.0)}
    b = [{"name": "root"},
         {"name": "hip", "parent": "root", "x": setup["hip"][0], "y": setup["hip"][1]},
         {"name": "pelvis", "parent": "hip"},
         {"name": "anchor_hip", "parent": "pelvis", "x": -3.5, "y": -2.0},
         {"name": "torso", "parent": "hip", "rotation": setup["torso"], "length": 13},
         {"name": "anchor_back", "parent": "torso", "x": 7.0, "y": 0.0},
         {"name": "coat_tail", "parent": "torso", "x": 3.0, "y": 4.5, "rotation": setup["coat_tail"], "length": 8},
         {"name": "cape", "parent": "torso", "x": 12.0, "y": 0.5, "rotation": 174, "length": 26},
         {"name": "head", "parent": "torso", "x": 13, "rotation": setup["head"], "length": 12},
         {"name": "crest", "parent": "head", "x": 16.0, "y": 0.0},
         {"name": "back_item", "parent": "torso", "x": 9.0, "y": 6.5, "rotation": 6},
         {"name": "belt", "parent": "torso", "x": 4.0, "y": 0.0},
         {"name": "belt_hang", "parent": "torso", "x": 3.5, "y": 2.5, "rotation": 180, "length": 6},
         {"name": "hair_fly", "parent": "head", "x": 8.0, "y": 6.0, "rotation": setup["hair_fly"], "length": 8}]
    for side in ("far", "near"):
        sx, sy = setup[f"shoulder_{side}"]
        b += [{"name": f"shoulder_{side}", "parent": "torso", "x": sx, "y": sy},
              {"name": f"upper_arm_{side}", "parent": f"shoulder_{side}", "rotation": setup[f"upper_arm_{side}"], "length": 7},
              {"name": f"forearm_{side}", "parent": f"upper_arm_{side}", "x": setup[f"forearm_{side}_x"], "rotation": setup[f"forearm_{side}"], "length": 4},
              {"name": f"hand_{side}", "parent": f"forearm_{side}", "x": 4, "length": 2}]
        if side == "near":
            b.append({"name": "weapon", "parent": "hand_near", "rotation": setup["weapon"], "length": 10})
        else:
            b.append({"name": "offhand", "parent": "hand_far", "inherit": "noRotationOrReflection"})
        hx, hy = hip_off[side]
        ix, iy = setup[f"ik_foot_{side}"]
        b += [{"name": f"thigh_{side}", "parent": "hip", "x": hx, "y": hy, "rotation": setup[f"thigh_{side}"], "length": 8},
              {"name": f"shin_{side}", "parent": f"thigh_{side}", "x": 8, "rotation": setup[f"shin_{side}"], "length": 8},
              {"name": f"foot_{side}", "parent": f"shin_{side}", "x": 8, "inherit": "noRotationOrReflection"},
              {"name": f"ik_foot_{side}", "parent": "root", "x": ix, "y": iy}]
    return b


FOOT_DROP = 0.55


def region(path, meta, base_rot, drop=0.0):
    k = DB.K
    w, h = meta["size"]
    px, py = meta["pivot"]
    vx, vy = (w / 2 - (px + 0.5)) / k, -(h / 2 - (py + 0.5)) / k - drop
    lx, ly = rotate((vx, vy), -base_rot)
    return {"path": path, "x": round(lx, 4), "y": round(ly, 4), "rotation": -base_rot, "width": w / k, "height": h / k}


def skins(manifest):
    att = manifest["attachments"]
    by_skin = {}
    for path, meta in att.items():
        cat, name, leaf = path.split("/")
        by_skin.setdefault(f"{cat}/{name}", {})[leaf] = (path, meta)
    out = []
    for skin, leaves in sorted(by_skin.items()):
        slots = {}
        for layer, slot, bone, placeholder, base_rot in SLOTS:
            for leaf, (path, meta) in leaves.items():
                target = None
                if slot.startswith("eye_"):
                    side = slot.split("_")[1]
                    if leaf.startswith(f"eye_{side}_"):
                        target = "eye_" + leaf.split("_")[-1]
                elif slot == "torso" and leaf in ("torso", "torso_side", "torso_back"):
                    target = leaf
                elif slot == "upper_arm_near" and leaf in ("upper_arm", "upper_arm_short"):
                    target = leaf
                elif leaf == placeholder:
                    target = leaf
                if target:
                    drop = FOOT_DROP if slot.startswith("foot_") else 0.0
                    slots.setdefault(slot, {})[target] = region(path, meta, base_rot, drop)
        out.append({"name": skin, "attachments": slots})
    return out


def unwrap(prev, val):
    if prev is None:
        return val
    while val - prev > 180:
        val -= 360
    while val - prev < -180:
        val += 360
    return val


ROT_BONES = ["torso", "head", "upper_arm_near", "upper_arm_far", "forearm_near", "forearm_far", "weapon"]
POS_BONES = ["hip", "ik_foot_near", "ik_foot_far"]
STEP_BONES = ["shoulder_near", "shoulder_far"]


def animation(frames, setup, loop):
    times, t = [], 0.0
    for _, ms in frames:
        times.append(round(t, 4))
        t += ms / 1000
    end = round(t, 4)
    vals = [v for v, _ in frames]
    if loop:
        vals = vals + [vals[0]]
    else:
        vals = vals + [vals[-1]]
    times = times + [end]
    bones_tl = {}
    for b in ROT_BONES:
        prev, keys = None, []
        for tt, v in zip(times, vals):
            val = unwrap(prev, v[b] - setup[b])
            prev = val
            keys.append({"time": tt, "value": round(val, 3)})
        bones_tl.setdefault(b, {})["rotate"] = keys
    for b in POS_BONES:
        bones_tl.setdefault(b, {})["translate"] = [{"time": tt, "x": round(v[b][0] - setup[b][0], 3), "y": round(v[b][1] - setup[b][1], 3)} for tt, v in zip(times, vals)]
    for b in STEP_BONES:
        bones_tl.setdefault(b, {})["translate"] = [{"time": tt, "x": round(v[b][0] - setup[b][0], 3), "y": round(v[b][1] - setup[b][1], 3), "curve": "stepped"} for tt, v in zip(times, vals)]
    bones_tl.setdefault("forearm_near", {})["translate"] = [{"time": tt, "x": round(v["forearm_near_x"] - setup["forearm_near_x"], 3), "curve": "stepped"} for tt, v in zip(times, vals)]
    slots_tl = {"torso": {"attachment": [{"time": tt, "name": v["view"]} for tt, v in zip(times, vals)]},
                "upper_arm_near": {"attachment": [{"time": tt, "name": "upper_arm_short" if v["short"] else "upper_arm"} for tt, v in zip(times, vals)]}}
    return {"bones": bones_tl, "slots": slots_tl}


def v8_sequences(doll):
    seqs = {"Idle": ([(P, 90) for P in R.idle_seq()], True)}
    for name, seq in C4.build(doll).items():
        seqs[name] = ([(C4.strip_fx(P), ms) for P, ms in seq], False)
    return seqs


def pack_atlas(manifest, name):
    paths = sorted(manifest["attachments"])
    ims = {p: Image.open(os.path.join(DB.OUT, "parts_tex", p + ".png")) for p in paths}
    W, x, y, row, pad = 2048, 0, 0, 0, 2
    pos = {}
    for p, im in sorted(ims.items(), key=lambda kv: (-kv[1].height, kv[0])):
        if x + im.width > W:
            x, y, row = 0, y + row + pad, 0
        pos[p] = (x, y)
        x += im.width + pad
        row = max(row, im.height)
    H = 1 << (y + row - 1).bit_length()
    page = Image.new("RGBA", (W, H))
    for p, (px, py) in pos.items():
        page.alpha_composite(ims[p], (px, py))
    page.save(os.path.join(EXPORT, name + ".png"))
    lines = [name + ".png", f"size:{W},{H}", "filter:Nearest,Nearest", "pma:false"]
    for p in paths:
        lines += [p, f"bounds:{pos[p][0]},{pos[p][1]},{ims[p].width},{ims[p].height}"]
    open(os.path.join(EXPORT, name + ".atlas"), "w").write("\n".join(lines) + "\n")


def build():
    os.makedirs(EXPORT, exist_ok=True)
    manifest = json.load(open(os.path.join(DB.OUT, "attachments.json")))
    doll = R.Doll({"weapon": "sword"})
    setup = pose_values(doll, R.BASE_IDLE)
    slots = []
    for layer, slot, bone, placeholder, base_rot in SLOTS:
        e = {"name": slot, "bone": bone, "attachment": placeholder}
        if slot.endswith("_far") or slot in ("coat_tail", "cape"):
            e["color"] = FAR_TINT
        slots.append(e)
    skel = {
        "skeleton": {"spine": "4.2.00", "fps": 60, "images": "../parts_tex/"},
        "bones": bones(setup),
        "slots": slots,
        "ik": [{"name": f"leg_{side}_ik", "order": i, "bones": [f"thigh_{side}", f"shin_{side}"], "target": f"ik_foot_{side}", "bendPositive": False}
               for i, side in enumerate(("far", "near"))],
        "physics": [
            {"name": "hair_fly_physics", "order": 2, "bone": "hair_fly", "rotate": 1, "inertia": 0.5, "strength": 90, "damping": 0.75, "mass": 1, "limit": 600},
            {"name": "coat_tail_physics", "order": 3, "bone": "coat_tail", "rotate": 1, "inertia": 0.6, "strength": 110, "damping": 0.8, "mass": 1, "limit": 600},
            {"name": "back_item_physics", "order": 5, "bone": "back_item", "rotate": 1, "inertia": 0.5, "strength": 140, "damping": 0.8, "mass": 1, "limit": 600},
            {"name": "belt_hang_physics", "order": 6, "bone": "belt_hang", "rotate": 1, "inertia": 0.8, "strength": 60, "damping": 0.7, "mass": 1, "limit": 600},
            {"name": "cape_physics", "order": 4, "bone": "cape", "rotate": 1, "inertia": 0.7, "strength": 70, "damping": 0.85, "mass": 1.4, "limit": 600},
        ],
        "skins": [{"name": "default", "attachments": {}}] + skins(manifest),
        "animations": {},
    }
    for name, (frames, loop) in v8_sequences(doll).items():
        vals = [(pose_values(doll, P), ms) for P, ms in frames]
        skel["animations"][name] = animation(vals, setup, loop)
    with open(os.path.join(EXPORT, "hero.json"), "w") as f:
        json.dump(skel, f, indent=1)
    pack_atlas(manifest, "hero")
    layers = {}
    for i, (layer, slot, *_rest) in enumerate(SLOTS):
        layers.setdefault(layer, [i, i])[1] = i
    presets = {"male": ["body/male", "hair/short_a", "eyes/a_male", "outfit/a", "weapon/sword"],
               "female": ["body/female", "hair/long_b", "eyes/b_female", "outfit/b", "weapon/axe"]}
    json.dump({"layers": layers, "outline": manifest["outline"], "presets": presets}, open(os.path.join(EXPORT, "hero.meta.json"), "w"), indent=1)
    print("bones", len(skel["bones"]), "slots", len(slots), "skins", len(skel["skins"]), "animations", list(skel["animations"]))


if __name__ == "__main__":
    build()
