import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Assets" / "Game" / "Art" / "Resources" / "Heroes" / "Seeker"
STRIKE = ROOT / "art" / "heroes" / "seeker" / "strike"
ATTACK = RESOURCES / "Seeker_Void_Attack1_90x50_8f.png"
ATTACK_CELL = (90, 50)
FIRST_SLASH_FRAME = 3
FACE_BOX = (12, 18, 24, 25)
EFFECTS = {"void-crescent": "Crescent", "void-burst": "Burst"}


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


def pack_effect(source, name):
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
    target = f"Seeker_Void_{name}_{cell[0]}x{cell[1]}_{len(frames)}f.png"
    for stale in RESOURCES.glob(f"Seeker_Void_{name}_*f.png"):
        if stale.name != target:
            print(f"stale sheet left in place: {stale.name}")
    strip.save(RESOURCES / target)
    pivot = (data["pivot"][0] - left, data["pivot"][1] - top)
    print(f"{target}: pivot {pivot}, {data['fps']} fps")


def main():
    split_attack()
    for source, name in EFFECTS.items():
        pack_effect(source, name)


if __name__ == "__main__":
    main()
