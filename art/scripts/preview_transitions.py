import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SHEETS = ROOT / "Assets" / "Game" / "Art" / "Resources" / "Transitions"
OUT = ROOT / "art" / "heroes" / "pixelfx" / "transitions" / "preview"
DARK = (0x1B, 0x22, 0x36, 255)
LIGHT = (0xC4, 0xD6, 0xEA, 255)
TICKS = 24
PAD = 8


def frames(entry):
    path = SHEETS / (entry["file"] + ".png")
    if not path.exists():
        return None
    sheet = Image.open(path).convert("RGBA")
    width, height = entry["width"], entry["height"]
    return [sheet.crop((i * width, 0, i * width + width, height)) for i in range(entry["frames"])]


def timeline(place):
    steps = TICKS // place["portalIdle"]["fps"]
    opened = frames(place["portalOpen"])
    idle = frames(place["portalIdle"])
    closed = frames(place["portalClose"])
    if not opened or not idle or not closed:
        return None
    portal = opened + [frame for frame in idle * 2 for _ in range(steps)] + closed
    blank = Image.new("RGBA", portal[0].size, (0, 0, 0, 0))
    portal += [blank] * 10
    columns = [(portal, 3)]
    for key in ("stepIn", "arrive", "allyOut", "allyIn"):
        clip = frames(place[key])
        if not clip:
            return None
        hold = 3 if key.startswith("ally") else 1
        spaced = [frame for frame in clip for _ in range(hold)]
        empty = Image.new("RGBA", clip[0].size, (0, 0, 0, 0))
        columns.append(((spaced + [empty] * 8), 2 if key.startswith("ally") else 3))
    return columns


def montage(name, place):
    columns = timeline(place)
    if not columns:
        return False
    length = len(columns[0][0])
    width = PAD + sum(clip[0].width * scale + PAD for clip, scale in columns)
    height = PAD * 2 + max(clip[0].height * scale for clip, scale in columns)
    shots = []
    for tick in range(length):
        shot = Image.new("RGBA", (width, height * 2), DARK)
        shot.paste(LIGHT, (0, height, width, height * 2))
        x = PAD
        for clip, scale in columns:
            frame = clip[tick % len(clip)]
            big = frame.resize((frame.width * scale, frame.height * scale), Image.NEAREST)
            for row in (0, height):
                shot.alpha_composite(big, (x, row + height - PAD - big.height))
            x += big.width + PAD
        shots.append(shot.convert("P", palette=Image.ADAPTIVE, colors=255))
    OUT.mkdir(parents=True, exist_ok=True)
    shots[0].save(OUT / f"{name}.gif", save_all=True, append_images=shots[1:], duration=1000 // TICKS, loop=0)
    return True


def overview(places):
    shots = []
    for key, place in places.items():
        idle = frames(place["portalIdle"])
        ally = frames(place["allyOut"])
        if idle and ally:
            shots.append((idle[2], ally[4]))
    if not shots:
        return
    width = PAD + sum(big.width * 3 + PAD + small.width * 2 + PAD for big, small in shots)
    height = PAD * 2 + max(big.height * 3 for big, _ in shots)
    sheet = Image.new("RGBA", (width, height * 2), DARK)
    sheet.paste(LIGHT, (0, height, width, height * 2))
    x = PAD
    for big, small in shots:
        for clip, scale in ((big, 3), (small, 2)):
            scaled = clip.resize((clip.width * scale, clip.height * scale), Image.NEAREST)
            for row in (0, height):
                sheet.alpha_composite(scaled, (x, row + height - PAD - scaled.height))
            x += scaled.width + PAD
    sheet.save(OUT / "all-places.png")


def rift_world(data):
    sky = data["riftSky"]
    parts = {name: frames(sky[name]) for name in ("sky", "edgeLeft", "edgeRight", "motes")}
    loading = frames(data["places"]["rift"]["loading"])
    if not loading or not all(parts.values()):
        return
    shots = []
    for tick in range(48):
        shot = Image.new("RGBA", (160 * 3 + PAD * 2, 60 * 3 * 3 + PAD * 4), (48, 48, 56, 255))
        for row, fill in enumerate((DARK, LIGHT)):
            strip = Image.new("RGBA", (160, 60), fill)
            strip.alpha_composite(parts["sky"][tick * 8 // 24 % 16], (0, 0))
            strip.alpha_composite(parts["motes"][tick * 12 // 24 % 24], (0, 0))
            strip.alpha_composite(parts["edgeLeft"][tick * 10 // 24 % 12], (0, 0))
            strip.alpha_composite(parts["edgeRight"][tick * 10 // 24 % 12], (146, 0))
            shot.paste(strip.resize((480, 180), Image.NEAREST), (PAD, PAD + row * (180 + PAD)))
        shot.paste(loading[tick // 2 % 24].resize((480, 180), Image.NEAREST), (PAD, PAD + 2 * (180 + PAD)))
        shots.append(shot.convert("P", palette=Image.ADAPTIVE, colors=255))
    shots[0].save(OUT / "rift-world.gif", save_all=True, append_images=shots[1:], duration=1000 // TICKS, loop=0)


def main():
    data = json.loads((SHEETS / "transitions.json").read_text())
    places = data["places"]
    wanted = sys.argv[1:]
    for key, place in places.items():
        name = place["portalOpen"]["file"].split("_")[1].lower()
        if wanted and name not in wanted:
            continue
        print(name, "ok" if montage(name, place) else "no sheets")
    if not wanted:
        overview(places)
        rift_world(data)


if __name__ == "__main__":
    main()
