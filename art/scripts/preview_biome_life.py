import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

from preview_statuses import BODY, DUMMY

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Assets" / "Game" / "Art" / "Resources"
SHEETS = RESOURCES / "BiomeLife"
HERO = RESOURCES / "Heroes" / "Seeker" / "Seeker_Void_Idle_30x30_24f.png"
OUT = ROOT / "art" / "heroes" / "pixelfx" / "biomelife" / "preview"
DARK = (0x1B, 0x22, 0x36, 255)
LIGHT = (0xC4, 0xD6, 0xEA, 255)
FLOOR = {"dark": (0x2E, 0x59, 0x3B, 255), "light": (0x6F, 0xA8, 0x7C, 255)}
FILL = {"dark": DARK, "light": LIGHT}
FPS = 12
LABEL = 12
HERO_FEET = (28, 52)
HERO_PIVOT = (15, 28)
HERO_HEAD = (29, 27)
ENEMY_FEET = (139, 52)
BOSS_SCALE = 2
ORDER = ["back", "light", "sky", "mid", "far", "turf", "ground", "actors", "front"]
SPOTS = {
    "sun": (140, 8), "sunLow": (22, 20), "sunSet": (136, 21), "moon": (114, 9),
    "butterflies": (66, 40), "starlings": (72, 11), "rabbit": (92, 52), "fireflies": (70, 38), "owl": (100, 52),
    "mill": (99, 31), "castle": (43, 27),
}
PHASE_SKY = {"day": [("sky.sun", "sun")], "morning": [("sky.sunLow", "sunLow")], "evening": [("sky.sunLow", "sunSet")], "night": [("sky.stars", (0, 0)), ("sky.moon", "moon")]}


class Art:
    def __init__(self):
        self.data = json.loads((SHEETS / "biome_life_art.json").read_text(encoding="utf-8"))
        self.scale = self.data["scale"]
        self.strip = tuple(self.data["strip"])
        self.view = (self.strip[0] * self.scale, self.strip[1] * self.scale)
        self.cache = {}

    def entry(self, path):
        node = self.data["looks"]["hills"]
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
        value = self.data["tints"][phase].lstrip("#")
        return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def spot(value):
    return SPOTS[value] if isinstance(value, str) else value


def item(path, at=(0, 0), start=0.0, every=None, until=None, move=None, dim=1.0):
    return {"path": path, "at": at, "start": start, "every": every, "until": until, "move": move, "dim": dim}


def weather(name):
    return [item(f"weather.{name}.{part}") for part in ("far", "near", "mist")]


def scenery(phase):
    if phase == "night":
        return [item("scenery.night.grass", (0, 52)), item("scenery.night.flowers", (0, 52)), item("scenery.always.castle", "castle"), item("scenery.always.mill", "mill"), item("scenery.night.castleLights", "castle"), item("scenery.night.mist")]
    return [item("scenery.day.grass", (0, 52)), item("scenery.day.flowers", (0, 52)), item("scenery.day.cloudShadows", (0, 34)), item("scenery.always.castle", "castle"), item("scenery.always.mill", "mill")]


def sky(phase):
    light = [] if phase == "day" else [item(f"light.{phase}")]
    return light + [item(path, at) for path, at in PHASE_SKY[phase]]


def creatures(phase, mood="calm"):
    if phase == "night":
        return [item("creatures.owl.perch", "owl"), item(f"creatures.owl.{mood}", (100, 41)), item(f"creatures.fireflies.{mood}", "fireflies")]
    return [item(f"creatures.butterflies.{mood}", "butterflies"), item(f"creatures.starlings.{mood}", "starlings"), item(f"creatures.rabbit.{mood}", "rabbit")]


def world(phase, name, mood="calm"):
    return sky(phase) + weather(name) + scenery(phase) + creatures(phase, mood)


def quiet(phase, name):
    return sky(phase) + weather(name) + scenery(phase)


def boss_scene(phase):
    alarmed = [item("creatures.owl.perch", "owl"), item("creatures.owl.alarmed", (100, 41), every=6), item("creatures.fireflies.alarmed", "fireflies", every=6)] if phase == "night" else [
        item("creatures.starlings.alarmed", (72, 40), every=6), item("creatures.butterflies.alarmed", "butterflies", every=6), item("creatures.rabbit.alarmed", "rabbit", every=6)]
    turf = [entry for entry in scenery(phase) if not entry["path"].endswith(".grass")]
    return sky(phase) + weather("clear") + turf + alarmed + [
        item("reactions.bossSky", dim=0.9), item("reactions.bossGrass", (0, 52)), item("reactions.bossOut", every=6), item("reactions.enemyFlash", ENEMY_FEET, every=6)]


def boss_down_scene():
    return quiet("day", "clear") + [item("reactions.bossDown", every=5), item("creatures.rabbit.calm", "rabbit", start=1.2), item("creatures.starlings.calm", "starlings", start=1.6), item("creatures.butterflies.calm", "butterflies", start=1.0)]


def rest_scene(phase):
    if phase == "night":
        return quiet(phase, "clear") + [item("creatures.owl.perch", "owl"), item("creatures.owl.friendly", (100, 41)), item("creatures.fireflies.friendly", (HERO_FEET[0] + 4, 38))]
    return quiet(phase, "clear") + [
        item("creatures.butterflies.friendly", HERO_HEAD), item("creatures.starlings.friendly", (98, 52)),
        item("creatures.rabbit.hop", (92, 52), until=2.4, move=(-12.5, 0)), item("creatures.rabbit.friendly", (62, 52), start=2.4)]


def streak_scene(tier):
    return quiet("day", "clear") + creatures("day") + [item(f"reactions.sway{tier}", HERO_FEET)]


SCENES = {
    "day-clear": world("day", "clear"),
    "day-cloudy": world("day", "cloudy"),
    "day-rain": world("day", "rain"),
    "day-wind": world("day", "wind"),
    "day-rainbow": world("day", "clear") + [item("special.rainbow")],
    "morning": world("morning", "clear"),
    "evening": world("evening", "clear"),
    "night-clear": world("night", "clear"),
    "night-cloudy": world("night", "cloudy"),
    "night-rain": world("night", "rain"),
    "night-wind": world("night", "wind"),
    "boss-day": boss_scene("day"),
    "boss-night": boss_scene("night"),
    "boss-down": boss_down_scene(),
    "rest-day": rest_scene("day"),
    "rest-night": rest_scene("night"),
    "streak-1": streak_scene(1),
    "streak-2": streak_scene(2),
    "streak-3": streak_scene(3),
    "enemy-flash": world("day", "clear") + [item("reactions.enemyFlash", ENEMY_FEET, every=2)],
    "guardian": quiet("day", "cloudy") + [item("reactions.guardianCrack", every=3), item("creatures.rabbit.hidden", "rabbit", every=3), item("creatures.butterflies.hidden", "butterflies", every=3), item("creatures.starlings.hidden", "starlings", every=3)],
    "guardian-night": quiet("night", "clear") + [item("reactions.guardianCrack", every=3), item("creatures.owl.perch", "owl"), item("creatures.owl.hidden", (100, 41), every=3), item("creatures.fireflies.hidden", "fireflies", every=3)],
    "prestige": world("day", "clear") + [item("reactions.prestigeWave", every=3)],
}
PHASE_OF = {"morning": "morning", "evening": "evening"}
BOSS = {"boss-day", "boss-night", "guardian", "guardian-night"}
REELS = {
    "weather-day": ["day-clear", "day-cloudy", "day-rain", "day-wind", "day-rainbow"],
    "weather-night": ["night-clear", "night-cloudy", "night-rain", "night-wind"],
    "day-phases": ["morning", "day-clear", "evening", "night-clear"],
    "boss": ["boss-day", "boss-night", "boss-down"],
    "rest-and-streak": ["rest-day", "rest-night", "streak-1", "streak-2", "streak-3"],
    "world-events": ["enemy-flash", "guardian", "guardian-night", "prestige"],
}
SECONDS = {"weather-day": 6, "weather-night": 6, "day-phases": 6, "boss": 6, "rest-and-streak": 6, "world-events": 6}


def phase_of(name):
    if name in PHASE_OF:
        return PHASE_OF[name]
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


class Stage:
    def __init__(self, art, backdrop, backgrounds):
        self.art = art
        self.backdrop = backdrop
        self.hero = grown(Image.open(HERO).convert("RGBA").crop((0, 0, 30, 30)), art.scale)
        self.enemy = dummy()
        self.layers = None
        if backdrop == "hills":
            self.layers = {name: Image.open(backgrounds / f"{name}.png").convert("RGBA").crop((0, 0, art.view[0], art.view[1])) for name in ("back", "mid", "ground")}

    def clip(self, entry, time):
        frames = self.art.frames(entry["path"])
        sheet = self.art.entry(entry["path"])
        local = time - entry["start"]
        if entry["every"]:
            local = local % entry["every"]
        if local < 0 or (entry["until"] is not None and time >= entry["until"]):
            return None, sheet, local
        if sheet["frames"] == 1:
            return frames[0], sheet, local
        index = int(local * sheet["fps"] + 1e-6)
        if sheet["loop"]:
            return frames[index % sheet["frames"]], sheet, local
        return (frames[index] if index < sheet["frames"] else None), sheet, local

    def paint(self, canvas, entry, time, tone):
        frame, sheet, local = self.clip(entry, time)
        if frame is None:
            return
        if not sheet.get("glow"):
            frame = tinted(frame, tone)
        scale = self.art.scale
        x, y = spot(entry["at"])
        if entry["move"]:
            x, y = x + entry["move"][0] * local, y + entry["move"][1] * local
        left, top = round(x) - sheet["pivot"][0], round(y) - sheet["pivot"][1]
        frame = faded(grown(frame, scale), entry["dim"])
        scroll = sheet.get("scroll")
        if not scroll:
            canvas.alpha_composite(frame, (left * scale, top * scale)) if left >= 0 and top >= 0 else self.paste_clipped(canvas, frame, left * scale, top * scale)
            return
        shift = round(time * scroll * scale) % frame.width
        for start in (shift - frame.width, shift):
            self.paste_clipped(canvas, frame, start, top * scale)

    @staticmethod
    def paste_clipped(canvas, frame, x, y):
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        layer.paste(frame, (x, y))
        canvas.alpha_composite(layer)

    def actors(self, canvas, boss):
        scale = self.art.scale
        canvas.alpha_composite(self.hero, ((HERO_FEET[0] - HERO_PIVOT[0]) * scale, (HERO_FEET[1] - HERO_PIVOT[1]) * scale))
        grow = scale * (BOSS_SCALE if boss else 1)
        enemy = grown(self.enemy, grow)
        self.paste_clipped(canvas, enemy, ENEMY_FEET[0] * scale - enemy.width // 2, ENEMY_FEET[1] * scale - enemy.height)

    def frame(self, name, time):
        art, scale = self.art, self.art.scale
        entries = SCENES[name]
        canvas = Image.new("RGBA", art.view, FILL.get(self.backdrop, DARK))
        tone = art.tint(phase_of(name))
        if not self.layers:
            canvas.paste(FLOOR[self.backdrop], (0, HERO_FEET[1] * scale, art.view[0], HERO_FEET[1] * scale + scale))
        for layer in ORDER:
            if layer in ("back", "mid"):
                if self.layers:
                    canvas.alpha_composite(tinted(self.layers[layer], tone))
                continue
            if layer == "turf":
                if self.layers:
                    canvas.alpha_composite(tinted(self.layers["ground"], tone))
                continue
            if layer == "actors":
                self.actors(canvas, name in BOSS)
                continue
            for entry in entries:
                if art.entry(entry["path"])["layer"] == layer:
                    self.paint(canvas, entry, time, tone)
        return canvas


def reel(stage, name, scenes, seconds, out, columns=1):
    art = stage.art
    wide, high = art.view[0], art.view[1] + LABEL
    rows = (len(scenes) + columns - 1) // columns
    frames = []
    for tick in range(int(seconds * FPS)):
        sheet = Image.new("RGBA", (wide * columns, high * rows), DARK)
        draw = ImageDraw.Draw(sheet)
        for index, scene in enumerate(scenes):
            x, y = (index % columns) * wide, (index // columns) * high
            sheet.paste(stage.frame(scene, tick / FPS), (x, y + LABEL))
            draw.text((x + 3, y + 1), scene, fill=(170, 180, 205))
        frames.append(sheet.convert("RGB"))
    out.mkdir(parents=True, exist_ok=True)
    frames[0].save(out / f"{name}.gif", save_all=True, append_images=frames[1:], duration=1000 // FPS, loop=0)
    return frames


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
    parser.add_argument("--backgrounds", type=Path)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--reel", action="append")
    parser.add_argument("--stills", nargs="*")
    parser.add_argument("--times", nargs="*", type=float, default=[0.5])
    return parser.parse_args()


def main():
    args = parse_args()
    art = Art()
    names = args.reel or list(REELS)
    if args.backgrounds:
        if not args.out:
            print("--backgrounds needs --out outside the repository")
            return 1
        stages = [("hills", Stage(art, "hills", args.backgrounds), args.out)]
    else:
        stages = [(backdrop, Stage(art, backdrop, None), args.out or OUT) for backdrop in ("dark", "light")]
    for backdrop, stage, out in stages:
        if args.stills is not None:
            still(stage, args.stills or list(SCENES), args.times, out, f"stills-{backdrop}")
            continue
        for name in names:
            reel(stage, f"{name}-{backdrop}", REELS[name], SECONDS[name], out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
