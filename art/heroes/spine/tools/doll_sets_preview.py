import json
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

LINE_ARG = sys.argv[1] if len(sys.argv) > 1 else "forge"

import doll_diag as DG
import doll_render as DR
from doll_render import compose

OUT = os.path.join(DG.DB.ROOT, "base", "hero", "preview")
FONT = ImageFont.truetype(os.path.join(DG.DB.ROOT, "..", "..", "..", "Assets", "Game", "Art", "Fonts", "Rubik-Regular.ttf"), 14)
CHECK = ("setup", "arm_up", "elbow_150", "lunge", "kick", "deep_crouch", "twist_side", "twist_back", "fk_legs_split", "combo_extreme")


def items_json():
    path = os.path.join(DG.DB.ROOT, "..", "..", "..", "Assets", "StreamingAssets", "Data", "items.json")
    return {s["id"]: s for s in json.load(open(path))["sets"]}


def set_skins(set_id):
    return [f"helmet/{set_id}", f"chest/{set_id}", f"gloves/{set_id}", f"boots/{set_id}"]


def sheet(rows, res, path, scale=1.5, box=(0, 0, 80, 80)):
    k = DG.DB.K
    box = tuple(v * k for v in box)
    w, h = int((box[2] - box[0]) * scale), int((box[3] - box[1]) * scale)
    cols = max(len(r) for _, r in rows)
    out = Image.new("RGBA", (cols * w, len(rows) * (h + 20)), (38, 32, 52, 255))
    d = ImageDraw.Draw(out)
    for ri, (title, items) in enumerate(rows):
        for ci, (label, key, pose) in enumerate(items):
            img = compose(res[key][pose]["layers"]).crop(box)
            x, y = ci * w, ri * (h + 20)
            out.alpha_composite(img.resize((int(img.width * scale), int(img.height * scale)), Image.NEAREST), (x, y + 20))
            d.text((x + 4, y + 2), label, fill=(255, 220, 120), font=FONT)
    out.save(path)


def main(line, ids):
    meta = json.load(open(os.path.join(DG.DS.EXPORT, "hero.meta.json")))
    names = items_json()
    jobs, rows = [], []
    for sex in ("male", "female"):
        items = []
        for sid in ids:
            key = f"{sex}_{sid}"
            jobs.append({"key": key, "skins": meta["presets"][sex] + set_skins(sid)})
            items.append((names[sid]["name"]["ru"], key, "setup"))
        rows.append((sex, items))
    res, _ = DG.render(jobs, {p: DG.POSES[p] for p in CHECK})
    faults = {}
    for j in jobs:
        for p in CHECK:
            f = DG.faults_of(res[j["key"]][p])
            if f:
                faults.setdefault(j["key"], {})[p] = f
    os.makedirs(OUT, exist_ok=True)
    sheet(rows, res, os.path.join(OUT, f"line_{line}.png"), scale=2)
    pose_rows = [(sid, [(p, f"male_{sid}", p) for p in CHECK]) for sid in ids]
    sheet(pose_rows, res, os.path.join(OUT, f"line_{line}_poses.png"), scale=1, box=(0, 8, 80, 80))
    json.dump(faults, open(os.path.join(OUT, f"line_{line}_defects.json"), "w"), indent=1)
    layers = [meta["layers"][k] for k in ("back", "body", "front")]
    play = [{"key": f"{sex}_{sid}", "skins": meta["presets"][sex] + set_skins(sid), "queue": ["Idle", "Enter", "Hit1", "Hit2", "Hit3", "Exit3", "Idle"], "steps": 170}
            for sex, sid in (("male", ids[2]), ("female", ids[3]))]
    open(os.path.join(DR.PROBE, "hero.html"), "w").write(DR.PAGE.replace("CFG", json.dumps({"layers": layers, "compare": [], "play": play})).replace("RES", str(DG.DB.K)))
    open(os.path.join(DR.PROBE, "hero_shot.js"), "w").write(DR.SHOT)
    path = os.path.join(DR.PROBE, "check", "sets_play.json")
    subprocess.run(["node", "hero_shot.js", path], cwd=DR.PROBE, check=True)
    playres = json.load(open(path))
    for job in play:
        frames = []
        for f in playres[job["key"]]:
            img = compose(f)
            bg = Image.new("RGBA", img.size, (38, 32, 52, 255))
            bg.alpha_composite(img)
            frames.append(bg.resize((img.width * 2, img.height * 2), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
        frames[0].save(os.path.join(OUT, f"line_{line}_play_{job['key']}.gif"), save_all=True, append_images=frames[1:], duration=33, loop=0, disposal=2)
    print("defects:", json.dumps(faults) if faults else "none")


if __name__ == "__main__":
    import importlib
    main(LINE_ARG, list(importlib.import_module(f"{LINE_ARG}2x").LINE))
