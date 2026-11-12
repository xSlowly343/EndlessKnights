import json
import uuid
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Assets" / "Game" / "Art" / "Resources" / "Heroes" / "Seeker"
STRIKE = ROOT / "art" / "heroes" / "seeker" / "strike"
ATTACK = RESOURCES / "Seeker_Void_Attack1_90x50_8f.png"
ATTACK_CELL = (90, 50)
FIRST_SLASH_FRAME = 3
FACE_BOX = (12, 18, 24, 25)
MANIFEST = RESOURCES / "strikes.json"
SHEET_META = RESOURCES / "Seeker_Void_Crescent_87x42_8f.png.meta"
STRIKES = [
    {"look": 0, "id": "void", "flight": ("void-crescent", "Seeker_Void_Crescent"), "burst": ("void-burst", "Seeker_Void_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#6ffd92"},
    {"look": 1, "id": "star", "flight": ("star-seed", "Seeker_Star_Flight"), "burst": ("star-fall", "Seeker_Star_Burst"),
     "path": "line", "arcPx": 12, "burstAt": "ground", "slash": "#ffe066"},
    {"look": 2, "id": "moth", "flight": ("moth-flock", "Seeker_Moth_Flight"), "burst": ("moth-scatter", "Seeker_Moth_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#b3a4ee"},
    {"look": 3, "id": "cog", "flight": ("cog-roll", "Seeker_Cog_Flight"), "burst": ("cog-break", "Seeker_Cog_Burst"),
     "path": "ground", "arcPx": 0, "burstAt": "ground", "slash": "#f0b424"},
    {"look": 4, "id": "card", "flight": ("card-throw", "Seeker_Card_Flight"), "burst": ("card-cut", "Seeker_Card_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#ff6a7a"},
    {"look": 5, "id": "bramble", "flight": ("bramble-run", "Seeker_Bramble_Flight"), "burst": ("bramble-bloom", "Seeker_Bramble_Burst"),
     "path": "ground", "arcPx": 0, "burstAt": "ground", "slash": "#7cc048"},
    {"look": 6, "id": "rift", "flight": ("rift-crack", "Seeker_Rift_Flight"), "burst": ("rift-claw", "Seeker_Rift_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#d24cf0"},
    {"look": 7, "id": "koi", "flight": ("koi-leap", "Seeker_Koi_Flight"), "burst": ("koi-splash", "Seeker_Koi_Burst"),
     "path": "line", "arcPx": 14, "burstAt": "ground", "slash": "#78ccf4"},
    {"look": 8, "id": "chord", "flight": ("chord-wave", "Seeker_Chord_Flight"), "burst": ("chord-ring", "Seeker_Chord_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#ff5aa8"},
    {"look": 9, "id": "phoenix", "flight": ("phoenix-glide", "Seeker_Phoenix_Flight"), "burst": ("phoenix-rise", "Seeker_Phoenix_Burst"),
     "path": "line", "arcPx": 8, "burstAt": "ground", "slash": "#ff8a2a"},
    {"look": 10, "id": "glacier", "flight": ("glacier-lance", "Seeker_Glacier_Flight"), "burst": ("glacier-bloom", "Seeker_Glacier_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "ground", "slash": "#72c0ee"},
    {"look": 11, "id": "thunder", "flight": ("thunder-orb", "Seeker_Thunder_Flight"), "burst": ("thunder-strike", "Seeker_Thunder_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "ground", "slash": "#8c9cff"},
    {"look": 12, "id": "blade", "flight": ("blade-volley", "Seeker_Blade_Flight"), "burst": ("blade-ring", "Seeker_Blade_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#dcecff"},
    {"look": 13, "id": "wolf", "flight": ("wolf-lunge", "Seeker_Wolf_Flight"), "burst": ("wolf-bite", "Seeker_Wolf_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#4cc4d8"},
    {"look": 14, "id": "ink", "flight": ("ink-stroke", "Seeker_Ink_Flight"), "burst": ("ink-enso", "Seeker_Ink_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#d8d8f0"},
    {"look": 15, "id": "glass", "flight": ("glass-shard", "Seeker_Glass_Flight"), "burst": ("glass-rose", "Seeker_Glass_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#5a5ae8"},
    {"look": 16, "id": "firework", "flight": ("firework-rocket", "Seeker_Firework_Flight"), "burst": ("firework-bloom", "Seeker_Firework_Burst"),
     "path": "line", "arcPx": 16, "burstAt": "body", "slash": "#ff4ab4"},
    {"look": 17, "id": "bone", "flight": ("bone-crawl", "Seeker_Bone_Flight"), "burst": ("bone-grasp", "Seeker_Bone_Burst"),
     "path": "ground", "arcPx": 0, "burstAt": "ground", "slash": "#e4dcc8"},
    {"look": 18, "id": "bat", "flight": ("bat-flight", "Seeker_Bat_Flight"), "burst": ("bat-swarm", "Seeker_Bat_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#9a48ec"},
    {"look": 19, "id": "golem", "flight": ("golem-rubble", "Seeker_Golem_Flight"), "burst": ("golem-fist", "Seeker_Golem_Burst"),
     "path": "line", "arcPx": 10, "burstAt": "ground", "slash": "#e8a218"},
    {"look": 20, "id": "serpent", "flight": ("serpent-wave", "Seeker_Serpent_Flight"), "burst": ("serpent-strike", "Seeker_Serpent_Burst"),
     "path": "ground", "arcPx": 0, "burstAt": "ground", "slash": "#2ac8a0"},
    {"look": 21, "id": "samurai", "flight": ("samurai-dash", "Seeker_Samurai_Flight"), "burst": ("samurai-iai", "Seeker_Samurai_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "body", "slash": "#ff4a3a"},
    {"look": 22, "id": "raven", "flight": ("raven-dive", "Seeker_Raven_Flight"), "burst": ("raven-omen", "Seeker_Raven_Burst"),
     "path": "line", "arcPx": 12, "burstAt": "body", "slash": "#2cb4d8"},
    {"look": 23, "id": "vine", "flight": ("vine-seed", "Seeker_Vine_Flight"), "burst": ("vine-maw", "Seeker_Vine_Burst"),
     "path": "line", "arcPx": 6, "burstAt": "ground", "slash": "#8cd040"},
    {"look": 24, "id": "guardian", "flight": ("guardian-spear", "Seeker_Guardian_Flight"), "burst": ("guardian-descent", "Seeker_Guardian_Burst"),
     "path": "line", "arcPx": 0, "burstAt": "ground", "slash": "#ffd84a"},
]


def is_green(pixel):
    red, green, blue, alpha = pixel
    return green > red + 20 and green > blue + 10 or red > 200 and green > 240 and blue > 200


def is_slash(pixel, x, y, frame):
    if pixel[3] == 0 or frame < FIRST_SLASH_FRAME:
        return False
    if pixel[3] < 255:
        return True
    left, top, right, bottom = FACE_BOX
    in_face = left <= x <= right and top <= y <= bottom
    return is_green(pixel) and not in_face


def split_attack():
    sheet = Image.open(ATTACK).convert("RGBA")
    body = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
    slash = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
    width, height = ATTACK_CELL
    source = sheet.load()
    for frame in range(sheet.width // width):
        for y in range(height):
            for x in range(width):
                pixel = source[frame * width + x, y]
                if pixel[3] == 0:
                    continue
                target = slash if is_slash(pixel, x, y, frame) else body
                target.putpixel((frame * width + x, y), pixel)
    frames = sheet.width // width
    body.save(RESOURCES / f"Seeker_Void_Attack1Body_{width}x{height}_{frames}f.png")
    slash.save(RESOURCES / f"Seeker_Void_Attack1Slash_{width}x{height}_{frames}f.png")


def write_meta(sheet):
    meta = sheet.with_name(sheet.name + ".meta")
    if meta.exists():
        return
    lines = SHEET_META.read_text().splitlines()
    lines[1] = "guid: " + uuid.uuid4().hex
    meta.write_text("\n".join(lines) + "\n")


def pack_effect(source, prefix):
    data = json.loads((STRIKE / f"{source}.json").read_text())["meta"]["pixelfx"]
    sheet = Image.open(STRIKE / f"{source}.png").convert("RGBA")
    width, height = data["frameSize"]
    frames = [
        sheet.crop((index % data["columns"] * width, index // data["columns"] * height,
                    index % data["columns"] * width + width, index // data["columns"] * height + height))
        for index in range(data["frames"])
    ]
    while frames and not frames[-1].getbbox():
        frames.pop()
    boxes = [frame.getbbox() for frame in frames if frame.getbbox()]
    left = min(box[0] for box in boxes)
    top = min(box[1] for box in boxes)
    right = max(box[2] for box in boxes)
    bottom = max(box[3] for box in boxes)
    cell = (right - left, bottom - top)
    strip = Image.new("RGBA", (cell[0] * len(frames), cell[1]), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        strip.paste(frame.crop((left, top, right, bottom)), (index * cell[0], 0))
    name = f"{prefix}_{cell[0]}x{cell[1]}_{len(frames)}f"
    for stale in RESOURCES.glob(f"{prefix}_*f.png"):
        if stale.stem != name:
            print(f"stale sheet left in place: {stale.name}")
    strip.save(RESOURCES / f"{name}.png")
    write_meta(RESOURCES / f"{name}.png")
    return {"file": name, "width": cell[0], "height": cell[1], "frames": len(frames),
            "pivot": [data["pivot"][0] - left, data["pivot"][1] - top], "fps": data["fps"]}


def main():
    split_attack()
    strikes = []
    for strike in STRIKES:
        strikes.append({
            "look": strike["look"], "id": strike["id"], "path": strike["path"], "arcPx": strike["arcPx"],
            "burstAt": strike["burstAt"], "slash": strike["slash"],
            "flight": pack_effect(*strike["flight"]), "burst": pack_effect(*strike["burst"]),
        })
    MANIFEST.write_text(json.dumps({"version": 1, "strikes": strikes}, indent=2) + "\n")


if __name__ == "__main__":
    main()
