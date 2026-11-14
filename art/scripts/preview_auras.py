import json
import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Assets" / "Game" / "Art" / "Resources" / "Heroes" / "Seeker"
OUT = ROOT / "art" / "heroes" / "seeker" / "strike" / "preview"
IDLE = RESOURCES / "Seeker_Void_Idle_30x30_24f.png"
IDLE_FEET = (15, 28)
DARK = (0x1B, 0x22, 0x36, 255)
LIGHT = (0xC4, 0xD6, 0xEA, 255)
GEMS = [(0xE8, 0x6A, 0xD0), (0x6F, 0xFD, 0x92), (0xFF, 0xD2, 0x7A), (0x78, 0xCC, 0xF4), (0xFF, 0x6A, 0x7A)]
ORBIT = (14, 4, 11)
TICKS = 30


def clip(sheet):
    image = Image.open(RESOURCES / (sheet["file"] + ".png")).convert("RGBA")
    width, height = sheet["width"], sheet["height"]
    return [image.crop((i * width, 0, i * width + width, height)) for i in range(sheet["frames"])]


def tint(frame, color, opacity=1.0):
    red, green, blue, alpha = frame.split()
    return Image.merge("RGBA", (
        red.point(lambda v: v * color[0] // 255), green.point(lambda v: v * color[1] // 255),
        blue.point(lambda v: v * color[2] // 255), alpha.point(lambda v: int(v * opacity))))


def place(canvas, frame, pivot, at):
    canvas.alpha_composite(frame, (at[0] - pivot[0], at[1] - pivot[1]))


def scene(auras, hero, tick, sigil, themes, timed, fill, opacity=1.0):
    canvas = Image.new("RGBA", (56, 44), fill)
    feet = (28, 36)
    sign = auras["sigils"][sigil]
    place(canvas, tint(clip_of(sign)[tick % sign["frames"]], GEMS[0], 0.7 * opacity), sign["pivot"], feet)
    if timed:
        surge = auras["surge"]
        place(canvas, tint(clip_of(surge)[tick % surge["frames"]], GEMS[1], 0.7 * opacity), surge["pivot"], feet)
    spots = []
    for index, theme in enumerate(themes):
        turn = tick / (6 * TICKS) + index / len(themes)
        spots.append((math.sin(turn * 2 * math.pi), theme, index,
                      (feet[0] + round(math.cos(turn * 2 * math.pi) * ORBIT[0]), feet[1] - ORBIT[2] + round(math.sin(turn * 2 * math.pi) * ORBIT[1]))))
    for depth, theme, index, at in spots:
        if depth < 0:
            mote = auras["motes"][theme]
            place(canvas, tint(clip_of(mote)[tick % mote["frames"]], GEMS[index % len(GEMS)], 0.8 * opacity), mote["pivot"], at)
    place(canvas, hero[tick * 10 // TICKS % len(hero)], IDLE_FEET, feet)
    for depth, theme, index, at in spots:
        if depth >= 0:
            mote = auras["motes"][theme]
            place(canvas, tint(clip_of(mote)[tick % mote["frames"]], GEMS[index % len(GEMS)], 0.8 * opacity), mote["pivot"], at)
    return canvas


CLIPS = {}


def clip_of(sheet):
    if sheet["file"] not in CLIPS:
        CLIPS[sheet["file"]] = clip(sheet)
    return CLIPS[sheet["file"]]


def grid(auras):
    names = list(auras["sigils"])
    cell = (34, 14)
    sheet = Image.new("RGBA", (6 + 6 * (cell[0] * 3 + 6), 6 + len(names) * (cell[1] * 3 + 6)), DARK)
    for row, name in enumerate(names):
        sign = auras["sigils"][name]
        for column in range(6):
            frame = Image.new("RGBA", cell, (0, 0, 0, 0))
            place(frame, tint(clip_of(sign)[column * 10], GEMS[row % len(GEMS)]), sign["pivot"], (17, 7))
            sheet.alpha_composite(frame.resize((cell[0] * 3, cell[1] * 3), Image.NEAREST), (6 + column * (cell[0] * 3 + 6), 6 + row * (cell[1] * 3 + 6)))
    sheet.save(OUT / "14e22-aura-sigils.png")


def motes(auras):
    names = list(auras["motes"])
    sheet = Image.new("RGBA", (6 + len(names) * 4 * 36 + 6, 6 + 60 + 30 + 30 + 12), DARK)
    sheet.paste(LIGHT, (0, 96, sheet.width, sheet.height))
    for column, name in enumerate(names):
        mote = auras["motes"][name]
        for step in range(4):
            frame = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
            place(frame, tint(clip_of(mote)[step * 9], GEMS[column % len(GEMS)]), mote["pivot"], (5, 5))
            x = 6 + (column * 4 + step) * 36
            sheet.alpha_composite(frame.resize((60, 60), Image.NEAREST).crop((12, 0, 48, 60)), (x, 6))
            faint = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
            place(faint, tint(clip_of(mote)[step * 9], GEMS[column % len(GEMS)], 0.4), mote["pivot"], (5, 5))
            sheet.alpha_composite(faint.resize((20, 20), Image.NEAREST), (x + 8, 70))
            sheet.alpha_composite(faint.resize((20, 20), Image.NEAREST), (x + 8, 102))
    sheet.save(OUT / "14e22-aura-motes-x2-40.png")


def strip(auras, hero):
    cases = [("common", ["might"], False), ("runedancer", ["edge", "hunt"], False), ("clockwright", ["flow", "fortune", "hex", "kin"], True),
             ("dreamcatcher", ["might", "edge", "hunt", "flow", "calm"], False)]
    shots = []
    for tick in range(6 * TICKS // 2):
        shot = Image.new("RGBA", (6 + len(cases) * (56 * 3 + 6), 6 + 2 * (44 * 3 + 6)), (48, 48, 56, 255))
        for column, (sigil, themes, timed) in enumerate(cases):
            for row, fill in enumerate((DARK, LIGHT)):
                frame = scene(auras, hero, tick * 2, sigil, themes, timed, fill)
                shot.paste(frame.resize((56 * 3, 44 * 3), Image.NEAREST), (6 + column * (56 * 3 + 6), 6 + row * (44 * 3 + 6)))
        shots.append(shot.convert("P", palette=Image.ADAPTIVE, colors=255))
    shots[0].save(OUT / "14e22-aura-show.gif", save_all=True, append_images=shots[1:], duration=2000 // TICKS, loop=0)
    still = Image.new("RGBA", (6 + len(cases) * (56 * 3 + 6), 6 + 44 * 3 + 6), (48, 48, 56, 255))
    for column, (sigil, themes, timed) in enumerate(cases):
        still.paste(scene(auras, hero, 40, sigil, themes, timed, DARK).resize((56 * 3, 44 * 3), Image.NEAREST), (6 + column * (56 * 3 + 6), 6))
    still.save(OUT / "14e22-aura-show.png")


def parts(auras):
    surge, off = auras["surge"], auras["off"]
    sheet = Image.new("RGBA", (6 + 8 * (26 * 3 + 4) + off["frames"] * (16 * 3 + 4), 6 + 30 * 3 + 6), DARK)
    for step in range(8):
        frame = Image.new("RGBA", (26, 30), (0, 0, 0, 0))
        place(frame, tint(clip_of(surge)[step * 3], GEMS[1]), surge["pivot"], (13, 28))
        sheet.alpha_composite(frame.resize((78, 90), Image.NEAREST), (6 + step * 82, 6))
    for step in range(off["frames"]):
        frame = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        place(frame, tint(clip_of(off)[step], GEMS[2]), off["pivot"], (8, 8))
        sheet.alpha_composite(frame.resize((48, 48), Image.NEAREST), (6 + 8 * 82 + step * 52, 28))
    sheet.save(OUT / "14e22-aura-surge-off.png")


def main():
    auras = json.loads((RESOURCES / "strikes.json").read_text())["auras"]
    idle = Image.open(IDLE).convert("RGBA")
    hero = [idle.crop((i * 30, 0, i * 30 + 30, 30)) for i in range(24)]
    OUT.mkdir(parents=True, exist_ok=True)
    grid(auras)
    motes(auras)
    parts(auras)
    strip(auras, hero)


if __name__ == "__main__":
    main()
