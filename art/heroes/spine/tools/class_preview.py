import json
import os
import sys

from PIL import Image

CLASS_ARG = sys.argv[1] if len(sys.argv) > 1 else "runedancer"

import subprocess

import doll_diag as DG
import doll_render as DR
from doll_render import compose

OUT = os.path.join(DG.DB.ROOT, "base", "hero", "preview")
BG = (38, 32, 52, 255)
LOOK_POSES = ("setup", "twist_side", "twist_back", "arm_up", "elbow_150", "lunge", "kick", "deep_crouch", "far_arm_up", "combo_extreme")


def on_bg(img, scale):
    bg = Image.new("RGBA", img.size, BG)
    bg.alpha_composite(img)
    return bg.resize((img.width * scale, img.height * scale), Image.NEAREST)


def look_sheet(cid, scale=3):
    meta = json.load(open(os.path.join(DG.DS.EXPORT, "hero.meta.json")))
    res, _ = DG.render([{"key": cid, "skins": meta["presets"][cid]}], {p: DG.POSES[p] for p in LOOK_POSES})
    k = DG.DB.K
    box = (4 * k, 10 * k, 68 * k, 80 * k)
    cells = [on_bg(compose(res[cid][p]["layers"]).crop(box), scale) for p in LOOK_POSES]
    faults = {p: DG.faults_of(res[cid][p]) for p in LOOK_POSES if DG.faults_of(res[cid][p])}
    hero = cells[0]
    hero.save(os.path.join(OUT, f"class_{cid}.png"))
    small = [c.resize((c.width // 2, c.height // 2), Image.NEAREST) for c in cells[1:]]
    sheet = Image.new("RGBA", (small[0].width * len(small), small[0].height), BG)
    for i, c in enumerate(small):
        sheet.alpha_composite(c, (i * c.width, 0))
    sheet.save(os.path.join(OUT, f"class_{cid}_poses.png"))
    json.dump(faults, open(os.path.join(OUT, f"class_{cid}_defects.json"), "w"), indent=1)
    print("defects:", faults or "none")


VIEW = (128, 88)
CAMERA = (26, 40)
COMBO = ["Idle", "Enter", "Hit1", "Hit2", "Hit3", "Hit1", "Hit2", "Hit3", "Exit3", "Idle"]


def play_page():
    page = DR.PAGE.replace("canvas.width=96*RES; canvas.height=64*RES", f"canvas.width={VIEW[0]}*RES; canvas.height={VIEW[1]}*RES")
    page = page.replace("r.camera.position.x=18; r.camera.position.y=30; r.camera.setViewport(96,64)",
                        f"r.camera.position.x={CAMERA[0]}; r.camera.position.y={CAMERA[1]}; r.camera.setViewport({VIEW[0]},{VIEW[1]})")
    return page.replace("sd.defaultMix=0.04", "sd.defaultMix=0.03")


def play(cid, queue=None, steps=None):
    data = json.load(open(os.path.join(DG.DS.EXPORT, "hero.json")))
    meta = json.load(open(os.path.join(DG.DS.EXPORT, "hero.meta.json")))
    queue = [f"{cid}/{a}" for a in (queue or COMBO)]
    length = {}
    for name, anim in data["animations"].items():
        length[name] = max(k["time"] for tl in anim["bones"].values() for keys in tl.values() for k in keys)
    steps = steps or int(sum(length[a] for a in queue) * 60) + 4
    job = {"key": cid, "skins": meta["presets"][cid], "queue": queue, "steps": steps}
    layers = [meta["layers"][k] for k in ("back", "body", "front")]
    DR.link_export()
    open(os.path.join(DR.PROBE, "hero.html"), "w").write(play_page().replace("CFG", json.dumps({"layers": layers, "compare": [], "play": [job]})).replace("RES", str(DG.DB.K)))
    open(os.path.join(DR.PROBE, "hero_shot.js"), "w").write(DR.SHOT)
    path = os.path.join(DR.PROBE, "check", "class_play.json")
    subprocess.run(["node", "hero_shot.js", path], cwd=DR.PROBE, check=True)
    starts, t = [], 0.0
    for a in queue:
        starts.append((a, t))
        t += length[a]
    return [compose(f) for f in json.load(open(path))[cid]], starts


def fx_cues(cid, starts):
    path = os.path.join(DG.DB.ROOT, "fx", cid, "fx.json")
    if not os.path.exists(path):
        return []
    manifest = json.load(open(path))
    data = json.load(open(os.path.join(DG.DS.EXPORT, "hero.json")))
    cues, n = [], 0
    for anim, start in starts:
        for ev in data["animations"][anim].get("events", []):
            fx = manifest["effects"][ev["string"]]
            variant = manifest["variants"][n % len(manifest["variants"])]
            n += 1
            sheet = Image.open(os.path.join(DG.DB.ROOT, "fx", cid, fx["sheet"].format(variant=variant))).convert("RGBA")
            cues.append((start + ev["time"], fx, sheet))
    return cues


def fx_frame(fx, sheet, index):
    w, h = fx["frameSize"]
    x, y = (index % fx["columns"]) * w, (index // fx["columns"]) * h
    k = DG.DB.K
    return sheet.crop((x, y, x + w, y + h)).resize((w * k, h * k), Image.NEAREST)


def with_fx(frame, cues, t):
    k = DG.DB.K
    out = Image.new("RGBA", frame.size, BG)
    out.alpha_composite(frame)
    for at, fx, sheet in cues:
        index = int(round((t - at) * fx["fps"]))
        if 0 <= index < fx["frames"]:
            px = (fx["anchor"][0] - (CAMERA[0] - VIEW[0] / 2) - fx["pivot"][0]) * k
            py = (CAMERA[1] + VIEW[1] / 2 - fx["anchor"][1] - fx["pivot"][1]) * k
            layer = Image.new("RGBA", frame.size)
            layer.paste(fx_frame(fx, sheet, index), (int(px), int(py)))
            out.alpha_composite(layer)
    return out


def play_gif(cid):
    frames, starts = play(cid)
    cues = fx_cues(cid, starts)
    shots = [with_fx(f, cues, i / 30) for i, f in enumerate(frames)]
    gif = [f.convert("RGB").quantize(colors=255, method=Image.MEDIANCUT, dither=Image.NONE) for f in shots]
    gif[0].save(os.path.join(OUT, f"class_{cid}_play.gif"), save_all=True, append_images=gif[1:], duration=33, loop=0)
    hits = [i for i, (a, t) in enumerate(starts) if "/Hit" in a]
    first, last = int(starts[hits[0]][1] * 30), int(starts[hits[2]][1] * 30) + 22
    picks = shots[first:last:2]
    cols = 8
    cw, ch = shots[0].size
    sheet = Image.new("RGBA", (cols * cw, ((len(picks) + cols - 1) // cols) * ch), BG)
    for i, f in enumerate(picks):
        sheet.alpha_composite(f, ((i % cols) * cw, (i // cols) * ch))
    sheet.save(os.path.join(OUT, f"class_{cid}_frames.png"))


if __name__ == "__main__":
    look_sheet(CLASS_ARG)
    play_gif(CLASS_ARG)
