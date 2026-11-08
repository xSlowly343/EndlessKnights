import argparse
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ENTITIES = ROOT / "art" / "entities"
SPRITES = ROOT / "art" / "out" / "sprites"
HEIGHT_TOLERANCE = 4
MAX_SHEET_BYTES = 1024 * 1024
OPAQUE = 128
DARK_LUMA = 90
MIN_DARK_EDGE_SHARE = 0.75
MAX_HALO_LUMA = 140
MAX_SILHOUETTE_OVERLAP = 0.85
HIT_ANIMS = {"attack"}


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--entity", required=True)
    parser.add_argument("--variant", default=None)
    return parser.parse_args()


def luma(pixel):
    r, g, b = pixel[:3]
    return 0.299 * r + 0.587 * g + 0.114 * b


def cells(sheet, meta):
    width, height = meta["cell"]
    for anim, info in meta["anims"].items():
        for frame in range(info["frames"]):
            box = (frame * width, info["row"] * height, (frame + 1) * width, (info["row"] + 1) * height)
            yield anim, frame, sheet.crop(box)


def mask(cell):
    return cell.getchannel("A").point(lambda v: 255 if v >= OPAQUE else 0)


def edge_pixels(cell):
    alpha = mask(cell).load()
    width, height = cell.size
    for y in range(height):
        for x in range(width):
            if alpha[x, y] and any(
                not (0 <= x + dx < width and 0 <= y + dy < height) or not alpha[x + dx, y + dy]
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
            ):
                yield x, y


def check_size(entity, anim, frame, cell, errors):
    box = cell.getchannel("A").getbbox()
    if box is None:
        errors.append(f"{anim}[{frame}]: empty frame")
        return
    left, top, right, bottom = box
    if left == 0 or top == 0 or right == cell.width or bottom == cell.height:
        errors.append(f"{anim}[{frame}]: figure touches the cell border {box}")
    if anim == "idle":
        height = bottom - top
        if abs(height - entity["height_px"]) > HEIGHT_TOLERANCE:
            errors.append(f"{anim}[{frame}]: height {height}px, expected {entity['height_px']}±{HEIGHT_TOLERANCE}")


def check_alpha(anim, frame, cell, errors):
    pixels = cell.load()
    soft = [pixels[x, y] for y in range(cell.height) for x in range(cell.width) if 0 < pixels[x, y][3] < 255]
    if soft and sum(luma(p) for p in soft) / len(soft) > MAX_HALO_LUMA:
        errors.append(f"{anim}[{frame}]: light halo on semi-transparent edge")


def check_outline(anim, frame, cell, errors):
    pixels = cell.load()
    edge = list(edge_pixels(cell))
    if not edge:
        return
    dark = sum(1 for x, y in edge if luma(pixels[x, y]) < DARK_LUMA)
    if dark / len(edge) < MIN_DARK_EDGE_SHARE:
        errors.append(f"{anim}[{frame}]: outline visible on {dark / len(edge):.0%} of the edge")


def overlap(a, b):
    pa, pb = a.load(), b.load()
    both = either = 0
    for y in range(a.height):
        for x in range(a.width):
            inside_a, inside_b = pa[x, y] > 0, pb[x, y] > 0
            both += inside_a and inside_b
            either += inside_a or inside_b
    return both / either if either else 0.0


def silhouette(name, meta, sheet):
    width, height = meta["cell"]
    row = meta["anims"]["idle"]["row"]
    return mask(sheet.crop((0, row * height, width, (row + 1) * height)))


def check_silhouettes(entity, name, meta, sheet, warnings):
    own = silhouette(name, meta, sheet)
    for other_dir in SPRITES.iterdir():
        other_json = other_dir / f"{other_dir.name}.json"
        if other_dir.name == name or not other_json.exists():
            continue
        other_entity_path = ENTITIES / other_dir.name / "entity.json"
        if not other_entity_path.exists():
            continue
        other_entity = json.loads(other_entity_path.read_text())
        if other_entity.get("biome") != entity.get("biome") or other_entity.get("type") != entity.get("type"):
            continue
        other_meta = json.loads(other_json.read_text())
        if other_meta["cell"] != meta["cell"]:
            continue
        score = overlap(own, silhouette(other_dir.name, other_meta, Image.open(other_dir / "sheet.png").convert("RGBA")))
        if score > MAX_SILHOUETTE_OVERLAP:
            warnings.append(f"silhouette overlaps {other_dir.name} by {score:.0%}")


def main():
    args = parse_args()
    name = args.variant or args.entity
    entity = json.loads((ENTITIES / args.entity / "entity.json").read_text())
    folder = SPRITES / name
    meta = json.loads((folder / f"{name}.json").read_text())
    sheet_path = folder / "sheet.png"
    sheet = Image.open(sheet_path).convert("RGBA")
    errors, warnings = [], []
    if sheet_path.stat().st_size > MAX_SHEET_BYTES:
        errors.append(f"sheet is {sheet_path.stat().st_size} bytes, limit {MAX_SHEET_BYTES}")
    for anim in HIT_ANIMS & set(meta["anims"]):
        if anim not in meta.get("hit_frame", {}):
            errors.append(f"{anim}: hit_frame is missing")
    for anim, frame, cell in cells(sheet, meta):
        check_size(entity, anim, frame, cell, errors)
        check_alpha(anim, frame, cell, errors)
        check_outline(anim, frame, cell, errors)
    check_silhouettes(entity, name, meta, sheet, warnings)
    for line in warnings:
        print(f"WARN  {name}: {line}")
    for line in errors:
        print(f"FAIL  {name}: {line}")
    if errors:
        sys.exit(1)
    print(f"OK    {name}: {sum(i['frames'] for i in meta['anims'].values())} frames, {sheet_path.stat().st_size // 1024} KB")


main()
