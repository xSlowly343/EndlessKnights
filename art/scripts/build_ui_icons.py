import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

SUPERSAMPLE = 4
PREVIEW_CELL = 88
PREVIEW_COLUMNS = 12
PREVIEW_LABEL = 22
PREVIEW_BACKGROUND = "#1B2236"
PREVIEW_TEXT = "#E6E9F0"
GEMS_PATH = "Assets/StreamingAssets/Data/skills.json"


def hex_rgba(value, alpha=255):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


def load_mask(path):
    alpha = Image.open(path).convert("RGBA").getchannel("A")
    box = alpha.getbbox()
    if box is None:
        raise ValueError(f"empty icon: {path}")
    return alpha.crop(box)


def fit_mask(mask, canvas, padding):
    inner = round(canvas * (1 - 2 * padding))
    scale = inner / max(mask.size)
    size = (max(1, round(mask.width * scale)), max(1, round(mask.height * scale)))
    resized = mask.resize(size, Image.LANCZOS)
    placed = Image.new("L", (canvas, canvas), 0)
    placed.paste(resized, ((canvas - size[0]) // 2, (canvas - size[1]) // 2))
    return placed


def dilate(mask, radius):
    spread = mask.filter(ImageFilter.GaussianBlur(radius / 2))
    return spread.point(lambda value: min(255, value * 6))


def render_icon(source, size, padding, outline):
    canvas = size * SUPERSAMPLE
    glyph = fit_mask(load_mask(source), canvas, padding)
    border = dilate(glyph, size * outline["widthFraction"] * SUPERSAMPLE)
    image = Image.new("RGBA", (canvas, canvas), hex_rgba(outline["color"], 0))
    image.paste(Image.new("RGBA", image.size, hex_rgba(outline["color"])), mask=border)
    image.paste(Image.new("RGBA", image.size, (255, 255, 255, 255)), mask=glyph)
    return image.resize((size, size), Image.LANCZOS)


def gem_colors(root):
    path = root / GEMS_PATH
    gems = json.loads(path.read_text(encoding="utf-8"))["gems"]
    return {gem["id"]: (gem["kind"], gem.get("color", "white")) for gem in gems}


def tile_color(icon, group, manifest, colors):
    if group["tile"] != "attribute":
        return group["tile"]
    attribute = icon.get("attribute") or colors.get(icon["id"], ("skill", "white"))[1]
    return manifest["attributes"][attribute]


def check_gems(manifest, colors):
    covered = {(icon["group"], icon["id"]) for icon in manifest["icons"]}
    missing = []
    for gem_id in colors:
        if ("skills", gem_id) not in covered:
            missing.append(gem_id)
    return missing


def check_duplicates(manifest):
    seen = set()
    duplicates = []
    for icon in manifest["icons"]:
        key = (icon["group"], icon["id"])
        if key in seen:
            duplicates.append(f"{key[0]}/{key[1]}")
        seen.add(key)
    return duplicates


def preview_tile(icon_image, color):
    tile = Image.new("RGBA", (PREVIEW_CELL - 8, PREVIEW_CELL - 8 - PREVIEW_LABEL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rounded_rectangle((0, 0, tile.width - 1, tile.height - 1), radius=10, fill=hex_rgba(color))
    glyph = icon_image.copy()
    glyph.thumbnail((tile.height - 10, tile.height - 10), Image.LANCZOS)
    tile.alpha_composite(glyph, ((tile.width - glyph.width) // 2, (tile.height - glyph.height) // 2))
    return tile


def write_preview(path, entries):
    rows = (len(entries) + PREVIEW_COLUMNS - 1) // PREVIEW_COLUMNS
    sheet = Image.new("RGBA", (PREVIEW_COLUMNS * PREVIEW_CELL, rows * PREVIEW_CELL), hex_rgba(PREVIEW_BACKGROUND))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for index, (label, image, color) in enumerate(entries):
        x = index % PREVIEW_COLUMNS * PREVIEW_CELL + 4
        y = index // PREVIEW_COLUMNS * PREVIEW_CELL + 4
        sheet.alpha_composite(preview_tile(image, color), (x, y))
        draw.text((x, y + PREVIEW_CELL - 8 - PREVIEW_LABEL + 4), label[:14], fill=hex_rgba(PREVIEW_TEXT), font=font)
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path, optimize=True)


def build(root, manifest_path, preview_only):
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    colors = gem_colors(root)
    problems = [f"no icon for gem {gem}" for gem in check_gems(manifest, colors)]
    problems += [f"duplicate icon {key}" for key in check_duplicates(manifest)]
    sources = root / manifest["sources"]
    output = root / manifest["output"]
    entries = []
    for icon in manifest["icons"]:
        group = manifest["groups"][icon["group"]]
        source = sources / f"{icon['source']}.png"
        if not source.exists():
            problems.append(f"missing source {icon['source']}")
            continue
        image = render_icon(source, group["size"], group["padding"], manifest["outline"])
        if not preview_only:
            target = output / icon["group"] / f"{icon['id']}.png"
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target, optimize=True)
        entries.append((f"{icon['group']}/{icon['id']}", image, tile_color(icon, group, manifest, colors)))
    write_preview(root / manifest["preview"], entries)
    for problem in problems:
        print(f"ERROR {problem}")
    print(f"icons: {len(entries)}, groups: {len(manifest['groups'])}, output: {manifest['output']}")
    return 1 if problems else 0


def main():
    parser = argparse.ArgumentParser(description="Build UI icon sprites from art/ui/icons.json")
    parser.add_argument("--root", default=str(Path(__file__).resolve().parents[2]))
    parser.add_argument("--manifest", default="art/ui/icons.json")
    parser.add_argument("--preview-only", action="store_true")
    args = parser.parse_args()
    root = Path(args.root)
    sys.exit(build(root, root / args.manifest, args.preview_only))


if __name__ == "__main__":
    main()
