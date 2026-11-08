import argparse
import importlib.util
import json
import sys
from pathlib import Path

import bpy

SCRIPTS = Path(__file__).resolve().parent
ROOT = SCRIPTS.parents[1]
sys.path.insert(0, str(SCRIPTS))

import toon

ENTITIES = ROOT / "art" / "entities"
TMP = ROOT / "art" / "out" / "tmp"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument("--entity", required=True)
    parser.add_argument("--anims", default="all")
    parser.add_argument("--variant", default=None)
    parser.add_argument("--samples", type=int, default=16)
    parser.add_argument("--blend", action="store_true")
    parser.add_argument("--first", action="store_true")
    return parser.parse_args(argv)


def load_model(entity_id):
    spec = importlib.util.spec_from_file_location(f"model_{entity_id}", ENTITIES / entity_id / "model.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def variant_colors(entity, variant_id):
    colors = dict(entity["colors"])
    if variant_id:
        variant = next(v for v in entity["variants"] if v["id"] == variant_id)
        colors.update(variant.get("colors", {}))
        colors["swap"] = variant.get("swap", {})
    return colors


def main():
    args = parse_args()
    entity = json.loads((ENTITIES / args.entity / "entity.json").read_text())
    model = load_model(args.entity)
    scene = toon.reset(args.samples)
    rig = model.build(variant_colors(entity, args.variant))
    toon.lights()
    toon.camera(entity["cell"], entity["facing"])
    anims = entity["anims"] if args.anims == "all" else {a: entity["anims"][a] for a in args.anims.split(",")}
    name = args.variant or args.entity
    out = TMP / args.entity / name
    out.mkdir(parents=True, exist_ok=True)
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=str(out / f"{name}.blend"))
    for anim, count in anims.items():
        for frame in range(1 if args.first else count):
            model.pose(rig, anim, frame, count)
            bpy.context.view_layer.update()
            scene.render.filepath = str(out / f"{anim}_{frame:02d}.png")
            bpy.ops.render.render(write_still=True)


main()
