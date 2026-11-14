import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

from preview_statuses import BODY, DUMMY

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Assets" / "Game" / "Art" / "Resources"
SHEETS = RESOURCES / "BiomeLife"
RULES = ROOT / "Assets" / "StreamingAssets" / "Data" / "biome_life.json"
HERO = RESOURCES / "Heroes" / "Seeker" / "Seeker_Void_Idle_30x30_24f.png"
OUT = ROOT / "art" / "heroes" / "pixelfx" / "biomelife" / "preview"
DARK = (0x1B, 0x22, 0x36, 255)
LIGHT = (0xC4, 0xD6, 0xEA, 255)
FLOOR = {"dark": (0x2E, 0x59, 0x3B, 255), "light": (0x6F, 0xA8, 0x7C, 255)}
FILL = {"dark": DARK, "light": LIGHT}
FPS = 12
SECONDS = 6
LABEL = 12
HERO_FEET = (28, 52)
HERO_PIVOT = (15, 28)
ENEMY_FEET = (139, 52)
NAMED = {"hero": HERO_FEET, "heroHead": (29, 27), "enemy": ENEMY_FEET}
BOSS_SCALE = 2
PHASE_SKY = {"day": ["sun"], "morning": ["sunRise"], "evening": ["sunSet"], "night": ["stars", "moon"]}
BACKGROUNDS = {"hills": "green_hills", "autumn": "autumn_forest", "snow": "snow_foothills", "rose": "pink_forest", "cascades": "singing_cascades"}
EXTRA_PARTS = ("perch",)
REST_WALK = 2.4


class Art:
    def __init__(self, look):
        data = json.loads((SHEETS / "biome_life_art.json").read_text(encoding="utf-8"))
        rules = json.loads(RULES.read_text(encoding="utf-8"))
        self.look = data["looks"][look]
        self.rules = next(item for item in rules["looks"] if item["id"] == look)
        self.tints = data["tints"]
        self.scale = data["scale"]
        self.strip = tuple(data["strip"])
        self.view = (self.strip[0] * self.scale, self.strip[1] * self.scale)
        self.cache = {}

    def entry(self, path):
        node = self.look
        for part in path.split("."):
            node = node[part]
        return node

    def frames(self, path):
        if path not in self.cache:
            entry = self.entry(path)
            sheet = Image.open(SHEETS / (entry["file"] + ".png")).convert("RGBA")
            width, height = entry["width"], entry["height"]
            self.cache[path] = [sheet.crop((i * width, 0, i * width + width, height)) for i in range(entry["frames"])]
        return self.cache[path]

    def tint(self, phase):
        value = self.tints[phase].lstrip("#")
        return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))

    def weathers(self):
        return [item["id"] for item in self.rules["weathers"]]

    def creatures(self, phase):
        part = self.rules["night" if phase == "night" else "day"]
        return part["background"] + part["ground"]


def item(path, at=None, start=0.0, every=None, until=None, move=None, dim=1.0):
    return {"path": path, "at": at, "start": start, "every": every, "until": until, "move": move, "dim": dim}


class Scenes:
    def __init__(self, art):
        self.art = art

    def weather(self, name):
        return [item(f"weather.{name}.{part}") for part in ("far", "near", "mist")]

    def sky(self, phase):
        light = [] if phase == "day" else [item(f"light.{phase}")]
        return light + [item(f"sky.{name}") for name in PHASE_SKY[phase]]

    def scenery(self, phase, skip=()):
        parts = self.art.look["scenery"]
        group = "night" if phase == "night" else "day"
        names = [(group, name) for name in parts[group]] + [("always", name) for name in parts["always"]]
        return [item(f"scenery.{where}.{name}", every=None if parts[where][name]["loop"] else 4) for where, name in names if name not in skip]

    def creatures(self, phase, mood="calm", every=None, start=0.0):
        out = []
        for name in self.art.creatures(phase):
            group = self.art.look["creatures"][name]
            out += [item(f"creatures.{name}.{part}") for part in EXTRA_PARTS if part in group]
            out.append(item(f"creatures.{name}.{mood}", every=every, start=start))
        return out

    def quiet(self, phase, name):
        return self.sky(phase) + self.weather(name) + self.scenery(phase)

    def world(self, phase, name):
        return self.quiet(phase, name) + self.creatures(phase)

    def boss(self, phase):
        reactions = self.art.look["reactions"]
        extra = [item(f"reactions.{name}", every=6) for name in reactions if name.startswith("boss") and name not in ("bossOut", "bossSky", "bossGround", "bossDown")]
        return self.sky(phase) + self.weather(self.art.weathers()[0]) + self.scenery(phase, skip=("grass",)) + self.creatures(phase, "alarmed", every=6) + extra + [
            item("reactions.bossSky", dim=0.9), item("reactions.bossGround"), item("reactions.bossOut", every=6), item("reactions.enemyFlash", every=6)]

    def boss_down(self):
        return self.quiet("day", self.art.weathers()[0]) + [item("reactions.bossDown", every=5)] + self.creatures("day", start=1.2)

    def rest(self, phase):
        out = self.quiet(phase, self.art.weathers()[0])
        for name in self.art.creatures(phase):
            group = self.art.look["creatures"][name]
            out += [item(f"creatures.{name}.{part}") for part in EXTRA_PARTS if part in group]
            if "hop" not in group:
                out.append(item(f"creatures.{name}.friendly"))
                continue
            home = group["hop"]["at"]
            stop = (home[0] - 30, home[1])
            out.append(item(f"creatures.{name}.hop", until=REST_WALK, move=(-30 / REST_WALK, 0)))
            out.append(item(f"creatures.{name}.friendly", at=stop, start=REST_WALK))
        return out

    def streak(self, tier):
        return self.world("day", self.art.weathers()[0]) + [item(f"reactions.sway{tier}")]

    def guardian(self, phase):
        return self.quiet(phase, self.art.weathers()[0]) + [item("reactions.guardianCrack", every=3)] + self.creatures(phase, "hidden", every=3)

    def table(self):
        weathers = self.art.weathers()
        special = next(iter(self.art.look["special"]))
        special_phase = "night" if self.art.rules.get("special", {}).get("phase") == "night" else "day"
        scenes = {f"day-{name}": self.world("day", name) for name in weathers}
        scenes.update({f"night-{name}": self.world("night", name) for name in weathers})
        scenes[f"{special_phase}-{special}"] = self.world(special_phase, weathers[0]) + [item(f"special.{special}")]
        scenes.update({
            "morning": self.world("morning", weathers[0]),
            "evening": self.world("evening", weathers[0]),
            "boss-day": self.boss("day"),
            "boss-night": self.boss("night"),
            "boss-down": self.boss_down(),
            "rest-day": self.rest("day"),
            "rest-night": self.rest("night"),
            "streak-1": self.streak(1),
            "streak-2": self.streak(2),
            "streak-3": self.streak(3),
            "enemy-flash": self.world("day", weathers[0]) + [item("reactions.enemyFlash", every=2)],
            "guardian": self.guardian("day"),
            "guardian-night": self.guardian("night"),
            "prestige": self.world("day", weathers[0]) + [item("reactions.prestigeWave", every=3)],
        })
        reels = {
            "weather-day": [f"day-{name}" for name in weathers] + ([f"day-{special}"] if special_phase == "day" else []),
            "weather-night": [f"night-{name}" for name in weathers] + ([f"night-{special}"] if special_phase == "night" else []),
            "day-phases": ["morning", f"day-{weathers[0]}", "evening", f"night-{weathers[0]}"],
            "boss": ["boss-day", "boss-night", "boss-down"],
            "rest-and-streak": ["rest-day", "rest-night", "streak-1", "streak-2", "streak-3"],
            "world-events": ["enemy-flash", "guardian", "guardian-night", "prestige"],
        }
        return scenes, reels


BOSS = {"boss-day", "boss-night", "guardian", "guardian-night"}


def phase_of(name):
    if name in ("morning", "evening"):
        return name
    return "night" if "night" in name else "day"


def dummy():
    image = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    for y, row in enumerate(DUMMY):
        for x, ch in enumerate(row):
            if BODY[ch]:
                image.putpixel((x, y), BODY[ch] + (255,))
    return image


def grown(image, scale):
    return image.resize((image.width * scale, image.height * scale), Image.NEAREST)


def tinted(image, colour):
    solid = Image.new("RGB", image.size, colour)
    out = ImageChops.multiply(image.convert("RGB"), solid).convert("RGBA")
    out.putalpha(image.getchannel("A"))
    return out


def faded(image, amount):
    if amount >= 1:
        return image
    out = image.copy()
    out.putalpha(image.getchannel("A").point(lambda value: int(value * amount)))
    return out


def paste_clipped(canvas, frame, x, y):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    layer.paste(frame, (x, y))
    canvas.alpha_composite(layer)


class Stage:
    def __init__(self, art, scenes, backdrop, backgrounds):
        self.art = art
        self.scenes = scenes
        self.backdrop = backdrop
        self.hero = grown(Image.open(HERO).convert("RGBA").crop((0, 0, 30, 30)), art.scale)
        self.enemy = dummy()
        self.layers = None
        if backgrounds:
            names = [name.split(":")[1] for name in art.look["order"] if name.startswith("bg:")]
            self.layers = {name: Image.open(backgrounds / f"{name}.png").convert("RGBA").crop((0, 0, art.view[0], art.view[1])) for name in names}

    def clip(self, entry, sheet, time):
        frames = self.art.frames(entry["path"])
        local = time - entry["start"]
        if entry["every"]:
            local = local % entry["every"]
        if local < 0 or (entry["until"] is not None and time >= entry["until"]):
            return None, local
        if sheet["frames"] == 1:
            return frames[0], local
        index = int(local * sheet["fps"] + 1e-6)
        if sheet["loop"]:
            return frames[index % sheet["frames"]], local
        return (frames[index] if index < sheet["frames"] else None), local

    def paint(self, canvas, entry, time, tone):
        sheet = self.art.entry(entry["path"])
        frame, local = self.clip(entry, sheet, time)
        if frame is None:
            return
        if not sheet.get("glow"):
            frame = tinted(frame, tone)
        scale = self.art.scale
        at = entry["at"] or sheet.get("at") or (0, 0)
        x, y = NAMED[at] if isinstance(at, str) else at
        if entry["move"]:
            x, y = x + entry["move"][0] * local, y + entry["move"][1] * local
        left, top = round(x) - sheet["pivot"][0], round(y) - sheet["pivot"][1]
        frame = faded(grown(frame, scale), entry["dim"])
        scroll = sheet.get("scroll")
        if not scroll:
            paste_clipped(canvas, frame, left * scale, top * scale)
            return
        shift = round(time * scroll * scale) % frame.width
        for start in (shift - frame.width, shift):
            paste_clipped(canvas, frame, start, top * scale)

    def actors(self, canvas, boss):
        scale = self.art.scale
        canvas.alpha_composite(self.hero, ((HERO_FEET[0] - HERO_PIVOT[0]) * scale, (HERO_FEET[1] - HERO_PIVOT[1]) * scale))
        enemy = grown(self.enemy, scale * (BOSS_SCALE if boss else 1))
        paste_clipped(canvas, enemy, ENEMY_FEET[0] * scale - enemy.width // 2, ENEMY_FEET[1] * scale - enemy.height)

    def frame(self, name, time):
        art, scale = self.art, self.art.scale
        entries = self.scenes[name]
        canvas = Image.new("RGBA", art.view, FILL.get(self.backdrop, DARK))
        tone = art.tint(phase_of(name))
        if not self.layers:
            canvas.paste(FLOOR[self.backdrop], (0, HERO_FEET[1] * scale, art.view[0], HERO_FEET[1] * scale + scale))
        for layer in art.look["order"]:
            if layer.startswith("bg:"):
                if self.layers:
                    canvas.alpha_composite(tinted(self.layers[layer.split(":")[1]], tone))
            elif layer == "actors":
                self.actors(canvas, name in BOSS)
            else:
                for entry in entries:
                    if art.entry(entry["path"])["layer"] == layer:
                        self.paint(canvas, entry, time, tone)
        return canvas


def reel(stage, name, scenes, out):
    art = stage.art
    wide, high = art.view[0], art.view[1] + LABEL
    frames = []
    for tick in range(SECONDS * FPS):
        sheet = Image.new("RGBA", (wide, high * len(scenes)), DARK)
        draw = ImageDraw.Draw(sheet)
        for index, scene in enumerate(scenes):
            sheet.paste(stage.frame(scene, tick / FPS), (0, index * high + LABEL))
            draw.text((3, index * high + 1), scene, fill=(170, 180, 205))
        frames.append(sheet.convert("RGB"))
    out.mkdir(parents=True, exist_ok=True)
    frames[0].save(out / f"{name}.gif", save_all=True, append_images=frames[1:], duration=1000 // FPS, loop=0)


def still(stage, scenes, times, out, name):
    art = stage.art
    wide, high = art.view[0], art.view[1] + LABEL
    sheet = Image.new("RGBA", (wide * len(times), high * len(scenes)), DARK)
    draw = ImageDraw.Draw(sheet)
    for row, scene in enumerate(scenes):
        for column, time in enumerate(times):
            sheet.paste(stage.frame(scene, time), (column * wide, row * high + LABEL))
            draw.text((column * wide + 3, row * high + 1), f"{scene}  {time:.1f}s", fill=(170, 180, 205))
    out.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(out / f"{name}.png")


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--look", default="hills")
    parser.add_argument("--backgrounds", type=Path)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--reel", action="append")
    parser.add_argument("--stills", nargs="*")
    parser.add_argument("--times", nargs="*", type=float, default=[0.5])
    return parser.parse_args()


def main():
    args = parse_args()
    art = Art(args.look)
    scenes, reels = Scenes(art).table()
    if args.backgrounds:
        if not args.out:
            print("--backgrounds needs --out outside the repository")
            return 1
        stages = [("biome", Stage(art, scenes, "biome", args.backgrounds / BACKGROUNDS[args.look]), args.out)]
    else:
        stages = [(backdrop, Stage(art, scenes, backdrop, None), args.out or OUT / args.look) for backdrop in ("dark", "light")]
    for backdrop, stage, out in stages:
        if args.stills is not None:
            still(stage, args.stills or list(scenes), args.times, out, f"{args.look}-stills-{backdrop}")
            continue
        for name in args.reel or list(reels):
            reel(stage, f"{args.look}-{name}-{backdrop}", reels[name], out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
