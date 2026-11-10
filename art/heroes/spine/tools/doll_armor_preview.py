import json
import os

from PIL import Image, ImageDraw

import doll_diag as DG
from doll_render import compose

OUT = os.path.join(DG.DB.ROOT, "base", "hero", "preview")
MATERIALS = ("plate_early", "plate_late", "leather_early", "leather_late", "cloth_early", "cloth_late")
SLOTS = ("helmet", "chest", "gloves", "boots")
CHECK_POSES = ("setup", "arm_up", "elbow_150", "lunge", "kick", "deep_crouch", "twist_side", "twist_back", "fk_legs_split", "combo_extreme")


def armor(material, rarity, slots=SLOTS):
    return [f"{s}/{material}_r{rarity}" for s in slots]


def sheet(rows, res, path, scale=3, box=(0, 0, 72, 80)):
    w, h = (box[2] - box[0]) * scale, (box[3] - box[1]) * scale
    cols = max(len(r[1]) for r in rows)
    out = Image.new("RGBA", (cols * w, len(rows) * (h + 14)), (38, 32, 52, 255))
    d = ImageDraw.Draw(out)
    for ri, (title, items) in enumerate(rows):
        for ci, (label, key, pose) in enumerate(items):
            img = compose(res[key][pose]["layers"]).crop(box)
            x, y = ci * w, ri * (h + 14)
            out.alpha_composite(img.resize((img.width * scale, img.height * scale), Image.NEAREST), (x, y + 14))
            d.text((x + 3, y + 1), label, fill=(210, 210, 220))
    out.save(path)


def main():
    meta = json.load(open(os.path.join(DG.DS.EXPORT, "hero.meta.json")))
    base = meta["presets"]
    jobs, rows, faults = [], [], {}

    def job(key, skins):
        jobs.append({"key": key, "skins": skins})
        return key

    for sex in ("male", "female"):
        items = [("no armor", job(f"{sex}_none", base[sex]), "setup")]
        for m in MATERIALS:
            items.append((f"{m} r4", job(f"{sex}_{m}", base[sex] + armor(m, 4)), "setup"))
        mixed = base[sex] + ["helmet/leather_late_r6", "chest/plate_late_r6", "gloves/cloth_late_r6", "boots/plate_early_r6"]
        items.append(("mixed r6", job(f"{sex}_mixed", mixed), "setup"))
        items.append(("shield r6", job(f"{sex}_shield", base[sex] + armor("plate_late", 6) + ["offhand/heater_r6"]), "setup"))
        items.append(("round shield", job(f"{sex}_round", base[sex] + armor("leather_early", 5) + ["offhand/round_r5"]), "setup"))
        rows.append((sex, items))
    for m, sex in (("plate_early", "male"), ("plate_late", "male"), ("leather_early", "female"), ("leather_late", "female"), ("cloth_early", "male"), ("cloth_late", "female")):
        rows.append((f"{m} rarities", [(f"r{r}", job(f"{sex}_{m}_r{r}", base[sex] + armor(m, r)), "setup") for r in range(1, 12)]))
    poses = {p: DG.POSES[p] for p in CHECK_POSES}
    res, _ = DG.render(jobs, poses)
    for j in jobs:
        for p in CHECK_POSES:
            f = DG.faults_of(res[j["key"]][p])
            if f:
                faults.setdefault(j["key"], {})[p] = f
    os.makedirs(OUT, exist_ok=True)
    sheet(rows[:2], res, os.path.join(OUT, "armor_sets.png"))
    sheet(rows[2:], res, os.path.join(OUT, "armor_rarities.png"), scale=2)
    pose_rows = []
    for m in MATERIALS:
        pose_rows.append((f"male {m}", [(p, f"male_{m}", p) for p in CHECK_POSES]))
    sheet(pose_rows, res, os.path.join(OUT, "armor_poses.png"), scale=2, box=(0, 8, 80, 80))
    json.dump(faults, open(os.path.join(OUT, "armor_defects.json"), "w"), indent=1)
    print("defects:", json.dumps(faults) if faults else "none")


if __name__ == "__main__":
    main()


def playback():
    import subprocess
    import doll_render as DR
    meta = json.load(open(os.path.join(DG.DS.EXPORT, "hero.meta.json")))
    layers = [meta["layers"][k] for k in ("back", "body", "front")]
    sets = {"male_plate_late_r6": meta["presets"]["male"] + armor("plate_late", 6), "female_cloth_late_r5": meta["presets"]["female"] + armor("cloth_late", 5),
            "female_leather_late_r7": meta["presets"]["female"] + armor("leather_late", 7)}
    play = [{"key": k, "skins": v, "queue": ["Idle", "Enter", "Hit1", "Hit2", "Hit3", "Exit3", "Idle"], "steps": 170} for k, v in sets.items()]
    cfg = {"layers": layers, "compare": [], "play": play}
    open(os.path.join(DR.PROBE, "hero.html"), "w").write(DR.PAGE.replace("CFG", json.dumps(cfg)))
    open(os.path.join(DR.PROBE, "hero_shot.js"), "w").write(DR.SHOT)
    path = os.path.join(DR.PROBE, "check", "armor_play.json")
    subprocess.run(["node", "hero_shot.js", path], cwd=DR.PROBE, check=True)
    res = json.load(open(path))
    for key in sets:
        frames = []
        for f in res[key]:
            bg = Image.new("RGBA", DR.SIZE, (38, 32, 52, 255))
            bg.alpha_composite(DR.compose(f))
            frames.append(bg.resize((DR.SIZE[0] * 4, DR.SIZE[1] * 4), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
        frames[0].save(os.path.join(OUT, f"armor_play_{key}.gif"), save_all=True, append_images=frames[1:], duration=33, loop=0, disposal=2)


if __name__ == "__main__":
    playback()
