import toon


def build(colors):
    root = toon.pivot("root")
    toon.block("cube", toon.toon_material("main", colors["main"]), (0.0, 0.0, 0.8), (1.6, 1.6, 1.6), root, rotation=(0, 0, 30))
    toon.ball("sphere", toon.toon_material("accent", colors["accent"]), (-0.6, -1.0, 2.35), (0.62, 0.62, 0.62), root)
    return {"root": root}


def pose(rig, anim, frame, count):
    rig["root"].location = (0, 0, 0)
