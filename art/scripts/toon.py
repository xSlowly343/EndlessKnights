import math

import bpy
import bmesh
from mathutils import Matrix

SUPERSAMPLE = 4
PX_PER_UNIT = 32
OUTLINE_PX = 2.0
CAMERA_TURN_DEG = 22.0
OUTLINE_COLOR = "#1A2024"
GROUND_PX = 8


def rgb(value):
    value = value.lstrip("#")
    srgb = [int(value[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb) + (1.0,)


def shade(value, factor):
    value = value.lstrip("#")
    channels = [min(255, max(0, round(int(value[i:i + 2], 16) * factor))) for i in (0, 2, 4)]
    return "#" + "".join(f"{c:02X}" for c in channels)


def outline_units(px=OUTLINE_PX):
    return px / PX_PER_UNIT


def reset(samples=16):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = samples
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.film_transparent = True
    scene.render.filter_size = 1.0
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    world = bpy.data.worlds.new("World")
    world.color = (0, 0, 0)
    scene.world = world
    return scene


def toon_material(name, color, shadow=None, highlight=None, glow=False):
    material = bpy.data.materials.get(name)
    if material:
        return material
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    nodes = tree.nodes
    nodes.clear()
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    tree.links.new(emission.outputs[0], output.inputs[0])
    if glow:
        emission.inputs["Color"].default_value = rgb(color)
        return material
    diffuse = nodes.new("ShaderNodeBsdfDiffuse")
    to_rgb = nodes.new("ShaderNodeShaderToRGB")
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].color = rgb(shadow or shade(color, 0.68))
    ramp.color_ramp.elements[1].position = 0.2
    ramp.color_ramp.elements[1].color = rgb(color)
    top = ramp.color_ramp.elements.new(0.8)
    top.color = rgb(highlight or shade(color, 1.18))
    tree.links.new(diffuse.outputs[0], to_rgb.inputs[0])
    tree.links.new(to_rgb.outputs[0], ramp.inputs[0])
    tree.links.new(ramp.outputs[0], emission.inputs[0])
    return material


def outline_material():
    material = bpy.data.materials.get("outline")
    if material is None:
        material = toon_material("outline", OUTLINE_COLOR, glow=True)
        material.use_backface_culling = True
    return material


def add_outline(obj, px=OUTLINE_PX):
    scale = max(obj.scale) if max(obj.scale) > 0 else 1.0
    modifier = obj.modifiers.new("outline", "SOLIDIFY")
    modifier.thickness = outline_units(px) / scale
    modifier.offset = 1.0
    modifier.use_flip_normals = True
    modifier.use_rim = False
    modifier.material_offset = len(obj.data.materials)
    obj.data.materials.append(outline_material())
    return obj


def _object(name, mesh, material, parent, location, scale, rotation, outline):
    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(material)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.scale = scale
    obj.rotation_euler = [math.radians(a) for a in rotation]
    if outline:
        add_outline(obj, outline)
    return obj


def _smooth(bm):
    for face in bm.faces:
        face.smooth = True


def ball(name, material, location, scale, parent=None, rotation=(0, 0, 0), outline=OUTLINE_PX, subdivisions=4):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    _smooth(bm)
    bm.to_mesh(mesh)
    bm.free()
    return _object(name, mesh, material, parent, location, scale, rotation, outline)


def cone(name, material, location, radius1, radius2, depth, parent=None, rotation=(0, 0, 0), outline=OUTLINE_PX, segments=24):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius1, radius2=radius2, depth=depth)
    _smooth(bm)
    bm.to_mesh(mesh)
    bm.free()
    return _object(name, mesh, material, parent, location, (1, 1, 1), rotation, outline)


def block(name, material, location, size, parent=None, rotation=(0, 0, 0), outline=OUTLINE_PX):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(mesh)
    bm.free()
    obj = _object(name, mesh, material, parent, location, (1, 1, 1), rotation, 0)
    obj.data.transform(Matrix.Diagonal((*size, 1.0)))
    if outline:
        add_outline(obj, outline)
    return obj


def pivot(name, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    return obj


def lights():
    key = bpy.data.lights.new("key", "SUN")
    key.energy = 3.2
    key.use_shadow = False
    key_obj = bpy.data.objects.new("key", key)
    key_obj.rotation_euler = (math.radians(40), math.radians(-30), math.radians(-35))
    bpy.context.scene.collection.objects.link(key_obj)
    rim = bpy.data.lights.new("rim", "SUN")
    rim.energy = 1.2
    rim.use_shadow = False
    rim_obj = bpy.data.objects.new("rim", rim)
    rim_obj.rotation_euler = (math.radians(-60), math.radians(20), math.radians(150))
    bpy.context.scene.collection.objects.link(rim_obj)


def camera(cell, facing):
    scene = bpy.context.scene
    width, height = cell
    scene.render.resolution_x = width * SUPERSAMPLE
    scene.render.resolution_y = height * SUPERSAMPLE
    data = bpy.data.cameras.new("camera")
    data.type = "ORTHO"
    data.ortho_scale = max(width, height) / PX_PER_UNIT
    data.clip_start = 0.1
    data.clip_end = 100.0
    rig = pivot("camera_rig", (0, 0, height / PX_PER_UNIT / 2 - GROUND_PX / PX_PER_UNIT))
    cam = bpy.data.objects.new("camera", data)
    scene.collection.objects.link(cam)
    cam.parent = rig
    cam.location = (0, -30, 0)
    cam.rotation_euler = (math.radians(90), 0, 0)
    turn = -CAMERA_TURN_DEG if facing == "left" else CAMERA_TURN_DEG
    rig.rotation_euler = (0, 0, math.radians(turn))
    scene.camera = cam
    return cam
