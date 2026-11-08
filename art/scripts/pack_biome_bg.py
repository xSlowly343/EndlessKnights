import argparse
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "art" / "out" / "backgrounds"
PREVIEW_DIR = ROOT / "art" / "out" / "preview"
LAYERS = ("back", "mid", "ground")
VIEW = (480, 180)
SUPERSAMPLE = 4
PARALLAX_SHIFT = {"back": 0, "mid": 60, "ground": 180}


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--biome", default="green_hills")
    return parser.parse_args()


def downscale(image):
    size = (image.width // SUPERSAMPLE, image.height // SUPERSAMPLE)
    return image.resize(size, Image.LANCZOS)


def tiled_crop(image, offset, width):
    doubled = Image.new("RGBA", (image.width * 2, image.height))
    doubled.paste(image, (0, 0))
    doubled.paste(image, (image.width, 0))
    start = offset % image.width
    return doubled.crop((start, 0, start + width, image.height))


def compose(layers, rift, step):
    frame = Image.new("RGBA", VIEW)
    for name in LAYERS:
        frame.alpha_composite(tiled_crop(layers[name], PARALLAX_SHIFT[name] * step, VIEW[0]))
    frame.alpha_composite(rift, (VIEW[0] - rift.width - 6, VIEW[1] - rift.height - 4))
    return frame


def main():
    args = parse_args()
    folder = OUT_DIR / args.biome
    layers = {}
    for name in LAYERS:
        image = downscale(Image.open(folder / "raw" / f"{name}.png").convert("RGBA"))
        image.save(folder / f"{name}.png", optimize=True)
        layers[name] = image
    rift = downscale(Image.open(folder / "raw" / "rift.png").convert("RGBA"))
    rift.save(folder / "rift.png", optimize=True)
    frames = [compose(layers, rift, step) for step in range(3)]
    sheet = Image.new("RGBA", (VIEW[0], VIEW[1] * len(frames) + 4 * (len(frames) - 1)), (20, 24, 36, 255))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, (0, index * (VIEW[1] + 4)))
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    sheet.save(PREVIEW_DIR / f"bg_{args.biome}.png", optimize=True)
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(PREVIEW_DIR / f"bg_{args.biome}@2x.png", optimize=True)


main()
