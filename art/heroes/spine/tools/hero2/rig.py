import json
import math
import os

from PIL import Image

import anims
import draw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "base", "hero2"))
EXPORT = os.path.join(OUT, "export")
NATIVE = os.path.join(OUT, "parts_native")
TEX = 4
FAR_TINT = "a9a9c4ff"
SKIN_BODY = "hero/warden"
SKIN_WEAPON = "weapon/sword"

BONES = [
    ("root", None, 0, 0, 0, 0, {}),
    ("shadow", "root", 0, 0, 0, 0, {}),
    ("fx", "root", 0, 0, 0, 0, {}),
    ("ik_foot_n", "root", -3, 4, 0, 0, {"color": "ff3f00ff"}),
    ("ik_foot_f", "root", 3, 4, 0, 0, {"color": "ff3f00ff"}),
    ("cog", "root", 0, 36, 0, 0, {}),
    ("pelvis", "cog", 0, 0, 0, 0, {}),
    ("spine", "cog", 0, 2, 90, 9, {}),
    ("chest", "spine", 9, 0, 0, 11, {}),
    ("neck", "chest", 11, 0, 0, 3, {}),
    ("head", "neck", 3, 0, 0, 24, {}),
    ("upper_arm_n", "chest", 8, 3, -180, 11, {}),
    ("forearm_n", "upper_arm_n", 11, 0, 0, 10, {}),
    ("hand_n", "forearm_n", 10, 0, 0, 5, {}),
    ("weapon", "hand_n", 3, 0, 90, 40, {}),
    ("upper_arm_f", "chest", 8, -5, -180, 11, {}),
    ("forearm_f", "upper_arm_f", 11, 0, 0, 10, {}),
    ("hand_f", "forearm_f", 10, 0, 0, 5, {}),
    ("thigh_n", "pelvis", -3, -2, -90, 15, {}),
    ("shin_n", "thigh_n", 15, 0, 0, 15, {}),
    ("foot_n", "shin_n", 15, 0, 0, 8, {"inherit": "noRotationOrReflection"}),
    ("thigh_f", "pelvis", 3, -2, -90, 15, {}),
    ("shin_f", "thigh_f", 15, 0, 0, 15, {}),
    ("foot_f", "shin_f", 15, 0, 0, 8, {"inherit": "noRotationOrReflection"}),
    ("skirt_b", "pelvis", -6, -1, -115, 13, {}),
    ("skirt_f", "pelvis", 5, -1, -70, 10, {}),
    ("scarf_1", "chest", 10, 5, 118, 11, {}),
    ("scarf_2", "scarf_1", 11, 0, 0, 11, {}),
    ("scarf_3", "scarf_2", 11, 0, 0, 11, {}),
    ("hair_lock", "head", 13, -1, -180, 12, {}),
    ("pony_1", "head", 16, 9, 142, 10.5, {}),
    ("pony_2", "pony_1", 10.5, 0, 0, 10.5, {}),
    ("pony_3", "pony_2", 10.5, 0, 0, 10.5, {}),
    ("pony_4", "pony_3", 10.5, 0, 0, 10.5, {}),
]

PHYSICS = [
    ("skirt_b", 0.6, 120, 0.8, 1.0),
    ("skirt_f", 0.6, 140, 0.8, 1.0),
    ("scarf_1", 0.75, 70, 0.85, 1.2),
    ("scarf_2", 0.8, 60, 0.85, 1.2),
    ("scarf_3", 0.85, 50, 0.85, 1.2),
    ("hair_lock", 0.5, 130, 0.8, 1.0),
    ("pony_1", 0.7, 80, 0.85, 1.2),
    ("pony_2", 0.8, 65, 0.85, 1.2),
    ("pony_3", 0.85, 55, 0.85, 1.2),
    ("pony_4", 0.9, 45, 0.85, 1.2),
]


def world_setup():
    world = {}
    for name, parent, x, y, rot, _length, extra in BONES:
        if parent is None:
            world[name] = (x, y, rot)
            continue
        px, py, pr = world[parent]
        c, s = math.cos(math.radians(pr)), math.sin(math.radians(pr))
        wr = rot if "inherit" in extra else pr + rot
        world[name] = (px + c * x - s * y, py + s * x + c * y, wr)
    return world


WORLD = world_setup()


def rotate(x, y, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return c * x - s * y, s * x + c * y


def region(name, size, pivot, bone, at=(0, 0), upright=False):
    w, h = size
    bone_rot = WORLD[bone][2]
    ang = 0 if upright else bone_rot
    cx, cy = rotate(w / 2 - pivot[0], -(h / 2 - pivot[1]), ang)
    lx, ly = rotate(at[0] + cx, at[1] + cy, -bone_rot)
    return {"path": name, "x": round(lx, 3), "y": round(ly, 3), "rotation": round(ang - bone_rot, 3), "width": w, "height": h}


def strip_mesh(name, part, chain):
    index = {b[0]: i for i, b in enumerate(BONES)}
    seg = [b[5] for b in BONES if b[0] in chain]
    pad = part.pivot[0]
    top, bottom = part.pivot[1], -(part.h - part.pivot[1])
    sections = [(-pad, [(0, -pad)])]
    start = 0.0
    for k, length in enumerate(seg):
        sections.append((start + length / 2, [(k, length / 2)]))
        if k + 1 < len(seg):
            sections.append((start + length, [(k, length), (k + 1, 0)]))
        else:
            sections.append((start + length + pad, [(k, length + pad)]))
        start += length
    count = len(sections)
    uvs, verts = [0.0] * (count * 4), [None] * (count * 2)
    for s, (u, bones) in enumerate(sections):
        for row, yl, v in ((s, top, 0.0), (2 * count - 1 - s, bottom, 1.0)):
            uvs[row * 2] = round((pad + u) / part.w, 5)
            uvs[row * 2 + 1] = v
            entry = [len(bones)]
            for k, lx in bones:
                entry += [index[chain[k]], round(lx, 3), yl, round(1 / len(bones), 3)]
            verts[row] = entry
    tris = []
    for s in range(count - 1):
        a, b, c, d = s, s + 1, 2 * count - 2 - s, 2 * count - 1 - s
        tris += [a, b, c, a, c, d]
    return {"type": "mesh", "path": name, "uvs": uvs, "triangles": tris, "vertices": [v for e in verts for v in e],
            "hull": count * 2, "width": part.w, "height": part.h}


def build_parts():
    parts = {
        "thigh": draw.thigh(), "shin": draw.shin(), "foot": draw.foot(),
        "upper_arm": draw.upper_arm(), "forearm": draw.forearm(), "hand": draw.hand(),
        "pelvis": draw.pelvis(), "waist": draw.waist(), "chest": draw.chest(), "neck": draw.neck(),
        "head": draw.head(), "hair_back": draw.hair_back(), "hair_front": draw.hair_front(),
        "hair_lock": draw.hair_lock(), "bow": draw.bow(), "ponytail": draw.ponytail(),
        "scarf_tail": draw.scarf_tail(), "scarf_knot": draw.scarf_knot(),
        "skirt_b": draw.skirt(13, 4, 7), "skirt_f": draw.skirt(10, 4, 6), "sword": draw.sword(),
    }
    for kind in ("open", "half", "closed", "fierce"):
        parts[f"eye_n_{kind}"] = draw.eye(kind)
        parts[f"eye_f_{kind}"] = draw.eye(kind, far=True)
    for kind in ("calm", "grit", "shout"):
        parts[f"mouth_{kind}"] = draw.mouth(kind)
    images = {name: p.rgba() for name, p in parts.items()}
    images["shadow"] = draw.shadow()
    white, cyan, teal = (255, 255, 255), (150, 245, 235), (40, 190, 180)
    deep = (30, 120, 170)
    for tag, size, a0, a1 in (("rise", (124, 124), -80, 78), ("slam", (156, 156), -62, 120)):
        images[f"fx_{tag}_1"] = draw.crescent(*size, a0, a1, 0.30, [white, white, cyan])
        images[f"fx_{tag}_2"] = draw.crescent(*size, a0 + 8, a1, 0.24, [cyan, cyan, teal], streaks=2, seed=3)
        images[f"fx_{tag}_3"] = draw.crescent(*size, a0 + 20, a1, 0.16, [teal, deep], streaks=4, seed=5)
        images[f"fx_{tag}_4"] = draw.crescent(*size, a0 + 40, a1, 0.08, [deep], streaks=3, seed=8)
    return parts, images


def pack(images):
    os.makedirs(EXPORT, exist_ok=True)
    os.makedirs(NATIVE, exist_ok=True)
    textures = {}
    for name, im in images.items():
        im.save(os.path.join(NATIVE, name + ".png"))
        textures[name] = im.resize((im.width * TEX, im.height * TEX), Image.NEAREST) if name.startswith(("fx_", "shadow")) else draw.texture(im)
    width, x, y, row = 1024, 2, 2, 0
    pos = {}
    for name in sorted(textures, key=lambda n: -textures[n].height):
        t = textures[name]
        if x + t.width + 2 > width:
            x, y, row = 2, y + row + 2, 0
        pos[name] = (x, y)
        x += t.width + 2
        row = max(row, t.height)
    height = 1
    while height < y + row + 2:
        height *= 2
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    lines = ["hero2.png", f"size:{width},{height}", "filter:Nearest,Nearest", "pma:false"]
    for name, (px, py) in pos.items():
        sheet.paste(textures[name], (px, py))
        lines += [name, f"bounds:{px},{py},{textures[name].width},{textures[name].height}"]
    sheet.save(os.path.join(EXPORT, "hero2.png"))
    open(os.path.join(EXPORT, "hero2.atlas"), "w").write("\n".join(lines) + "\n")


def build():
    parts, images = build_parts()
    pack(images)

    def reg(part, bone, at=(0, 0), upright=False, name=None):
        p = parts[part]
        return region(name or part, (p.w, p.h), p.pivot, bone, at, upright)

    def raw(name, bone, at=(0, 0)):
        im = images[name]
        return region(name, im.size, (im.width / 2, im.height / 2), bone, at, True)

    slots, body, weapon, default = [], {}, {}, {}

    def slot(name, bone, attachments, skin, first=None, far=False):
        entry = {"name": name, "bone": bone}
        if first:
            entry["attachment"] = first
        if far:
            entry["color"] = FAR_TINT
        slots.append(entry)
        skin[name] = attachments

    slot("shadow", "shadow", {"shadow": raw("shadow", "shadow")}, default, "shadow")
    slot("fx_back", "fx", {}, default)
    slot("scarf_tail", "scarf_1", {"scarf_tail": strip_mesh("scarf_tail", parts["scarf_tail"], ["scarf_1", "scarf_2", "scarf_3"])}, body, "scarf_tail")
    slot("ponytail", "pony_1", {"ponytail": strip_mesh("ponytail", parts["ponytail"], ["pony_1", "pony_2", "pony_3", "pony_4"])}, body, "ponytail")
    slot("hair_back", "head", {"hair_back": reg("hair_back", "head", (-1, 0), True)}, body, "hair_back")
    slot("bow", "head", {"bow": reg("bow", "head", (-10, 16), True)}, body, "bow")
    for part in ("upper_arm", "forearm", "hand"):
        slot(f"{part}_f", f"{part}_f", {part: reg(part, f"{part}_f")}, body, part, far=True)
    slot("skirt_b", "skirt_b", {"skirt_b": reg("skirt_b", "skirt_b")}, body, "skirt_b")
    for side, far in (("f", True), ("n", False)):
        slot(f"thigh_{side}", f"thigh_{side}", {"thigh": reg("thigh", f"thigh_{side}")}, body, "thigh", far=far)
        slot(f"shin_{side}", f"shin_{side}", {"shin": reg("shin", f"shin_{side}")}, body, "shin", far=far)
        slot(f"foot_{side}", f"foot_{side}", {"foot": reg("foot", f"foot_{side}", upright=True)}, body, "foot", far=far)
    slot("pelvis", "pelvis", {"pelvis": reg("pelvis", "pelvis", upright=True)}, body, "pelvis")
    slot("waist", "spine", {"waist": reg("waist", "spine", upright=True)}, body, "waist")
    slot("skirt_f", "skirt_f", {"skirt_f": reg("skirt_f", "skirt_f")}, body, "skirt_f")
    slot("neck", "neck", {"neck": reg("neck", "neck", upright=True)}, body, "neck")
    slot("chest", "chest", {"chest": reg("chest", "chest", (0, -2), True)}, body, "chest")
    slot("scarf_knot", "chest", {"scarf_knot": reg("scarf_knot", "chest", (0, 12), True)}, body, "scarf_knot")
    slot("head", "head", {"head": reg("head", "head", upright=True)}, body, "head")
    kinds = ("open", "half", "closed", "fierce")
    slot("eye_f", "head", {k: reg(f"eye_f_{k}", "head", (10, 10), True) for k in kinds}, body, "open")
    slot("eye_n", "head", {k: reg(f"eye_n_{k}", "head", (3, 10), True) for k in kinds}, body, "open")
    slot("mouth", "head", {k: reg(f"mouth_{k}", "head", (7, 3), True) for k in ("calm", "grit", "shout")}, body, "calm")
    slot("hair_front", "head", {"hair_front": reg("hair_front", "head", (0, 2), True)}, body, "hair_front")
    slot("hair_lock", "hair_lock", {"hair_lock": reg("hair_lock", "hair_lock", upright=True)}, body, "hair_lock")
    slot("weapon", "weapon", {"sword": reg("sword", "weapon")}, weapon, "sword")
    for part in ("upper_arm", "forearm", "hand"):
        slot(f"{part}_n", f"{part}_n", {part: reg(part, f"{part}_n")}, body, part)
    fx = {n: raw(n, "fx") for n in images if n.startswith("fx_")}
    slot("fx_front", "fx", fx, default)

    bones = []
    for name, parent, x, y, rot, length, extra in BONES:
        b = {"name": name}
        if parent:
            b["parent"] = parent
        for key, val in (("x", x), ("y", y), ("rotation", rot), ("length", length)):
            if val:
                b[key] = val
        b.update(extra)
        bones.append(b)

    ik = [{"name": f"leg_{s}_ik", "order": i, "bones": [f"thigh_{s}", f"shin_{s}"], "target": f"ik_foot_{s}", "bendPositive": True}
          for i, s in enumerate(("n", "f"))]
    physics = [{"name": f"{bone}_physics", "order": 2 + i, "bone": bone, "rotate": 1, "inertia": inertia, "strength": strength,
                "damping": damping, "mass": mass, "limit": 800} for i, (bone, inertia, strength, damping, mass) in enumerate(PHYSICS)]
    names = [s["name"] for s in slots]
    data = {
        "skeleton": {"spine": "4.2.00", "fps": 60, "images": "../parts_native/"},
        "bones": bones,
        "slots": slots,
        "ik": ik,
        "physics": physics,
        "skins": [{"name": "default", "attachments": default}, {"name": SKIN_BODY, "attachments": body}, {"name": SKIN_WEAPON, "attachments": weapon}],
        "events": {"hit": {}, "chain": {}, "step": {}},
        "animations": anims.build_all(),
    }
    json.dump(data, open(os.path.join(EXPORT, "hero2.json"), "w"), separators=(",", ":"))
    meta = {
        "unitsPerPixel": 1,
        "textureScale": TEX,
        "preset": [SKIN_BODY, SKIN_WEAPON],
        "layers": {"under": [0, names.index("fx_back")], "body": [names.index("scarf_tail"), names.index("hand_n")], "over": [names.index("fx_front"), names.index("fx_front")]},
        "animations": {name: {"duration": round(a["duration"], 3), "loop": a["loop"]} for name, a in anims.CATALOG.items()},
        "bones": len(bones), "slots": len(slots),
    }
    json.dump(meta, open(os.path.join(EXPORT, "hero2.meta.json"), "w"), indent=1)
    print(len(bones), "bones", len(slots), "slots", len(data["animations"]), "animations")


if __name__ == "__main__":
    build()
