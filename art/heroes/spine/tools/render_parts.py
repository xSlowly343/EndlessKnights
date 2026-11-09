import io
import json
import os
import sys

import cairosvg
import numpy as np
from PIL import Image

from body import Body
from svgkit import UNIT

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
PROFILES = os.path.join(ROOT, "shared", "body_profiles.json")
PREVIEW_DEFAULTS = ("hand_r_relaxed", "hand_l_relaxed", "eye_r_open", "eye_l_open")
PREVIEW_SKIP = ("hand_", "finger_", "eye_", "mouth_", "nose", "brow_")
OUTFIT = ("tunic", "belt", "sleeve_", "bracer_", "pants_", "boot_")
WORLD = (-10, -1, 10, 57)


def quantize(img, colors):
    a = np.asarray(img.convert("RGBA")).astype(np.int32)
    pal = np.array(sorted(set(colors)), dtype=np.int32)
    rgb = a[..., :3].reshape(-1, 1, 3)
    idx = ((rgb - pal[None, :, :]) ** 2).sum(-1).argmin(1)
    out = np.zeros_like(a)
    out[..., :3] = pal[idx].reshape(a.shape[0], a.shape[1], 3)
    out[..., 3] = np.where(a[..., 3] >= 128, 255, 0)
    out[out[..., 3] == 0] = 0
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def render_part(part):
    svg, bb = part.svg()
    png = cairosvg.svg2png(bytestring=svg.encode())
    img = quantize(Image.open(io.BytesIO(png)), part.colors())
    return svg, bb, img


def visible(name, outfit):
    if name in PREVIEW_DEFAULTS:
        return True
    if name.startswith(PREVIEW_SKIP):
        return False
    if name.startswith(OUTFIT):
        return outfit
    return True


def compose(entries, outfit):
    x0, y0, x1, y1 = WORLD
    canvas = Image.new("RGBA", ((x1 - x0) * UNIT, (y1 - y0) * UNIT), (0, 0, 0, 0))
    for e in sorted(entries, key=lambda e: e["layer"]):
        if not visible(e["name"], outfit):
            continue
        img = e["image"]
        if e["tint"] != 1.0:
            arr = np.asarray(img).astype(np.float32)
            arr[..., :3] *= e["tint"]
            img = Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA")
        bx0, by0, bx1, by1 = e["bbox"]
        canvas.alpha_composite(img, ((bx0 - x0) * UNIT, (y1 - by1) * UNIT))
    return canvas


def pixelate(img):
    a = np.asarray(img)
    return Image.fromarray(a[UNIT // 2::UNIT, UNIT // 2::UNIT].copy(), "RGBA")


def on_bg(img, color=(38, 32, 52)):
    bg = Image.new("RGBA", img.size, color + (255,))
    bg.alpha_composite(img)
    return bg


def build(sex, profile):
    body = Body(sex, profile).build_all()
    base = os.path.join(ROOT, "base", sex, "artwork")
    os.makedirs(os.path.join(base, "svg"), exist_ok=True)
    os.makedirs(os.path.join(base, "png"), exist_ok=True)
    entries, manifest = [], []
    for item in body.parts:
        part = item["part"]
        svg, bb, img = render_part(part)
        with open(os.path.join(base, "svg", part.name + ".svg"), "w") as f:
            f.write(svg)
        img.save(os.path.join(base, "png", part.name + ".png"))
        entries.append({"name": part.name, "bbox": bb, "image": img, "layer": item["layer"], "tint": item["tint"]})
        manifest.append({"name": part.name, "slot": item["slot"], "layer": item["layer"], "tint": item["tint"],
                         "bbox": list(bb), "size": list(img.size),
                         "pivots": {k: [round(v[0], 3), round(v[1], 3)] for k, v in item["pivots"].items()}})
    with open(os.path.join(base, "parts.json"), "w") as f:
        json.dump({"sex": sex, "unit": UNIT, "joints": profile["joints"], "parts": manifest}, f, indent=1)
    return entries


def main():
    profiles = json.load(open(PROFILES))
    out = os.path.join(ROOT, "base", "preview")
    os.makedirs(out, exist_ok=True)
    hd, px = [], []
    for sex in ("male", "female"):
        entries = build(sex, profiles[sex])
        for outfit in (False, True):
            img = compose(entries, outfit)
            hd.append(img)
            px.append(pixelate(img))
    gap = 8
    w = sum(i.width for i in hd) + gap * (len(hd) + 1)
    sheet = Image.new("RGBA", (w, hd[0].height + 2 * gap), (38, 32, 52, 255))
    x = gap
    for i in hd:
        sheet.alpha_composite(i, (x, gap))
        x += i.width + gap
    sheet.save(os.path.join(out, "lineup_4x.png"))
    scale = 4
    pw = sum(i.width * scale for i in px) + gap * (len(px) + 1)
    psheet = Image.new("RGBA", (pw, px[0].height * scale + 2 * gap), (38, 32, 52, 255))
    x = gap
    for i in px:
        psheet.alpha_composite(i.resize((i.width * scale, i.height * scale), Image.NEAREST), (x, gap))
        x += i.width * scale + gap
    psheet.save(os.path.join(out, "lineup_1x_x4.png"))
    print("ok", len(hd))


if __name__ == "__main__" and len(sys.argv) == 1:
    sys.exit(main())


def contact_sheet(sex, columns=12, cell=80):
    base = os.path.join(ROOT, "base", sex, "artwork")
    parts = json.load(open(os.path.join(base, "parts.json")))["parts"]
    rows = (len(parts) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell, rows * cell), (38, 32, 52, 255))
    for i, p in enumerate(parts):
        img = Image.open(os.path.join(base, "png", p["name"] + ".png"))
        k = max(1, min(cell // max(img.size), 4)) if max(img.size) < cell else 1
        if max(img.size) > cell:
            r = cell / max(img.size)
            img = img.resize((max(1, int(img.width * r)), max(1, int(img.height * r))), Image.NEAREST)
        else:
            img = img.resize((img.width * k, img.height * k), Image.NEAREST)
        x, y = (i % columns) * cell, (i // columns) * cell
        sheet.alpha_composite(img, (x + (cell - img.width) // 2, y + (cell - img.height) // 2))
    sheet.save(os.path.join(ROOT, "base", "preview", f"parts_{sex}.png"))


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "sheets":
    contact_sheet("male")
    contact_sheet("female")
