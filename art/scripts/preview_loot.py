import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SHEETS = ROOT / "Assets" / "Game" / "Art" / "Resources" / "Loot"
OUT = ROOT / "art" / "heroes" / "pixelfx" / "loot" / "preview"
DARK = (0x1B, 0x22, 0x36, 255)
LIGHT = (0xC4, 0xD6, 0xEA, 255)
ZOOM = 3
TICKS = 24
STEP_MS = 1000 // 12
LABEL = 12
HEIGHT = 56


def frames(entry):
    sheet = Image.open(SHEETS / (entry["file"] + ".png")).convert("RGBA")
    width, height = entry["width"], entry["height"]
    return [sheet.crop((i * width, 0, i * width + width, height)) for i in range(entry["frames"])]


def at(entry, clip, tick):
    index = tick * entry["fps"] // 12
    return clip[index % len(clip)] if entry.get("loops") else clip[min(index, len(clip) - 1)]


def scenes(data):
    sheets = data["sheets"]
    out = [(beam["rarity"], [beam, sheets["bundle"]]) for beam in data["beams"]]
    out.append(("set", [data["beams"][4], sheets["setMark"], sheets["bundle"]]))
    out.append(("unique", [data["beams"][5], sheets["uniqueMark"], sheets["bundle"]]))
    for key in ("chestOpen", "letterOpen", "coin", "gem", "spark"):
        out.append((key, [sheets[key]]))
    return out


def tile(layers, tick, fill):
    width = max(layer["width"] for layer in layers) + 8
    image = Image.new("RGBA", (width, HEIGHT), fill)
    for layer in layers:
        frame = at(layer, frames(layer), tick)
        ground = HEIGHT - 5 if layer["pivot"][1] > layer["height"] * 0.7 else HEIGHT // 2
        image.alpha_composite(frame, (width // 2 - layer["pivot"][0], ground - layer["pivot"][1]))
    return image.resize((width * ZOOM, HEIGHT * ZOOM), Image.NEAREST)


def main():
    data = json.loads((SHEETS / "loot.json").read_text(encoding="utf-8"))
    rows = scenes(data)
    widths = [tile(layers, 0, DARK).width for _, layers in rows]
    total = sum(widths)
    high = HEIGHT * ZOOM + LABEL
    out = []
    for tick in range(TICKS * 2):
        sheet = Image.new("RGBA", (total, high * 2), DARK)
        sheet.paste(LIGHT, (0, high, total, high * 2))
        draw = ImageDraw.Draw(sheet)
        x = 0
        for (name, layers), width in zip(rows, widths):
            for row, fill in enumerate((DARK, LIGHT)):
                sheet.paste(tile(layers, tick % TICKS, fill), (x, row * high + LABEL))
                draw.text((x + 2, row * high), name[:11], fill=(150, 160, 190) if row == 0 else (60, 70, 100))
            x += width
        out.append(sheet.convert("RGB"))
    OUT.mkdir(parents=True, exist_ok=True)
    out[0].save(OUT / "loot.gif", save_all=True, append_images=out[1:], duration=STEP_MS, loop=0)
    out[7].save(OUT / "loot.png")
    return 0


if __name__ == "__main__":
    sys.exit(main())
