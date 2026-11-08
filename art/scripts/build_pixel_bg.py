import argparse
import json
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance

ROOT = Path(__file__).resolve().parents[2]
BIOMES = ROOT / "art" / "biomes"
OUT_DIR = ROOT / "art" / "out" / "backgrounds"
PREVIEW_DIR = ROOT / "art" / "out" / "preview"
VIEW = (480, 180)
PARALLAX_SHIFT = {"back": 0, "mid": 40, "ground": 120, "front": 160}
PREVIEW_STEPS = 3


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--biome", default="green_hills")
    parser.add_argument("--packs", required=True)
    return parser.parse_args()


def hex_rgba(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def darker(color, amount):
    return tuple(max(0, c - amount) for c in color[:3]) + (255,)


def floor_strip(width, spec):
    rng = random.Random(spec["seed"])
    top, dirt = hex_rgba(spec["top"]), hex_rgba(spec["dirt"])
    strip = Image.new("RGBA", (width, spec["height"]), dirt)
    draw = ImageDraw.Draw(strip)
    draw.rectangle((0, 0, width, 3), fill=top)
    for x in range(0, width, 2):
        if rng.random() < 0.5:
            draw.rectangle((x, 4, x + 1, 5), fill=top)
    for _ in range(width // 2):
        x, y = rng.randrange(0, width, 2), rng.randrange(8, spec["height"], 2)
        draw.rectangle((x, y, x + 1, y + 1), fill=darker(dirt, 22))
    return strip


def dimmed(image, spec):
    image = ImageEnhance.Color(image).enhance(spec["color"])
    return ImageEnhance.Brightness(image).enhance(spec["brightness"])


def layer_entry(entry):
    return (entry, 0) if isinstance(entry, str) else (entry["file"], entry["y"])


def placed_part(folder, entry, scale, canvas):
    name, y = layer_entry(entry)
    part = Image.open(folder / name).convert("RGBA")
    if scale == 1:
        return part
    size = (round(part.width * scale), round(part.height * scale))
    placed = Image.new("RGBA", (round(canvas[0] * scale), round(canvas[1] * scale)))
    placed.alpha_composite(part.resize(size, Image.BOX), (0, round(y * scale)))
    return placed


def build_layer(folder, files, config):
    window, scale = config["window"], config.get("scale", 1)
    layer = None
    for entry in files:
        part = placed_part(folder, entry, scale, config.get("canvas"))
        part = part.crop((0, window["top"], part.width, window["top"] + window["height"]))
        layer = part if layer is None else Image.alpha_composite(layer, part)
    alpha = layer.getchannel("A")
    layer = dimmed(layer.convert("RGB"), config["dim"]).convert("RGBA")
    layer.putalpha(alpha)
    return layer


def mirror_tiled(image):
    joined = Image.new("RGBA", (image.width * 2, image.height))
    joined.paste(image, (0, 0))
    joined.paste(image.transpose(Image.FLIP_LEFT_RIGHT), (image.width, 0))
    return joined


def flattened(layers):
    flat = Image.new("RGBA", next(iter(layers.values())).size, (0, 0, 0, 255))
    for image in layers.values():
        flat.alpha_composite(image)
    return flat


def with_palette(layers, colors):
    palette = flattened(layers).convert("RGB").quantize(colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    result = {}
    for name, image in layers.items():
        alpha = image.getchannel("A").point(lambda value: 255 if value > 110 else 0)
        mapped = image.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE).convert("RGBA")
        mapped.putalpha(alpha)
        result[name] = mapped
    return result


def tiled(image, offset, width):
    doubled = Image.new("RGBA", (image.width * 2, image.height))
    doubled.paste(image, (0, 0))
    doubled.paste(image, (image.width, 0))
    start = offset % image.width
    return doubled.crop((start, 0, start + width, image.height))


def main():
    args = parse_args()
    config = json.loads((BIOMES / f"{args.biome}.json").read_text())
    folder = Path(args.packs) / config["source"]
    layers = {name: build_layer(folder, files, config) for name, files in config["layers"].items()}
    if config.get("mirror_tile"):
        layers = {name: mirror_tiled(image) for name, image in layers.items()}
    if "palette" in config:
        layers = with_palette(layers, config["palette"])
    if "floor" in config:
        floor = floor_strip(layers["ground"].width, config["floor"])
        layers["ground"].alpha_composite(floor, (0, VIEW[1] - floor.height))
    out = OUT_DIR / args.biome
    out.mkdir(parents=True, exist_ok=True)
    for name, image in layers.items():
        image.save(out / f"{name}.png", optimize=True)
    gap = 4
    sheet = Image.new("RGBA", (VIEW[0], VIEW[1] * PREVIEW_STEPS + gap * (PREVIEW_STEPS - 1)), (20, 24, 36, 255))
    for step in range(PREVIEW_STEPS):
        frame = Image.new("RGBA", VIEW)
        for name, image in layers.items():
            frame.alpha_composite(tiled(image, PARALLAX_SHIFT[name] * step, VIEW[0]))
        sheet.alpha_composite(frame, (0, step * (VIEW[1] + gap)))
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(PREVIEW_DIR / f"bg_{args.biome}.png", optimize=True)


main()
