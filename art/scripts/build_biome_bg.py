import argparse
import json
import math
import random
import sys
from pathlib import Path

import bpy
import bmesh

ROOT = Path(__file__).resolve().parents[2]
PALETTES = ROOT / "art" / "palettes" / "biomes.json"
OUT_DIR = ROOT / "art" / "out" / "backgrounds"

TILE_PX = 960
HEIGHT_PX = 180
PX_PER_UNIT = 20
SUPERSAMPLE = 4
TILE_W = TILE_PX / PX_PER_UNIT
VIEW_H = HEIGHT_PX / PX_PER_UNIT
HALF = TILE_W / 2
LAYERS = ("back", "mid", "ground")


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument("--biome", default="green_hills")
    parser.add_argument("--seed", type=int, default=7)
    parser.add_argument("--samples", type=int, default=16)
    parser.add_argument("--blend", action="store_true")
    return parser.parse_args(argv)


def rgb(value):
    value = value.lstrip("#")
    srgb = [int(value[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb) + (1.0,)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = ARGS.samples
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.resolution_x = TILE_PX * SUPERSAMPLE
    scene.render.resolution_y = HEIGHT_PX * SUPERSAMPLE
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.use_freestyle = True
    scene.render.line_thickness_mode = "ABSOLUTE"
    scene.render.line_thickness = 1.0
    world = bpy.data.worlds.new("World")
    world.color = (0, 0, 0)
    scene.world = world
    return scene


def make_collection(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def flat_material(name, color):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = rgb(color)
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(emission.outputs[0], output.inputs[0])
    return material


def toon_material(name, color, shadow, highlight=None):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    nodes = tree.nodes
    nodes.clear()
    diffuse = nodes.new("ShaderNodeBsdfDiffuse")
    to_rgb = nodes.new("ShaderNodeShaderToRGB")
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = rgb(shadow)
    ramp.color_ramp.elements[1].position = 0.32
    ramp.color_ramp.elements[1].color = rgb(color)
    if highlight:
        top = ramp.color_ramp.elements.new(0.85)
        top.color = rgb(highlight)
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    tree.links.new(diffuse.outputs[0], to_rgb.inputs[0])
    tree.links.new(to_rgb.outputs[0], ramp.inputs[0])
    tree.links.new(ramp.outputs[0], emission.inputs[0])
    tree.links.new(emission.outputs[0], output.inputs[0])
    return material


def gradient_material(name, bottom, top, z0, z1):
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
    ramp.color_ramp.elements[0].color = rgb(bottom)
    ramp.color_ramp.elements[1].color = rgb(top)
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    tree.links.new(coords.outputs["Object"], separate.inputs[0])
    tree.links.new(separate.outputs["Z"], span.inputs["Value"])
    tree.links.new(span.outputs[0], ramp.inputs[0])
    tree.links.new(ramp.outputs[0], emission.inputs[0])
    tree.links.new(emission.outputs[0], output.inputs[0])
    return material


def outline_material(color):
    material = bpy.data.materials.get("outline") or flat_material("outline", color)
    material.use_backface_culling = True
    return material


def link(obj, collection):
    collection.objects.link(obj)
    return obj


def mesh_object(name, mesh, collection, material):
    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(material)
    return link(obj, collection)


def periodic(waves, x):
    return sum(a * math.sin(2 * math.pi * k * x / TILE_W + p) for a, k, p in waves)


def profile_polygon(name, collection, material, top, depth, bottom=-1.0, step=0.2):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    xs = [-HALF - 1.0 + i * step for i in range(int((TILE_W + 2.0) / step) + 1)]
    upper = [bm.verts.new((x, depth, top(x))) for x in xs]
    lower = [bm.verts.new((x, depth, bottom)) for x in reversed(xs)]
    bm.faces.new(upper + lower)
    bm.to_mesh(mesh)
    bm.free()
    return mesh_object(name, mesh, collection, material)


def add_outline(obj, thickness, palette):
    modifier = obj.modifiers.new("outline", "SOLIDIFY")
    modifier.thickness = thickness
    modifier.offset = 1.0
    modifier.use_flip_normals = True
    modifier.material_offset = len(obj.data.materials)
    obj.data.materials.append(outline_material(palette["outline"]))


def sphere(name, collection, material, location, scale, palette, outline=0.06, subdivisions=3):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    for face in bm.faces:
        face.smooth = True
    bm.to_mesh(mesh)
    bm.free()
    obj = mesh_object(name, mesh, collection, material)
    obj.location = location
    obj.scale = scale
    if outline:
        add_outline(obj, outline / max(scale), palette)
    return obj


def cone(name, collection, material, location, radius, height, palette, outline=0.05, segments=12):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=0.0, depth=height)
    bm.to_mesh(mesh)
    bm.free()
    obj = mesh_object(name, mesh, collection, material)
    obj.location = (location[0], location[1], location[2] + height / 2)
    if outline:
        add_outline(obj, outline, palette)
    return obj


def box(name, collection, material, location, size, palette, outline=0.05):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(mesh)
    bm.free()
    obj = mesh_object(name, mesh, collection, material)
    obj.location = (location[0], location[1], location[2] + size[2] / 2)
    obj.scale = size
    if outline:
        add_outline(obj, outline / min(size), palette)
    return obj


def cylinder(name, collection, material, location, radius, height, palette, outline=0.05):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=radius, radius2=radius, depth=height)
    bm.to_mesh(mesh)
    bm.free()
    obj = mesh_object(name, mesh, collection, material)
    obj.location = (location[0], location[1], location[2] + height / 2)
    if outline:
        add_outline(obj, outline, palette)
    return obj


def wrapped(x, extent):
    shifts = [0.0]
    if x - extent < -HALF:
        shifts.append(TILE_W)
    if x + extent > HALF:
        shifts.append(-TILE_W)
    return [x + s for s in shifts]


def build_sky(collection, palette, rng):
    sky = gradient_material("sky", palette["sky_bottom"], palette["sky_top"], 2.0, VIEW_H)
    profile_polygon("sky", collection, sky, lambda x: VIEW_H + 1.0, 60.0)
    white = toon_material("cloud", palette["cloud"], palette["cloud_shadow"])
    for index, (cx, cz, size) in enumerate([(-17.0, 7.4, 1.0), (-4.0, 8.0, 0.8), (9.5, 7.2, 1.15), (19.0, 8.1, 0.7)]):
        for x in wrapped(cx, 3.0 * size):
            puffs = [(-1.6, 0.0, 0.9), (-0.6, 0.35, 1.15), (0.6, 0.25, 1.05), (1.6, -0.05, 0.8), (0.0, -0.2, 1.0)]
            for p, (dx, dz, r) in enumerate(puffs):
                sphere(
                    f"cloud_{index}_{p}_{x:.0f}", collection, white,
                    (x + dx * size, 55.0, cz + dz * size), (r * size, 0.6 * r * size, 0.62 * r * size),
                    palette, outline=0.0, subdivisions=3,
                )


def build_far(collection, palette, rng):
    waves = [(0.55, 1, 0.4), (0.35, 2, 2.1), (0.22, 3, 4.0), (0.12, 5, 1.3)]
    top = lambda x: 4.2 + periodic(waves, x)
    rim = flat_material("far_rim", palette["far_hill_rim"])
    body = flat_material("far_hill", palette["far_hill"])
    profile_polygon("far_rim", collection, rim, top, 40.0)
    profile_polygon("far_hill", collection, body, lambda x: top(x) - 0.22, 39.5)
    stone = flat_material("castle", palette["castle"])
    roof = flat_material("castle_roof", palette["castle_roof"])
    cx = -13.5
    base = top(cx) - 0.3
    for dx, w, h in [(-0.9, 0.5, 1.5), (0.0, 0.8, 1.1), (0.9, 0.5, 1.3), (0.35, 0.35, 2.0)]:
        box(f"tower_{dx}", collection, stone, (cx + dx, 38.0, base), (w, 0.5, h), palette, outline=0.0)
        cone(f"roof_{dx}", collection, roof, (cx + dx, 37.8, base + h), w * 0.62, 0.55, palette, outline=0.0, segments=4)


def build_mid(collection, palette, rng):
    waves = [(0.45, 1, 1.1), (0.4, 2, 5.2), (0.2, 4, 0.6), (0.1, 7, 2.9)]
    top = lambda x: 2.75 + periodic(waves, x)
    profile_polygon("mid_rim", collection, flat_material("mid_rim", palette["mid_hill_rim"]), top, 20.0)
    profile_polygon("mid_hill", collection, flat_material("mid_hill", palette["mid_hill"]), lambda x: top(x) - 0.25, 19.5)
    leaves = toon_material("tree", palette["tree"], palette["tree_dark"])
    trunk = toon_material("trunk", palette["trunk"], "#5E4130")
    spots = [-21.0, -18.6, -10.5, -8.4, -1.5, 3.0, 4.6, 14.0, 21.5]
    for index, tx in enumerate(spots):
        scale = rng.uniform(0.75, 1.05)
        for x in wrapped(tx, 1.2):
            ground = top(x) - 0.2
            cylinder(f"trunk_{index}_{x:.0f}", collection, trunk, (x, 18.0, ground), 0.12 * scale, 0.9 * scale, palette, outline=0.04)
            crown = ground + 1.05 * scale
            sphere(f"crown_{index}_{x:.0f}", collection, leaves, (x, 17.5, crown), (0.62 * scale,) * 3, palette, outline=0.05)
            sphere(f"crown_l_{index}_{x:.0f}", collection, leaves, (x - 0.38 * scale, 17.6, crown - 0.22 * scale), (0.42 * scale,) * 3, palette, outline=0.05)
            sphere(f"crown_r_{index}_{x:.0f}", collection, leaves, (x + 0.4 * scale, 17.6, crown - 0.18 * scale), (0.45 * scale,) * 3, palette, outline=0.05)


def build_ground(collection, palette, rng):
    waves = [(0.08, 3, 0.2), (0.05, 7, 1.7), (0.03, 13, 4.4)]
    top = lambda x: 1.45 + periodic(waves, x)
    profile_polygon("ground_rim", collection, flat_material("ground_rim", palette["ground_rim"]), top, 2.0)
    profile_polygon("ground", collection, flat_material("ground", palette["ground"]), lambda x: top(x) - 0.18, 1.9)
    path_waves = [(0.06, 2, 0.9), (0.04, 5, 3.1)]
    path = flat_material("path", palette["path"])
    profile_polygon("path", collection, path, lambda x: 0.72 + periodic(path_waves, x), 1.5, bottom=0.32)
    grass = toon_material("grass", palette["ground"], palette["tree"], palette["ground_rim"])
    for index in range(46):
        gx = -HALF + (index + rng.uniform(0.1, 0.9)) * TILE_W / 46
        for x in wrapped(gx, 0.3):
            for blade in range(3):
                cone(
                    f"grass_{index}_{blade}_{x:.0f}", collection, grass,
                    (x + (blade - 1) * 0.11, 1.0, top(x) - 0.12), 0.07, rng.uniform(0.28, 0.45), palette, outline=0.0, segments=5,
                )
    petals = [flat_material(f"flower_{k}", palette[k]) for k in ("flower_pink", "flower_yellow", "flower_white")]
    for index in range(26):
        fx = -HALF + (index + rng.uniform(0.1, 0.9)) * TILE_W / 26
        fz = rng.choice([rng.uniform(0.1, 0.28), rng.uniform(0.95, 1.2)])
        for x in wrapped(fx, 0.2):
            sphere(f"flower_{index}_{x:.0f}", collection, rng.choice(petals), (x, 0.5, fz), (0.09, 0.09, 0.09), palette, outline=0.025, subdivisions=2)


def build_rift(collection, palette):
    glow = flat_material("rift", palette["rift"])
    core = flat_material("rift_core", palette["rift_core"])
    zig = [(0.0, 0.0), (0.35, 0.9), (-0.15, 1.8), (0.4, 2.8), (-0.05, 3.7), (0.2, 4.6)]
    for name, material, width, depth in (("rift_glow", glow, 0.42, 0.6), ("rift_core", core, 0.14, 0.5)):
        mesh = bpy.data.meshes.new(name)
        bm = bmesh.new()
        left = [bm.verts.new((x - width * (1 - z / 5.2), depth, z + 1.2)) for x, z in zig]
        right = [bm.verts.new((x + width * (1 - z / 5.2), depth, z + 1.2)) for x, z in reversed(zig)]
        bm.faces.new(left + right)
        bm.to_mesh(mesh)
        bm.free()
        mesh_object(name, mesh, collection, material)


def add_camera(scene, width_units, height_units, center_x=0.0):
    data = bpy.data.cameras.new("camera")
    data.type = "ORTHO"
    data.ortho_scale = max(width_units, height_units)
    data.clip_end = 200.0
    camera = bpy.data.objects.new("camera", data)
    camera.location = (center_x, -80.0, height_units / 2)
    camera.rotation_euler = (math.radians(90), 0.0, 0.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def add_sun(scene):
    data = bpy.data.lights.new("sun", "SUN")
    data.energy = 3.0
    sun = bpy.data.objects.new("sun", data)
    sun.rotation_euler = (math.radians(50), math.radians(-35), math.radians(-30))
    scene.collection.objects.link(sun)


def setup_freestyle(scene, palette):
    layer = scene.view_layers[0]
    layer.use_freestyle = True
    settings = layer.freestyle_settings
    lineset = settings.linesets[0] if settings.linesets else settings.linesets.new("lines")
    lineset.select_by_visibility = True
    lineset.select_by_edge_types = True
    lineset.select_silhouette = True
    lineset.select_border = True
    lineset.select_crease = False
    style = lineset.linestyle or bpy.data.linestyles.new("outline")
    lineset.linestyle = style
    style.color = rgb(palette["outline"])[:3]
    style.thickness = 1.5 * SUPERSAMPLE
    style.alpha = 0.55


def render_layer(scene, collections, visible, path, freestyle):
    for name, collection in collections.items():
        collection.hide_render = name not in visible
    scene.render.use_freestyle = freestyle
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main():
    palette = json.loads(PALETTES.read_text())[ARGS.biome]
    scene = reset_scene()
    rng = random.Random(ARGS.seed)
    collections = {name: make_collection(name) for name in ("sky", "far", "mid", "ground", "rift")}
    build_sky(collections["sky"], palette, rng)
    build_far(collections["far"], palette, rng)
    build_mid(collections["mid"], palette, rng)
    build_ground(collections["ground"], palette, rng)
    build_rift(collections["rift"], palette)
    add_sun(scene)
    setup_freestyle(scene, palette)
    out = OUT_DIR / ARGS.biome / "raw"
    out.mkdir(parents=True, exist_ok=True)
    add_camera(scene, TILE_W, VIEW_H)
    if ARGS.blend:
        bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "art" / "out" / "backgrounds" / ARGS.biome / f"{ARGS.biome}.blend"))
    render_layer(scene, collections, {"sky", "far"}, out / "back.png", False)
    render_layer(scene, collections, {"mid"}, out / "mid.png", True)
    render_layer(scene, collections, {"ground"}, out / "ground.png", True)
    scene.render.resolution_x = 40 * SUPERSAMPLE
    scene.render.resolution_y = 140 * SUPERSAMPLE
    scene.camera.data.ortho_scale = 7.0
    scene.camera.location = (0.1, -80.0, 3.5)
    render_layer(scene, collections, {"rift"}, out / "rift.png", False)


ARGS = parse_args()
main()
