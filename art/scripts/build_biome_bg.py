import argparse
import json
import math
import random
import sys
from pathlib import Path

import bpy
import bmesh

SCRIPTS = Path(__file__).resolve().parent
ROOT = SCRIPTS.parents[1]
sys.path.insert(0, str(SCRIPTS))

import toon

PALETTES = ROOT / "art" / "palettes" / "biomes.json"
OUT_DIR = ROOT / "art" / "out" / "backgrounds"

TILE_PX = 960
HEIGHT_PX = 180
PX_PER_UNIT = 20
SUPERSAMPLE = 4
TILE_W = TILE_PX / PX_PER_UNIT
VIEW_H = HEIGHT_PX / PX_PER_UNIT
HALF = TILE_W / 2
STEP = 0.1
GROUPS = {
    "back": ("sky", "clouds", "bank", "far", "near"),
    "mid": ("treeline", "midground", "trees"),
    "ground": ("ground",),
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument("--biome", default="green_hills")
    parser.add_argument("--seed", type=int, default=11)
    parser.add_argument("--samples", type=int, default=16)
    parser.add_argument("--blend", action="store_true")
    return parser.parse_args(argv)


def flat(name, color):
    return toon.toon_material(name, color, glow=True)


def gradient(name, bottom, top, z0, z1):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    nodes = tree.nodes
    nodes.clear()
    coords = nodes.new("ShaderNodeTexCoord")
    separate = nodes.new("ShaderNodeSeparateXYZ")
    span = nodes.new("ShaderNodeMapRange")
    span.inputs["From Min"].default_value = z0
    span.inputs["From Max"].default_value = z1
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = toon.rgb(bottom)
    ramp.color_ramp.elements[1].color = toon.rgb(top)
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    tree.links.new(coords.outputs["Object"], separate.inputs[0])
    tree.links.new(separate.outputs["Z"], span.inputs["Value"])
    tree.links.new(span.outputs[0], ramp.inputs[0])
    tree.links.new(ramp.outputs[0], emission.inputs[0])
    tree.links.new(emission.outputs[0], output.inputs[0])
    return material


def link(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def polygon(name, collection, material, top, depth, bottom=-1.0, x0=-HALF - 1.0, x1=HALF + 1.0):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    count = max(2, int((x1 - x0) / STEP) + 1)
    xs = [x0 + (x1 - x0) * i / (count - 1) for i in range(count)]
    upper = [bm.verts.new((x, depth, top(x))) for x in xs]
    lower = [bm.verts.new((x, depth, bottom)) for x in reversed(xs)]
    bm.faces.new(upper + lower)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(material)
    return link(obj, collection)


def waves(spec):
    return lambda x: sum(a * math.sin(2 * math.pi * k * x / TILE_W + p) for a, k, p in spec)


def bumps(circles):
    def top(x):
        best = -10.0
        for cx, cz, r in circles:
            for shift in (-TILE_W, 0.0, TILE_W):
                dx = x - cx - shift
                if abs(dx) < r:
                    best = max(best, cz + math.sqrt(r * r - dx * dx))
        return best
    return top


def bump_row(rng, base, radius, spacing, jitter):
    circles = []
    x = -HALF
    while x < HALF:
        r = radius * rng.uniform(0.75, 1.25)
        circles.append((x, base + rng.uniform(-jitter, jitter), r))
        x += spacing * rng.uniform(0.7, 1.2)
    return circles


def wrapped(x, extent):
    shifts = [0.0]
    if x - extent < -HALF:
        shifts.append(TILE_W)
    if x + extent > HALF:
        shifts.append(-TILE_W)
    return [x + s for s in shifts]


def build_sky(collections, palette, rng):
    polygon("sky", collections["sky"], gradient("sky", palette["sky_bottom"], palette["sky_top"], 3.0, VIEW_H), lambda x: VIEW_H + 1, 90.0)
    light = flat("cloud", palette["cloud"])
    shadow = flat("cloud_shadow", palette["cloud_shadow"])
    for index, (cx, cz, width) in enumerate([(-18.0, 7.2, 4.4), (-6.5, 8.1, 2.4), (5.0, 7.0, 5.2), (16.5, 7.9, 3.0), (-11.5, 6.4, 1.6)]):
        circles = []
        x = cx - width / 2
        while x < cx + width / 2:
            middle = 1 - abs(x - cx) / (width / 2)
            circles.append((x, cz - 0.1, 0.25 + 0.75 * math.sqrt(max(0.0, middle)) * rng.uniform(0.7, 1.1)))
            x += 0.45
        top = bumps(circles)
        for xs in wrapped(cx, width / 2 + 0.5):
            shift = xs - cx
            body = lambda x, s=shift: max(cz, top(x - s))
            polygon(f"cloud_{index}_{xs:.0f}", collections["clouds"], light, body, 80.0, bottom=cz, x0=xs - width / 2 - 0.4, x1=xs + width / 2 + 0.4)
            polygon(f"cloud_shadow_{index}_{xs:.0f}", collections["clouds"], shadow, lambda x: cz + 0.12, 79.8, bottom=cz - 0.08, x0=xs - width / 2 + 0.1, x1=xs + width / 2 - 0.1)


def build_bank(collections, palette, rng):
    top = bumps(bump_row(rng, 4.3, 0.9, 1.1, 0.35))
    polygon("bank", collections["bank"], flat("bank", palette["cloud_bank"]), top, 70.0)
    polygon("bank_shadow", collections["bank"], flat("bank_shadow", palette["cloud_bank_shadow"]), lambda x: top(x) - 0.7, 69.8)


def build_far(collections, palette, rng):
    far = waves([(0.45, 1, 0.3), (0.3, 2, 2.4), (0.15, 3, 4.1)])
    top = lambda x: 3.9 + far(x)
    polygon("far_lip", collections["far"], flat("far_lip", palette["far_hill_lip"]), top, 60.0)
    polygon("far_hill", collections["far"], flat("far_hill", palette["far_hill"]), lambda x: top(x) - 0.12, 59.8)
    build_castle(collections["far"], palette, -13.0, top(-13.0) - 0.15)
    near = waves([(0.35, 1, 2.2), (0.3, 2, 0.4), (0.12, 4, 1.9)])
    near_top = lambda x: 3.25 + near(x)
    polygon("near_lip", collections["near"], flat("near_lip", palette["near_hill_lip"]), near_top, 50.0)
    polygon("near_hill", collections["near"], flat("near_hill", palette["near_hill"]), lambda x: near_top(x) - 0.12, 49.8)


def build_castle(collection, palette, cx, base):
    stone = flat("castle", palette["castle"])
    roof = flat("castle_roof", palette["castle_roof"])
    for dx, width, height in [(-0.75, 0.34, 1.5), (-0.25, 0.66, 1.1), (0.3, 0.28, 2.0), (0.7, 0.38, 1.3)]:
        x = cx + dx
        polygon(f"tower_{dx}", collection, stone, lambda _x, h=height: base + h, 58.0, bottom=base, x0=x - width / 2, x1=x + width / 2)
        apex = base + height + width * 1.1
        polygon(
            f"roof_{dx}", collection, roof,
            lambda px, x=x, w=width, h=height, a=apex: base + h + (a - base - h) * max(0.0, 1 - abs(px - x) / (w / 2 + 0.06)),
            57.9, bottom=base + height - 0.01, x0=x - width / 2 - 0.06, x1=x + width / 2 + 0.06,
        )


def build_treeline(collections, palette, rng):
    top = bumps(bump_row(rng, 2.85, 0.42, 0.55, 0.18))
    polygon("treeline", collections["treeline"], flat("treeline", palette["tree_far"]), top, 40.0)
    polygon("treeline_shadow", collections["treeline"], flat("treeline_shadow", palette["tree_far_dark"]), lambda x: top(x) - 0.45, 39.8)


def midground_top():
    shape = waves([(0.12, 2, 0.8), (0.08, 5, 2.3)])
    return lambda x: 2.35 + shape(x)


def build_midground(collections, palette, rng):
    top = midground_top()
    polygon("mid_lip", collections["midground"], flat("mid_lip", palette["mid_ground_lip"]), top, 30.0)
    polygon("mid_ground", collections["midground"], flat("mid_ground", palette["mid_ground"]), lambda x: top(x) - 0.14, 29.8)


def trunk(collection, material, x, base, height, lean, depth):
    curve = bpy.data.curves.new(f"trunk_{x:.2f}", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.11
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(2)
    points = [(x, depth, base - 0.1, 1.25), (x + lean * 0.4, depth, base + height * 0.5, 0.8), (x + lean, depth, base + height, 0.6)]
    for point, (px, py, pz, radius) in zip(spline.bezier_points, points):
        point.co = (px, py, pz)
        point.handle_left_type = point.handle_right_type = "AUTO"
        point.radius = radius
    obj = bpy.data.objects.new(curve.name, curve)
    obj.data.materials.append(material)
    return link(obj, collection)


def crown(collection, material, cx, cz, width, height, depth, rng, puff=0.5):
    count = int(4 * width * height) + 5
    for index in range(count):
        angle = rng.uniform(0, 2 * math.pi)
        radius = math.sqrt(rng.uniform(0, 1))
        x = cx + math.cos(angle) * radius * width * 0.5
        z = cz + math.sin(angle) * radius * height * 0.5
        size = puff * rng.uniform(0.85, 1.15) * (1.2 - 0.4 * radius)
        link(toon.ball(f"puff_{cx:.2f}_{index}", material, (x, depth - (1 - radius) * 0.3, z), (size, size, size * 0.92), outline=0, subdivisions=3), collection)


def build_trees(collections, palette, rng):
    top = midground_top()
    leaves = toon.toon_material("tree", palette["tree"], palette["tree_dark"], palette["tree_light"])
    bark = toon.toon_material("trunk", palette["trunk"], palette["trunk_dark"])
    bush = toon.toon_material("bush", palette["bush"], palette["bush_dark"], palette["bush_light"])
    for index, (tx, scale) in enumerate([(-20.5, 1.0), (-14.0, 0.8), (-6.0, 1.1), (2.5, 0.75), (7.0, 0.95), (15.5, 0.85)]):
        for x in wrapped(tx, 1.4 * scale):
            base = top(x) - 0.1
            lean = rng.uniform(-0.25, 0.25)
            height = 1.25 * scale
            trunk(collections["trees"], bark, x, base, height, lean, 25.0)
            crown(collections["trees"], leaves, x + lean, base + height + 0.35 * scale, 2.2 * scale, 1.35 * scale, 24.0, rng)
    for index, bx in enumerate([-17.0, -9.5, -2.0, 4.5, 11.0, 19.5]):
        for x in wrapped(bx, 0.8):
            crown(collections["trees"], bush, x, top(x) + 0.05, 1.0, 0.35, 23.0, rng, puff=0.26)


def build_ground(collections, palette, rng):
    shape = waves([(0.06, 3, 0.2), (0.04, 7, 1.7)])
    top = lambda x: 1.35 + shape(x)
    collection = collections["ground"]
    polygon("ground_lip", collection, flat("ground_lip", palette["ground_lip"]), top, 10.0)
    polygon("ground", collection, flat("ground", palette["ground"]), lambda x: top(x) - 0.12, 9.9)
    path_shape = waves([(0.05, 2, 0.9), (0.03, 5, 3.1)])
    path_top = lambda x: 0.78 + path_shape(x)
    polygon("path_lip", collection, flat("path_lip", palette["path_lip"]), path_top, 9.0)
    polygon("path", collection, flat("path", palette["path"]), lambda x: path_top(x) - 0.08, 8.9, bottom=-1.0)
    spot = flat("path_spot", palette["path_spot"])
    for index in range(34):
        sx = -HALF + (index + rng.uniform(0.1, 0.9)) * TILE_W / 34
        sz = rng.uniform(0.0, 0.55)
        w = rng.uniform(0.12, 0.3)
        for x in wrapped(sx, w):
            polygon(
                f"spot_{index}_{x:.0f}", collection, spot,
                lambda px, x=x, w=w, z=sz: z + 0.45 * w * math.sqrt(max(0.0, 1 - ((px - x) / w) ** 2)),
                8.5, bottom=sz - 0.0001, x0=x - w, x1=x + w,
            )
    petals = [flat(f"petal_{k}", palette[k]) for k in ("flower_pink", "flower_yellow", "flower_white")]
    for index in range(30):
        fx = -HALF + (index + rng.uniform(0.1, 0.9)) * TILE_W / 30
        for x in wrapped(fx, 0.2):
            fz = top(x) - rng.uniform(0.18, 0.4)
            link(toon.ball(f"flower_{index}_{x:.0f}", rng.choice(petals), (x, 8.0, fz), (0.07, 0.07, 0.07), outline=0, subdivisions=2), collection)


def build_rift(collection, palette):
    zig = [(0.0, 0.0), (0.3, 0.9), (-0.12, 1.8), (0.34, 2.8), (-0.04, 3.7), (0.16, 4.6)]
    for name, color, width, depth in (("rift_glow", palette["rift"], 0.36, 0.6), ("rift_core", palette["rift_core"], 0.12, 0.5)):
        mesh = bpy.data.meshes.new(name)
        bm = bmesh.new()
        left = [bm.verts.new((x - width * (1 - z / 5.2), depth, z + 1.2)) for x, z in zig]
        right = [bm.verts.new((x + width * (1 - z / 5.2), depth, z + 1.2)) for x, z in reversed(zig)]
        bm.faces.new(left + right)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        obj.data.materials.append(flat(name, color))
        link(obj, collection)


def add_camera(scene, center_x, center_z, scale):
    data = bpy.data.cameras.new("camera")
    data.type = "ORTHO"
    data.ortho_scale = scale
    data.clip_end = 300.0
    camera = bpy.data.objects.new("camera", data)
    camera.location = (center_x, -120.0, center_z)
    camera.rotation_euler = (math.radians(90), 0.0, 0.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def render(scene, collections, visible, path):
    for name, collection in collections.items():
        collection.hide_render = name not in visible
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main():
    palette = json.loads(PALETTES.read_text())[ARGS.biome]
    scene = toon.reset(ARGS.samples)
    scene.render.resolution_x = TILE_PX * SUPERSAMPLE
    scene.render.resolution_y = HEIGHT_PX * SUPERSAMPLE
    rng = random.Random(ARGS.seed)
    names = [name for group in GROUPS.values() for name in group] + ["rift"]
    collections = {}
    for name in names:
        collections[name] = bpy.data.collections.new(name)
        scene.collection.children.link(collections[name])
    build_sky(collections, palette, rng)
    build_bank(collections, palette, rng)
    build_far(collections, palette, rng)
    build_treeline(collections, palette, rng)
    build_midground(collections, palette, rng)
    build_trees(collections, palette, rng)
    build_ground(collections, palette, rng)
    build_rift(collections["rift"], palette)
    toon.lights()
    camera = add_camera(scene, 0.0, VIEW_H / 2, TILE_W)
    out = OUT_DIR / ARGS.biome / "raw"
    out.mkdir(parents=True, exist_ok=True)
    if ARGS.blend:
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT_DIR / ARGS.biome / f"{ARGS.biome}.blend"))
    for layer, members in GROUPS.items():
        render(scene, collections, set(members), out / f"{layer}.png")
    scene.render.resolution_x = 40 * SUPERSAMPLE
    scene.render.resolution_y = 140 * SUPERSAMPLE
    camera.data.ortho_scale = 7.0
    camera.location = (0.1, -120.0, 3.5)
    render(scene, collections, {"rift"}, out / "rift.png")


ARGS = parse_args()
main()
