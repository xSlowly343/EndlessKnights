import json
import os
import shutil
import subprocess
import sys

from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
FX = os.path.join(ROOT, "fx")

RUNE_PALETTES = {
    "flame": ["#1f0b10", "#4a1118", "#8c1d1c", "#cc3a1a", "#f2701f", "#ffae35", "#ffe27a", "#fff8d6"],
    "frost": ["#0c1430", "#162c5c", "#1f4f94", "#2f7fcc", "#58b4ec", "#9bdcff", "#d8f4ff", "#ffffff"],
    "thunder": ["#24160a", "#5e3a0c", "#a8680f", "#e8a218", "#ffd23c", "#fff08a", "#ffffff"],
    "shadow": ["#140a26", "#2b1252", "#481c88", "#6b2cc0", "#9a48ec", "#c684ff", "#ecc8ff", "#ffffff"],
    "light": ["#2a1c08", "#5a3e10", "#9a6c18", "#d8a428", "#ffd84a", "#fff09a", "#fffbe6", "#ffffff"],
}

CLASSES = {
    "runedancer": {
        "variants": RUNE_PALETTES,
        "own_effects": {"runes/rune-word": "rune-word.js"},
        "effects": {
            "rune_slash": {"source": "melee/sword-slash", "opts": {"radius": 34}, "anchor": [14, 2], "layer": "front"},
            "rune_rise": {"source": "melee/sword-slash", "opts": {"radius": 30, "seed": 5}, "flip_y": True, "anchor": [18, 54], "layer": "front"},
            "rune_spin": {"source": "melee/spin-slash", "opts": {"radius": 34}, "anchor": [13, 6], "layer": "front"},
            "rune_word": {"source": "runes/rune-word", "opts": {}, "rune_option": True, "anchor": [52, 30], "layer": "front"},
        },
    },
}


def render(pixelfx, source, opts):
    subprocess.run(["node", "tools/render.mjs", source, "--sheet", "--opts", json.dumps(opts)], cwd=pixelfx, check=True, stdout=subprocess.DEVNULL)
    folder = os.path.join(pixelfx, "records", source)
    name = source.split("/")[1]
    meta = json.load(open(os.path.join(folder, name + ".json")))["meta"]["pixelfx"]
    return Image.open(os.path.join(folder, name + ".png")).convert("RGBA"), meta


def flip_frames(sheet, meta):
    w, h = meta["frameSize"]
    out = Image.new("RGBA", sheet.size)
    for i in range(meta["frames"]):
        x, y = (i % meta["columns"]) * w, (i // meta["columns"]) * h
        out.paste(sheet.crop((x, y, x + w, y + h)).transpose(Image.FLIP_TOP_BOTTOM), (x, y))
    return out, [meta["pivot"][0], h - 1 - meta["pivot"][1]]


def build(cid, pixelfx):
    spec = CLASSES[cid]
    out = os.path.join(FX, cid)
    os.makedirs(out, exist_ok=True)
    for source, file in spec["own_effects"].items():
        target = os.path.join(pixelfx, "src", "fx", source + ".js")
        os.makedirs(os.path.dirname(target), exist_ok=True)
        shutil.copy(os.path.join(FX, "src", file), target)
    manifest = {"unitsPerPixel": 1, "variants": list(spec["variants"]), "effects": {}}
    for name, fx in spec["effects"].items():
        entry = None
        for variant, palette in spec["variants"].items():
            opts = dict(fx["opts"], palette=palette)
            if fx.get("rune_option"):
                opts["rune"] = variant
            sheet, meta = render(pixelfx, fx["source"], opts)
            pivot = meta["pivot"]
            if fx.get("flip_y"):
                sheet, pivot = flip_frames(sheet, meta)
            sheet.save(os.path.join(out, f"{name}_{variant}.png"))
            entry = {"sheet": f"{name}_{{variant}}.png", "source": fx["source"], "fps": meta["fps"], "frames": meta["frames"], "columns": meta["columns"],
                     "frameSize": meta["frameSize"], "pivot": pivot, "anchor": fx["anchor"], "layer": fx["layer"]}
        manifest["effects"][name] = entry
    json.dump(manifest, open(os.path.join(out, "fx.json"), "w"), indent=1)
    print(cid, "effects", len(manifest["effects"]), "variants", len(spec["variants"]))


if __name__ == "__main__":
    build(sys.argv[1], sys.argv[2])
