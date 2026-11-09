import math

import toon

HIP_HEIGHT = 0.8
BODY_SCALE = 0.96


def build(colors):
    skin = toon.toon_material("skin", colors["skin"])
    belly = toon.toon_material("belly", toon.shade(colors["skin"], 1.1))
    cloth = toon.toon_material("cloth", colors["cloth"])
    accent = toon.toon_material("accent", colors["accent"], highlight="#FFD0C8")
    eyes = toon.toon_material("eyes", colors["accent"], glow=True)
    wood = toon.toon_material("wood", colors["wood"])
    bone = toon.toon_material("bone", "#F2EAD3")
    dark = toon.toon_material("dark", "#2A2E2A", glow=True)

    root = toon.pivot("root")
    for side in (-1, 1):
        toon.ball(f"leg_{side}", skin, (0.08, 0.38 * side, 0.42), (0.34, 0.32, 0.46), root)
        toon.ball(f"foot_{side}", skin, (-0.12, 0.4 * side, 0.16), (0.4, 0.26, 0.16), root)

    hips = toon.pivot("hips", (0, 0, HIP_HEIGHT), root)
    toon.cone("loincloth", cloth, (0.0, 0.0, 0.02), 0.98, 0.86, 0.42, hips)

    torso = toon.pivot("torso", (0, 0, 0.1), hips)
    toon.ball("body", skin, (0.05, 0.0, 0.55), (1.0, 0.92, 0.95), torso)
    toon.ball("belly", belly, (-0.36, 0.0, 0.4), (0.66, 0.7, 0.68), torso, outline=0)

    head = toon.pivot("head", (-0.2, 0.0, 1.45), torso)
    toon.ball("skull", skin, (0.0, 0.0, 0.25), (0.6, 0.58, 0.52), head)
    for side in (-1, 1):
        toon.cone(f"ear_{side}", skin, (0.2, 0.62 * side, 0.42), 0.2, 0.0, 0.78, head, rotation=(-58 * side, 38, 0))
        toon.ball(f"eye_{side}", eyes, (-0.5, 0.21 * side, 0.36), (0.08, 0.08, 0.07), head, outline=1.2)
        toon.block(f"brow_{side}", dark, (-0.52, 0.22 * side, 0.47), (0.08, 0.2, 0.06), head, rotation=(18 * side, 0, 0), outline=0)
        toon.cone(f"tusk_{side}", bone, (-0.56, 0.17 * side, 0.02), 0.06, 0.0, 0.22, head, rotation=(0, -10, 0), outline=1.5)
    toon.ball("nose", skin, (-0.72, 0.0, 0.22), (0.26, 0.2, 0.2), head)
    toon.ball("wart", accent, (-0.9, -0.1, 0.3), (0.11, 0.11, 0.11), head, outline=1.5)

    club_arm = toon.pivot("club_arm", (-0.05, -0.95, 1.05), torso)
    toon.ball("upper_arm_club", skin, (0.0, 0.0, -0.38), (0.27, 0.27, 0.5), club_arm)
    toon.ball("fist_club", skin, (-0.12, 0.0, -0.82), (0.27, 0.27, 0.27), club_arm)
    club = toon.pivot("club", (-0.12, -0.05, -0.82), club_arm)
    club.rotation_euler = (0, math.radians(28), 0)
    toon.cone("club_head", wood, (0.0, 0.0, 0.8), 0.12, 0.36, 1.55, club)
    for index, (dz, ang) in enumerate([(1.15, 0), (1.4, 120), (1.0, 240)]):
        rad = math.radians(ang)
        toon.cone(
            f"stud_{index}", bone, (0.3 * math.cos(rad), 0.3 * math.sin(rad), dz), 0.06, 0.0, 0.18, club,
            rotation=(-90 * math.sin(rad), 90 * math.cos(rad), 0), outline=1.2,
        )

    free_arm = toon.pivot("free_arm", (0.0, 0.92, 1.05), torso)
    toon.ball("upper_arm_free", skin, (0.0, 0.0, -0.4), (0.26, 0.26, 0.5), free_arm)
    toon.ball("fist_free", skin, (-0.06, 0.0, -0.86), (0.25, 0.25, 0.25), free_arm)

    return {"root": root, "hips": hips, "torso": torso, "head": head, "club_arm": club_arm, "free_arm": free_arm}


def _reset(rig):
    rig["root"].location = (0, 0, 0)
    rig["root"].rotation_euler = (0, 0, 0)
    rig["root"].scale = (BODY_SCALE, BODY_SCALE, BODY_SCALE)
    rig["hips"].rotation_euler = (0, 0, 0)
    rig["torso"].location = (0, 0, 0.1)
    rig["torso"].scale = (1, 1, 1)
    rig["head"].rotation_euler = (0, 0, 0)
    rig["club_arm"].rotation_euler = (0, 0, 0)
    rig["free_arm"].rotation_euler = (0, 0, 0)


def _idle(rig, t):
    breath = math.sin(2 * math.pi * t)
    rig["torso"].location = (0, 0, 0.1 + 0.035 * breath)
    rig["torso"].scale = (1 + 0.015 * breath, 1 + 0.015 * breath, 1 - 0.01 * breath)
    rig["head"].rotation_euler = (0, math.radians(3 * math.sin(2 * math.pi * t - 0.6)), 0)
    rig["club_arm"].rotation_euler = (0, math.radians(-4 * breath), 0)
    rig["free_arm"].rotation_euler = (math.radians(-3 * breath), math.radians(5 * breath), 0)


def _hit(rig, frame):
    push = (1.0, 0.55, 0.2)[frame]
    rig["root"].location = (0.12 * push, 0, 0)
    rig["hips"].rotation_euler = (0, math.radians(11 * push), 0)
    rig["head"].rotation_euler = (math.radians(6 * push), math.radians(14 * push), 0)
    rig["club_arm"].rotation_euler = (0, math.radians(-18 * push), 0)
    rig["free_arm"].rotation_euler = (math.radians(-25 * push), 0, 0)


def _death(rig, frame, count):
    t = frame / (count - 1)
    ease = 1 - (1 - t) ** 2
    rig["root"].location = (-0.1 * ease, 0, 0)
    rig["hips"].rotation_euler = (0, math.radians(28 * ease), 0)
    rig["head"].rotation_euler = (math.radians(-10 * ease), math.radians(22 * ease), 0)
    rig["club_arm"].rotation_euler = (math.radians(30 * ease), math.radians(25 * ease), 0)
    rig["free_arm"].rotation_euler = (math.radians(-55 * ease), math.radians(-30 * ease), 0)
    rig["root"].scale = (BODY_SCALE, BODY_SCALE, BODY_SCALE * (1 - 0.18 * ease))


def pose(rig, anim, frame, count):
    _reset(rig)
    if anim == "idle":
        _idle(rig, frame / count)
    elif anim == "hit":
        _hit(rig, frame)
    elif anim == "death":
        _death(rig, frame, count)
