import argparse
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ENEMIES = ROOT / "art" / "enemies"
SPRITES = ROOT / "art" / "out" / "sprites"
PREVIEW = ROOT / "art" / "out" / "preview"
PREVIEW_BACKGROUND = (178, 214, 160, 255)
BAND_GAP = 2


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--roster", default="green_hills")
    parser.add_argument("--packs", required=True)
    parser.add_argument("--only", default=None)
    return parser.parse_args()


def grid_frames(sheet, cell, row):
    width, height = cell
    frames = []
    for column in range(sheet.width // width):
        frame = sheet.crop((column * width, row * height, (column + 1) * width, (row + 1) * height))
        if frame.getbbox():
            frames.append(frame)
    return frames


def filled_runs(filled, length):
    runs, start, gap, end = [], None, 0, 0
    for index in range(length):
        if filled(index):
            start = index if start is None else start
            gap, end = 0, index + 1
        elif start is not None:
            gap += 1
            if gap >= BAND_GAP:
                runs.append((start, end))
                start = None
    if start is not None:
        runs.append((start, end))
    return runs


def band_frames(sheet, band, slot, right_edge, min_width=12):
    top, bottom = band
    alpha = sheet.getchannel("A")
    columns = filled_runs(lambda x: alpha.crop((x, top, x + 1, bottom)).getbbox() is not None, right_edge)
    frames = []
    for left, right in columns:
        if right - left < min_width:
            continue
        part = sheet.crop((left, top, right, bottom))
        frame = Image.new("RGBA", (slot, bottom - top))
        frame.alpha_composite(part, ((slot - part.width) // 2, 0))
        frames.append(frame)
    return frames


def strip_frames(packs, strip, width):
    sheet = Image.open(packs / strip["file"]).convert("RGBA")
    frame_width = strip["width"]
    frame_height = strip.get("height", sheet.height)
    rows = [strip["row"]] if "row" in strip else range(sheet.height // frame_height)
    frames = []
    for row in rows:
        for column in range(sheet.width // frame_width):
            box = (column * frame_width, row * frame_height, (column + 1) * frame_width, (row + 1) * frame_height)
            part = sheet.crop(box)
            if part.getbbox() is None:
                continue
            frame = Image.new("RGBA", (width, frame_height))
            frame.alpha_composite(part, ((width - frame_width) // 2, 0))
            frames.append(frame)
    return frames


def resampled(frame, factor):
    size = (max(1, round(frame.width * factor)), max(1, round(frame.height * factor)))
    shrunk = frame.resize(size, Image.BOX)
    alpha = shrunk.getchannel("A").point(lambda value: 255 if value > 110 else 0)
    shrunk.putalpha(alpha)
    return shrunk


def trimmed_to_feet(frames):
    feet = max(frame.getbbox()[3] for frame in frames)
    return [frame.crop((0, 0, frame.width, feet)) for frame in frames]


def bottom_aligned(frames, height):
    aligned = []
    for frame in frames:
        canvas = Image.new("RGBA", (frame.width, height))
        canvas.alpha_composite(frame, (0, height - frame.height))
        aligned.append(canvas)
    return aligned


def hue_shifted(frame, degrees):
    alpha = frame.getchannel("A")
    hue, saturation, value = frame.convert("RGB").convert("HSV").split()
    offset = round(degrees * 256 / 360)
    hue = hue.point(lambda h: (h + offset) % 256)
    shifted = Image.merge("HSV", (hue, saturation, value)).convert("RGBA")
    shifted.putalpha(alpha)
    return shifted


def source_anims(spec, packs):
    if "strips" in spec:
        width = max(strip["width"] for strip in spec["strips"].values())
        return {name: strip_frames(packs, strip, width) for name, strip in spec["strips"].items()}
    sheet = Image.open(packs / spec["source"]).convert("RGBA")
    if "bands" in spec:
        right_edge = spec.get("right_edge", sheet.width)
        anims = {name: trimmed_to_feet(band_frames(sheet, band, spec["slot"], right_edge)) for name, band in spec["bands"].items()}
        height = max(frames[0].height for frames in anims.values())
        return {name: bottom_aligned(frames, height) for name, frames in anims.items()}
    return {name: grid_frames(sheet, tuple(spec["cell"]), row) for name, row in spec["rows"].items()}


def load_anims(spec, packs):
    anims = source_anims(spec, packs)
    if "resample" in spec:
        anims = {name: [resampled(frame, spec["resample"]) for frame in frames] for name, frames in anims.items()}
    if "hue_shift" in spec:
        anims = {name: [hue_shifted(frame, spec["hue_shift"]) for frame in frames] for name, frames in anims.items()}
    if spec["mirror"]:
        anims = {name: [frame.transpose(Image.FLIP_LEFT_RIGHT) for frame in frames] for name, frames in anims.items()}
    return anims


def scaled(frame, scale):
    return frame.resize((frame.width * scale, frame.height * scale), Image.NEAREST)


def baseline(frames):
    return max(frame.getbbox()[3] for frame in frames)


def build(name, spec, roster, packs):
    scale = spec.get("scale", roster["scale"])
    anims = load_anims(spec, packs)
    cell = (anims["idle"][0].width * scale, anims["idle"][0].height * scale)
    columns = max(len(frames) for frames in anims.values())
    sheet = Image.new("RGBA", (cell[0] * columns, cell[1] * len(anims)))
    feet = baseline(anims["idle"]) * scale
    meta = {
        "entity": name,
        "cell": list(cell),
        "pivot": [0.5, round((cell[1] - feet) / cell[1], 4)],
        "fps": roster["fps"],
        "boss": spec.get("boss", False),
        "anims": {},
    }
    for row, (anim, frames) in enumerate(anims.items()):
        for column, frame in enumerate(frames):
            sheet.alpha_composite(scaled(frame, scale), (column * cell[0], row * cell[1]))
        meta["anims"][anim] = {"row": row, "frames": len(frames), "loop": anim in roster["loop"]}
    meta["hit_frame"] = spec.get("hit_frame", len(anims["attack"]) // 2)
    out = SPRITES / name
    out.mkdir(parents=True, exist_ok=True)
    sheet.save(out / "sheet.png", optimize=True)
    (out / f"{name}.json").write_text(json.dumps(meta, indent=2) + "\n")
    PREVIEW.mkdir(parents=True, exist_ok=True)
    backdrop = Image.new("RGBA", sheet.size, PREVIEW_BACKGROUND)
    backdrop.alpha_composite(sheet)
    backdrop.save(PREVIEW / f"sheet_{name}.png", optimize=True)
    return meta


def main():
    args = parse_args()
    roster = json.loads((ENEMIES / f"{args.roster}.json").read_text())
    packs = Path(args.packs)
    for name, spec in roster["enemies"].items():
        if args.only and name != args.only:
            continue
        meta = build(name, spec, roster, packs)
        frames = ", ".join(f"{anim} {info['frames']}" for anim, info in meta["anims"].items())
        print(f"{name}: cell {meta['cell'][0]}x{meta['cell'][1]}, pivot {meta['pivot'][1]}, {frames}")


main()
