import argparse
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ENTITIES = ROOT / "art" / "entities"
TMP = ROOT / "art" / "out" / "tmp"
SPRITES = ROOT / "art" / "out" / "sprites"
PREVIEW = ROOT / "art" / "out" / "preview"
SUPERSAMPLE = 4
GROUND_PX = 8
FPS = 12
LOOPING = {"idle"}
PREVIEW_BACKGROUND = (178, 214, 160, 255)


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--entity", required=True)
    parser.add_argument("--variant", default=None)
    return parser.parse_args()


def premultiply_down(image, size):
    r, g, b, a = image.split()
    premultiplied = Image.merge("RGBA", (
        Image.composite(r, Image.new("L", r.size, 0), a),
        Image.composite(g, Image.new("L", g.size, 0), a),
        Image.composite(b, Image.new("L", b.size, 0), a),
        a,
    ))
    small = premultiplied.resize(size, Image.LANCZOS)
    return unpremultiply(small)


def unpremultiply(image):
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if 0 < a < 255:
                pixels[x, y] = (min(255, r * 255 // a), min(255, g * 255 // a), min(255, b * 255 // a), a)
            elif a == 0:
                pixels[x, y] = (0, 0, 0, 0)
    return image


def main():
    args = parse_args()
    entity = json.loads((ENTITIES / args.entity / "entity.json").read_text())
    name = args.variant or args.entity
    cell = tuple(entity["cell"])
    anims = entity["anims"]
    columns = max(anims.values())
    sheet = Image.new("RGBA", (cell[0] * columns, cell[1] * len(anims)), (0, 0, 0, 0))
    meta = {"entity": name, "cell": list(cell), "pivot": [0.5, round(GROUND_PX / cell[1], 4)], "fps": FPS, "anims": {}}
    for row, (anim, count) in enumerate(anims.items()):
        for frame in range(count):
            big = Image.open(TMP / args.entity / name / f"{anim}_{frame:02d}.png").convert("RGBA")
            sheet.alpha_composite(premultiply_down(big, cell), (frame * cell[0], row * cell[1]))
        meta["anims"][anim] = {"row": row, "frames": count, "loop": anim in LOOPING}
    if "hit_frame" in entity:
        meta["hit_frame"] = entity["hit_frame"]
    out = SPRITES / name
    out.mkdir(parents=True, exist_ok=True)
    sheet.save(out / "sheet.png", optimize=True)
    (out / f"{name}.json").write_text(json.dumps(meta, indent=2) + "\n")
    PREVIEW.mkdir(parents=True, exist_ok=True)
    backdrop = Image.new("RGBA", sheet.size, PREVIEW_BACKGROUND)
    backdrop.alpha_composite(sheet)
    backdrop.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(PREVIEW / f"sheet_{name}.png", optimize=True)


main()
