import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SHEETS = ROOT / "Assets" / "Game" / "Art" / "Resources" / "Statuses"
OUT = ROOT / "art" / "heroes" / "pixelfx" / "statuses" / "preview"
DARK = (0x1B, 0x22, 0x36, 255)
LIGHT = (0xC4, 0xD6, 0xEA, 255)
TICKS = 24
STEP_MS = 1000 // 12
CURSE = (0xB0, 0x7C, 0xFF, 255)
VULNERABLE = (0xE0, 0x57, 0x4A, 255)
SCENES = [
    ("poison 1", ["poison1"], None),
    ("poison 2", ["poison2"], None),
    ("poison 3", ["poison3"], None),
    ("burn 1", ["burnBody", "burnTop1"], None),
    ("burn 2", ["burnBody", "burnTop2"], None),
    ("burn 3", ["burnBody", "burnTop3"], None),
    ("ember", ["ember"], None),
    ("curse", ["curse"], CURSE),
    ("mark", ["markOn", "mark"], None),
    ("stun", ["stun"], None),
    ("blind", ["blind"], None),
    ("vulnerable", [], VULNERABLE),
    ("fracture 1", ["fracture1"], None),
    ("fracture 2", ["fracture2"], None),
    ("fracture 3", ["fracture3"], None),
    ("fracture 4", ["fracture4", "fractureChip"], None),
    ("earth 1", ["earth1"], None),
    ("earth 2", ["earth2"], None),
    ("earth 3", ["earth3"], None),
    ("water 1", ["water1"], None),
    ("water 2", ["water2"], None),
    ("lightning", ["lightning"], None),
    ("air", ["air"], None),
    ("corrosion", ["corrosion", "fracture1"], None),
    ("weakened", [], None),
    ("poison+burn+fracture", ["poison2", "burnBody", "burnTop2", "fracture2"], None),
    ("stun+blind+curse+mark", ["stun", "blind", "curse", "mark"], CURSE),
    ("everything", ["poison3", "burnBody", "burnTop3", "fracture3", "stun", "blind", "curse", "mark", "earth3", "water2"], CURSE),
]
BODY = {".": None, "o": (0x2A, 0x14, 0x18), "d": (0x6A, 0x30, 0x2C), "m": (0x9A, 0x4E, 0x3E), "l": (0xC0, 0x74, 0x54), "h": (0xE6, 0xD8, 0xB8), "e": (0xFF, 0xF0, 0x90), "k": (0x14, 0x10, 0x18)}
DUMMY = [
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "......oo..............oo........",
    ".....ohho............ohho.......",
    ".....ohhho..........ohhho.......",
    "......ohhho..oooooo.ohho........",
    ".......ohhooollllllooho.........",
    "........oollllllllllloo.........",
    ".......olllllmmmmmmmmmlo........",
    "......ollllmmmmmmmmmmmmmo.......",
    "......ollmmmmmmmmmmmmmmmo.......",
    ".....olleeommmmmmmeeommmdo......",
    ".....olmekommmmmmmekommmdo......",
    ".....olmmmmmmmmmmmmmmmmmdo......",
    ".....olmmmmmokkkkommmmmddo......",
    ".....olmmmmmhkhkhkmmmmmddo......",
    ".....ommmmmmmooooommmmmddo......",
    "...ooommmmmmmmmmmmmmmmdddooo....",
    "..ollommmmmmmmmmmmmmmmdddodlo...",
    "..olmodmmmmmmmmmmmmmmdddoddmo...",
    "..ommodmmmmmmmmmmmmmdddddoddo...",
    "...oo.odmmmmmmmmmmmddddddo.oo...",
    "......oddmmmmmmmmdddddddo.......",
    ".......oddddddddddddddddo.......",
    ".......odddddooooodddddo........",
    "......olmmddo.....olmmddo.......",
    "......ommdddo.....ommdddo.......",
    ".....oommddoo....oommddoo.......",
    ".....oooooooo....oooooooo.......",
]


def dummy(scale, pale):
    image = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    for y, row in enumerate(DUMMY):
        for x, char in enumerate(row):
            colour = BODY[char]
            if colour is None:
                continue
            if pale:
                grey = sum(colour) // 3
                colour = tuple((c + grey * 2) // 3 * 3 // 4 + 48 for c in colour)
            image.putpixel((x, y), colour + (255,))
    return image.resize((32 * scale, 32 * scale), Image.NEAREST)


def frames(entry):
    sheet = Image.open(SHEETS / (entry["file"] + ".png")).convert("RGBA")
    width, height = entry["width"], entry["height"]
    return [sheet.crop((i * width, 0, i * width + width, height)) for i in range(entry["frames"])]


def at(entry, clip, tick):
    index = tick * entry["fps"] // 12
    if entry.get("loops"):
        return clip[index % len(clip)]
    return clip[index] if index < len(clip) else None


def outline(body, colour, tick):
    alpha = body.getchannel("A")
    grown = Image.new("L", body.size, 0)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        shifted = Image.new("L", body.size, 0)
        shifted.paste(alpha, (dx, dy))
        grown = Image.composite(shifted, grown, shifted)
    strength = 255 if tick % 8 < 5 else 150
    layer = Image.new("RGBA", body.size, colour[:3] + (0,))
    layer.putalpha(grown.point(lambda a: strength if a else 0))
    return layer


def cell(sheets, size, scene, tick, fill, body_px):
    _, keys, rim = scene
    scale = body_px // 32
    pad = 16 * scale
    width, height = body_px + pad * 2, body_px + pad * 2
    canvas = Image.new("RGBA", (width, height), fill)
    body = dummy(scale, scene[0] == "weakened")
    box = body.getbbox()
    origin = (pad, pad)
    feet = (pad + body_px // 2, pad + body_px - 1)
    anchors = {
        "body": (pad + (box[0] + box[2]) // 2, pad + box[3] - 1),
        "top": (pad + (box[0] + box[2]) // 2, pad + box[1]),
        "feet": feet,
        "head": (pad + (box[0] + box[2]) // 2, pad + box[1]),
        "eyes": (pad + (box[0] + box[2]) // 2, pad + box[1] + (box[3] - box[1]) * 34 // 100),
        "chest": (pad + (box[0] + box[2]) // 2, pad + (box[1] + box[3]) // 2),
    }
    if rim:
        halo = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        halo.paste(body, origin)
        canvas.alpha_composite(outline(halo, rim, tick))
    canvas.alpha_composite(body, origin)
    mask = Image.new("L", (width, height), 0)
    mask.paste(body.getchannel("A"), origin)
    lift = 0
    for key in keys:
        entry = sheets[key]
        frame = at(entry, frames(entry), tick % TICKS if not entry.get("loops") else tick)
        if frame is None:
            continue
        anchor = anchors[entry["anchor"]]
        spot = [anchor[0] - entry["pivot"][0], anchor[1] - entry["pivot"][1]]
        if key == "curse" and "stun" in keys:
            spot[1] -= 12 * scale
        layer = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        layer.paste(frame, tuple(spot))
        if entry.get("masked"):
            layer.putalpha(Image.composite(layer.getchannel("A"), Image.new("L", (width, height), 0), mask))
        canvas.alpha_composite(layer)
    return canvas


def grid(sheets, size, body_px, zoom, columns, name, scenes):
    rows = (len(scenes) + columns - 1) // columns
    sample = cell(sheets, size, scenes[0], 0, DARK, body_px)
    cw, ch = sample.width * zoom, sample.height * zoom
    label = 12
    out = []
    for tick in range(TICKS * 2):
        sheet = Image.new("RGBA", (cw * columns * 2 + 8, (ch + label) * rows), DARK)
        sheet.paste(LIGHT, (cw * columns + 8, 0, sheet.width, sheet.height))
        draw = ImageDraw.Draw(sheet)
        for i, scene in enumerate(scenes):
            for half, fill in enumerate((DARK, LIGHT)):
                tile = cell(sheets, size, scene, tick, fill, body_px).resize((cw, ch), Image.NEAREST)
                x = (i % columns) * cw + half * (cw * columns + 8)
                y = (i // columns) * (ch + label)
                sheet.paste(tile, (x, y + label))
                draw.text((x + 3, y), scene[0], fill=(150, 160, 190) if half == 0 else (60, 70, 100))
        out.append(sheet.convert("RGB"))
    OUT.mkdir(parents=True, exist_ok=True)
    out[0].save(OUT / f"{name}.gif", save_all=True, append_images=out[1:], duration=STEP_MS, loop=0)
    out[5].save(OUT / f"{name}.png")


def icons(entry):
    clip = frames(entry)
    zoom = 6
    sheet = Image.new("RGBA", ((9 * zoom + 6) * len(clip) + 6, (9 * zoom + 18) * 2), DARK)
    sheet.paste(LIGHT, (0, 9 * zoom + 18, sheet.width, sheet.height))
    for i, frame in enumerate(clip):
        big = frame.resize((9 * zoom, 9 * zoom), Image.NEAREST)
        for row in range(2):
            sheet.alpha_composite(big, (6 + i * (9 * zoom + 6), 6 + row * (9 * zoom + 18)))
    sheet.save(OUT / "icons.png")


def main():
    data = json.loads((SHEETS / "statuses.json").read_text(encoding="utf-8"))
    grid(data["sizes"]["small"]["sheets"], "small", 32, 3, 7, "statuses-small", SCENES)
    grid(data["sizes"]["large"]["sheets"], "large", 64, 2, 4, "statuses-large", [s for s in SCENES if s[0] in ("poison 3", "burn 3", "fracture 4", "curse", "stun", "mark", "earth 3", "water 2", "lightning", "air", "corrosion", "everything")])
    icons(data["icons"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
