import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

from preview_statuses import BODY, DUMMY

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Assets" / "Game" / "Art" / "Resources"
SHEETS = RESOURCES / "Milestones"
HERO = RESOURCES / "Heroes" / "Seeker" / "Seeker_Void_Idle_30x30_24f.png"
OUT = ROOT / "art" / "heroes" / "pixelfx" / "milestones" / "preview"
DARK = (0x1B, 0x22, 0x36, 255)
LIGHT = (0xC4, 0xD6, 0xEA, 255)
GROUND = {DARK: (0x2E, 0x59, 0x3B, 255), LIGHT: (0x6F, 0xA8, 0x7C, 255)}
ZOOM = 3
FPS = 15
STRIP = (160, 60)
GROUND_Y = 50
HERO_FEET = (28, GROUND_Y)
HERO_PIVOT = (15, 28)
ENEMY_FEET = (139, GROUND_Y)
SMALL_AT = (147, 15)
REST_TICKS = 6
LABEL = 12
MEDIUM = ["levelUp", "riftStep", "featureOpen", "giftOffer", "giftChosen", "gate"] + [f"artifact{part}{group}" for part in ("Open", "Awaken", "Enchant") for group in range(4)]
SMALL = ["achievement", "collection", "campTier", "quest", "trial"]
MEDIUM_CROP = (0, 64)
SMALL_CROP = (100, 160)
COLUMNS = 6


def frames(entry):
    sheet = Image.open(SHEETS / (entry["file"] + ".png")).convert("RGBA")
    width, height = entry["width"], entry["height"]
    return [sheet.crop((i * width, 0, i * width + width, height)) for i in range(entry["frames"])]


def dummy():
    image = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    for y, row in enumerate(DUMMY):
        for x, ch in enumerate(row):
            if BODY[ch]:
                image.putpixel((x, y), BODY[ch] + (255,))
    return image


def scene(entry, clip, tick, fill, hero, enemy):
    image = Image.new("RGBA", STRIP, fill)
    image.paste(GROUND[fill], (0, GROUND_Y, STRIP[0], GROUND_Y + 1))
    image.alpha_composite(hero, (HERO_FEET[0] - HERO_PIVOT[0], HERO_FEET[1] - HERO_PIVOT[1]))
    image.alpha_composite(enemy, (ENEMY_FEET[0] - 16, ENEMY_FEET[1] - 32))
    if tick < len(clip):
        at = SMALL_AT if entry["size"] == "small" else HERO_FEET
        layer = Image.new("RGBA", STRIP, (0, 0, 0, 0))
        layer.paste(clip[tick], (at[0] - entry["pivot"][0], at[1] - entry["pivot"][1]))
        image.alpha_composite(layer)
    return image


def tile(entry, clip, tick, fill, hero, enemy, crop):
    image = scene(entry, clip, tick, fill, hero, enemy).crop((crop[0], 0, crop[1], STRIP[1]))
    return image.resize((image.width * ZOOM, image.height * ZOOM), Image.NEAREST)


def grid(data, keys, crop, columns, hero, enemy, name):
    clips = {key: frames(data["sheets"][key]) for key in keys}
    ticks = max(len(clip) for clip in clips.values()) + REST_TICKS
    wide = (crop[1] - crop[0]) * ZOOM
    high = STRIP[1] * ZOOM + LABEL
    rows = (len(keys) + columns - 1) // columns
    out = []
    for tick in range(ticks):
        sheet = Image.new("RGBA", (wide * columns, high * rows * 2), DARK)
        sheet.paste(LIGHT, (0, high * rows, wide * columns, high * rows * 2))
        draw = ImageDraw.Draw(sheet)
        for half, fill in enumerate((DARK, LIGHT)):
            for index, key in enumerate(keys):
                x, y = (index % columns) * wide, (half * rows + index // columns) * high
                sheet.paste(tile(data["sheets"][key], clips[key], tick, fill, hero, enemy, crop), (x, y + LABEL))
                draw.text((x + 2, y), key, fill=(150, 160, 190) if half == 0 else (60, 70, 100))
        out.append(sheet.convert("RGB"))
    out[0].save(OUT / f"{name}.gif", save_all=True, append_images=out[1:], duration=1000 // FPS, loop=0)
    return out


def film(data, keys, crop, hero, enemy, name):
    wide = (crop[1] - crop[0]) * ZOOM
    high = STRIP[1] * ZOOM
    longest = max(data["sheets"][key]["frames"] for key in keys)
    sheet = Image.new("RGBA", (wide * longest, high * len(keys)), DARK)
    for row, key in enumerate(keys):
        entry = data["sheets"][key]
        clip = frames(entry)
        for tick in range(len(clip)):
            sheet.paste(tile(entry, clip, tick, LIGHT if row % 2 else DARK, hero, enemy, crop), (tick * wide, row * high))
    sheet.convert("RGB").save(OUT / f"{name}.png")


def main():
    data = json.loads((SHEETS / "milestones.json").read_text(encoding="utf-8"))
    hero = Image.open(HERO).convert("RGBA").crop((0, 0, 30, 30))
    enemy = dummy()
    OUT.mkdir(parents=True, exist_ok=True)
    grid(data, ["prestige"], (0, STRIP[0]), 1, hero, enemy, "milestones-prestige")
    grid(data, MEDIUM, MEDIUM_CROP, COLUMNS, hero, enemy, "milestones-medium")
    grid(data, SMALL, SMALL_CROP, len(SMALL), hero, enemy, "milestones-small")
    film(data, ["prestige"], (0, STRIP[0]), hero, enemy, "film-prestige")
    film(data, MEDIUM[:6], MEDIUM_CROP, hero, enemy, "film-medium")
    film(data, [MEDIUM[6], MEDIUM[11], MEDIUM[16], MEDIUM[9]], MEDIUM_CROP, hero, enemy, "film-artifacts")
    film(data, SMALL, SMALL_CROP, hero, enemy, "film-small")
    return 0


if __name__ == "__main__":
    sys.exit(main())
