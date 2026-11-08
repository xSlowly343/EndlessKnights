import argparse
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ENEMIES = ROOT / "art" / "enemies"
BIOMES = ROOT / "art" / "biomes"
SPRITES = ROOT / "art" / "out" / "sprites"
BACKGROUNDS = ROOT / "art" / "out" / "backgrounds"
PREVIEW = ROOT / "art" / "out" / "preview"
VIEW = (480, 180)
FEET = 166
SEQUENCE = (("idle", 2), ("attack", 1), ("hit", 2), ("death", 1))
HOLD = 8


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--roster", default="green_hills")
    parser.add_argument("--background", default="green_meadow")
    parser.add_argument("--offset", type=int, default=40)
    return parser.parse_args()


def load(name):
    meta = json.loads((SPRITES / name / f"{name}.json").read_text())
    sheet = Image.open(SPRITES / name / "sheet.png").convert("RGBA")
    width, height = meta["cell"]
    anims = {}
    for anim, info in meta["anims"].items():
        top = info["row"] * height
        anims[anim] = [sheet.crop((i * width, top, (i + 1) * width, top + height)) for i in range(info["frames"])]
    return meta, anims


def timeline(anims):
    frames = []
    for anim, repeat in SEQUENCE:
        frames += anims[anim] * repeat
    return frames + [anims["death"][-1]] * HOLD


def background(name, offset):
    scene = Image.new("RGBA", VIEW)
    layers = json.loads((BIOMES / f"{name}.json").read_text())["layers"]
    for layer in layers:
        image = Image.open(BACKGROUNDS / name / f"{layer}.png").convert("RGBA")
        scene.alpha_composite(image.crop((offset, 0, offset + VIEW[0], VIEW[1])))
    return scene


def render(group, scene, path, fps):
    loaded = [load(name) for name in group]
    lines = [timeline(anims) for _, anims in loaded]
    widths = [meta["cell"][0] for meta, _ in loaded]
    gap = max(0, (VIEW[0] - sum(widths)) // (len(widths) + 1))
    slots, x = [], gap
    for width in widths:
        slots.append(x)
        x += width + gap
    frames = []
    for tick in range(max(len(line) for line in lines)):
        frame = scene.copy()
        for (meta, _), line, left in zip(loaded, lines, slots):
            image = line[min(tick, len(line) - 1)]
            feet = round(image.height * (1 - meta["pivot"][1]))
            frame.alpha_composite(image, (left, FEET - feet))
        big = frame.resize((VIEW[0] * 2, VIEW[1] * 2), Image.NEAREST).convert("RGB")
        frames.append(big.quantize(255, method=Image.Quantize.MEDIANCUT))
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=1000 // fps, loop=0, optimize=True)


def main():
    args = parse_args()
    roster = json.loads((ENEMIES / f"{args.roster}.json").read_text())
    scene = background(args.background, args.offset)
    mobs = [name for name, spec in roster["enemies"].items() if not spec.get("boss")]
    bosses = [name for name, spec in roster["enemies"].items() if spec.get("boss") and not spec.get("master")]
    masters = [name for name, spec in roster["enemies"].items() if spec.get("master")]
    PREVIEW.mkdir(parents=True, exist_ok=True)
    half = (len(mobs) + 1) // 2
    render(mobs[:half], scene, PREVIEW / f"roster_{args.roster}_mobs_a.gif", roster["fps"])
    render(mobs[half:], scene, PREVIEW / f"roster_{args.roster}_mobs_b.gif", roster["fps"])
    render(bosses, scene, PREVIEW / f"roster_{args.roster}_bosses.gif", roster["fps"])
    if masters:
        render(masters, scene, PREVIEW / f"roster_{args.roster}_master.gif", roster["fps"])


main()
