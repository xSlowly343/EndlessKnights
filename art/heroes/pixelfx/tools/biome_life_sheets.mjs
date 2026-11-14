import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { randomUUID } from "node:crypto";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..", "..", "..");
const SOURCES = resolve(HERE, "..", "biomelife");
const RESOURCES = join(REPO, "Assets", "Game", "Art", "Resources");
const OUT = join(RESOURCES, "BiomeLife");
const RULES = join(REPO, "Assets", "StreamingAssets", "Data", "biome_life.json");
const META_TEMPLATE = join(RESOURCES, "Heroes", "Seeker", "Seeker_Void_Crescent_87x42_8f.png.meta");
const MODULES = ["weather", "light", "critters", "reactions", "scenery", "snow"];
const MAX_SHEET_WIDTH = 4096;
const STRIP = [160, 60];
const GROUND = 52;
const SCALE = 3;
const FULL = [STRIP, [0, 0]];
const SKY = [[160, 24], [0, 0]];
const TURF = [[160, 12], [0, 10]];
const MOODS = ["calm", "friendly", "alarmed", "hidden"];
const TINTS = { morning: "#ffe6ea", day: "#ffffff", evening: "#ffd2b0", night: "#6f82c8" };

const loop = (frames, fps, more = {}) => ({ frames, fps, loop: true, ...more });
const once = (frames, fps, more = {}) => ({ frames, fps, loop: false, ...more });
const still = (more = {}) => ({ frames: 1, fps: 0, loop: true, ...more });
const GLOW = { glow: true };
const TURF_AT = { at: [0, GROUND] };
const shared = (name, more = {}) => ({ shared: name, more });
const placed = (home, spec) => (Array.isArray(spec) && !spec[4].at ? [...spec.slice(0, 4), { ...spec[4], at: home }] : spec);
const creature = (home, moods) => Object.fromEntries(Object.entries(moods).map(([mood, spec]) => [mood, placed(home, spec)]));
const sky = (sun, rise, set, moon) => ({
  sun: shared("sun", { at: sun }),
  sunRise: shared("sunLow", { at: rise }),
  sunSet: shared("sunLow", { at: set }),
  moon: shared("moon", { at: moon }),
  stars: shared("stars"),
});
const LIGHT = { morning: shared("lightMorning"), evening: shared("lightEvening"), night: shared("lightNight") };

const COMMON = {
  lightMorning: ["lightMorning", "LightMorning", FULL, "light", still(GLOW)],
  lightEvening: ["lightEvening", "LightEvening", FULL, "light", still(GLOW)],
  lightNight: ["lightNight", "LightNight", FULL, "light", still(GLOW)],
  sun: ["sun", "Sun", [[21, 21], [10, 10]], "sky", loop(8, 4, GLOW)],
  sunLow: ["sunLow", "SunLow", [[27, 27], [13, 13]], "sky", loop(8, 4, GLOW)],
  moon: ["moon", "Moon", [[23, 23], [11, 11]], "sky", loop(8, 4, GLOW)],
  stars: ["stars", "Stars", SKY, "sky", loop(16, 4, GLOW)],
  enemyFlash: ["enemyFlash", "EnemyFlash", [[22, 40], [11, 38]], "ground", once(10, 15, { ...GLOW, at: "enemy" })],
  guardianCrack: ["guardianCrack", "GuardianCrack", [[160, 30], [0, 0]], "sky", once(14, 12, GLOW)],
  prestigeWave: ["prestigeWave", "PrestigeWave", FULL, "front", once(16, 15, GLOW)],
};

const HILLS = {
  order: ["bg:back", "light", "sky", "hill", "bg:mid", "far", "bg:ground", "ground", "actors", "front"],
  weather: {
    clear: {
      far: ["clearFar", "ClearFar", SKY, "sky", still({ scroll: 1.2 })],
      near: ["clearNear", "ClearNear", FULL, "front", loop(24, 8)],
      mist: ["clearMist", "ClearMist", FULL, "ground", loop(24, 6)],
    },
    cloudy: {
      far: ["cloudyFar", "CloudyFar", [[160, 28], [0, 0]], "sky", still({ scroll: 2 })],
      near: ["cloudyNear", "CloudyNear", SKY, "sky", still({ scroll: 5 })],
      mist: ["cloudyMist", "CloudyMist", FULL, "far", loop(16, 4)],
    },
    rain: {
      far: ["rainFar", "RainFar", FULL, "sky", loop(12, 12)],
      near: ["rainNear", "RainNear", FULL, "front", loop(12, 15)],
      mist: ["rainMist", "RainMist", FULL, "ground", loop(16, 8)],
    },
    wind: {
      far: ["windFar", "WindFar", SKY, "sky", still({ scroll: 9 })],
      near: ["windNear", "WindNear", FULL, "front", loop(24, 12)],
      mist: ["windMist", "WindMist", FULL, "ground", loop(16, 12)],
    },
  },
  special: {
    rainbow: ["rainbow", "Rainbow", FULL, "sky", loop(8, 4, GLOW)],
  },
  light: LIGHT,
  sky: sky([140, 8], [22, 20], [136, 21], [114, 9]),
  creatures: {
    butterflies: creature([66, 40], {
      calm: ["butterfliesCalm", "ButterfliesCalm", [[28, 22], [14, 11]], "ground", loop(24, 12)],
      friendly: ["butterfliesFriendly", "ButterfliesFriendly", [[11, 9], [5, 8]], "front", loop(16, 8, { at: "heroHead" })],
      alarmed: ["butterfliesAlarmed", "ButterfliesAlarmed", [[28, 30], [14, 19]], "ground", once(10, 15)],
      hidden: ["butterfliesHidden", "ButterfliesHidden", [[28, 22], [14, 11]], "ground", once(8, 12)],
    }),
    starlings: creature([72, 11], {
      calm: ["starlingsCalm", "StarlingsCalm", [[56, 24], [28, 12]], "sky", loop(24, 12, { drift: 3 })],
      friendly: ["starlingsFriendly", "StarlingsFriendly", [[34, 8], [17, 7]], "ground", loop(16, 8, { at: [98, GROUND] })],
      alarmed: ["starlingsAlarmed", "StarlingsAlarmed", [[72, 44], [36, 40]], "far", once(16, 15, { at: [72, 40] })],
      hidden: ["starlingsHidden", "StarlingsHidden", [[56, 24], [28, 12]], "sky", once(10, 12)],
    }),
    rabbit: creature([92, GROUND], {
      calm: ["rabbitCalm", "RabbitCalm", [[18, 16], [9, 14]], "ground", loop(32, 8)],
      friendly: ["rabbitFriendly", "RabbitFriendly", [[18, 16], [9, 14]], "ground", loop(16, 8)],
      alarmed: ["rabbitAlarmed", "RabbitAlarmed", [[44, 18], [34, 16]], "ground", once(14, 15)],
      hidden: ["rabbitHidden", "RabbitHidden", [[18, 16], [9, 14]], "ground", once(6, 12)],
      hop: ["rabbitHop", "RabbitHop", [[18, 16], [9, 14]], "ground", loop(6, 12, { step: 10 })],
    }),
    fireflies: creature([70, 38], {
      calm: ["firefliesCalm", "FirefliesCalm", [[48, 28], [24, 14]], "front", loop(32, 8, GLOW)],
      friendly: ["firefliesFriendly", "FirefliesFriendly", [[26, 22], [13, 11]], "front", loop(24, 8, { ...GLOW, at: "hero" })],
      alarmed: ["firefliesAlarmed", "FirefliesAlarmed", [[48, 28], [24, 14]], "front", once(10, 15, GLOW)],
      hidden: ["firefliesHidden", "FirefliesHidden", [[48, 28], [24, 14]], "front", once(8, 12, GLOW)],
    }),
    owl: creature([100, GROUND - 11], {
      calm: ["owlCalm", "OwlCalm", [[13, 12], [6, 11]], "ground", loop(32, 8)],
      friendly: ["owlFriendly", "OwlFriendly", [[13, 12], [6, 11]], "ground", loop(16, 8)],
      alarmed: ["owlAlarmed", "OwlAlarmed", [[44, 34], [36, 31]], "ground", once(14, 15)],
      hidden: ["owlHidden", "OwlHidden", [[13, 12], [6, 11]], "ground", once(8, 12)],
      perch: ["owlPerch", "OwlPerch", [[13, 16], [6, 15]], "ground", still({ at: [100, GROUND] })],
    }),
  },
  reactions: {
    enemyFlash: shared("enemyFlash"),
    bossOut: ["bossOut", "BossOut", FULL, "front", once(16, 15)],
    bossSky: ["bossSky", "BossSky", [[160, 28], [0, 0]], "sky", loop(16, 6)],
    bossGround: ["bossGrass", "BossGrass", TURF, "ground", loop(8, 12, TURF_AT)],
    bossDown: ["bossDown", "BossDown", FULL, "ground", once(20, 12, GLOW)],
    guardianCrack: shared("guardianCrack"),
    prestigeWave: shared("prestigeWave"),
    sway1: ["sway1", "Sway1", [[40, 14], [20, 11]], "ground", loop(8, 8, { at: "hero" })],
    sway2: ["sway2", "Sway2", [[40, 14], [20, 11]], "ground", loop(8, 12, { at: "hero" })],
    sway3: ["sway3", "Sway3", [[40, 22], [20, 19]], "ground", loop(12, 15, { at: "hero" })],
  },
  scenery: {
    day: {
      grass: ["grass", "Grass", TURF, "ground", loop(16, 8, TURF_AT)],
      cloudShadows: ["cloudShadows", "CloudShadows", [[160, 20], [0, 0]], "ground", still({ scroll: 4, at: [0, 34] })],
      flowers: ["flowers", "Flowers", TURF, "ground", loop(8, 6, TURF_AT)],
    },
    night: {
      grass: ["grass", "Grass", TURF, "ground", loop(16, 8, TURF_AT)],
      flowersClose: ["flowersClose", "FlowersClose", TURF, "ground", once(6, 8, TURF_AT)],
      flowers: ["flowersNight", "FlowersNight", TURF, "ground", still(TURF_AT)],
      castleLights: ["castleLights", "CastleLights", [[30, 20], [15, 19]], "hill", loop(8, 6, { ...GLOW, at: [43, 25] })],
      mist: ["nightMist", "NightMist", FULL, "ground", loop(16, 6)],
    },
    always: {
      castle: ["castle", "Castle", [[30, 26], [15, 19]], "hill", still({ at: [43, 25] })],
      mill: ["mill", "Mill", [[21, 30], [10, 23]], "hill", loop(8, 8, { at: [99, 32] })],
    },
  },
};

const SNOW = {
  order: ["bg:back", "light", "sky", "bg:mid", "far", "bg:ground", "bg:front", "ground", "actors", "front"],
  weather: {
    clear: {
      far: ["clearFar", "ClearFar", SKY, "sky", still({ scroll: 1.2 })],
      near: ["snowClearNear", "ClearNear", FULL, "front", loop(24, 8, GLOW)],
      mist: ["snowClearMist", "ClearMist", FULL, "ground", loop(16, 4)],
    },
    snow: {
      far: ["snowFallFar", "SnowFar", FULL, "sky", loop(24, 8)],
      near: ["snowFallNear", "SnowNear", FULL, "front", loop(24, 8)],
      mist: ["snowFallMist", "SnowMist", FULL, "ground", loop(16, 4)],
    },
    blizzard: {
      far: ["snowBlizzardFar", "BlizzardFar", FULL, "far", loop(16, 12)],
      near: ["snowBlizzardNear", "BlizzardNear", FULL, "front", loop(16, 15)],
      mist: ["snowBlizzardMist", "BlizzardMist", FULL, "ground", loop(16, 12)],
    },
    frost: {
      far: ["snowFrostFar", "FrostFar", [[160, 34], [0, 0]], "sky", loop(16, 4, GLOW)],
      near: ["snowFrostNear", "FrostNear", FULL, "front", loop(24, 8, GLOW)],
      mist: ["snowFrostMist", "FrostMist", FULL, "ground", loop(16, 4)],
    },
  },
  special: {
    aurora: ["snowAurora", "Aurora", [[160, 28], [0, 0]], "sky", loop(24, 6, GLOW)],
  },
  light: LIGHT,
  sky: sky([128, 6], [20, 9], [140, 9], [118, 6]),
  creatures: {
    bullfinches: creature([66, 34], {
      calm: ["bullfinchesCalm", "BullfinchesCalm", [[40, 24], [20, 12]], "ground", loop(24, 12)],
      friendly: ["bullfinchesFriendly", "BullfinchesFriendly", [[21, 16], [10, 15]], "ground", loop(16, 8, { at: [66, GROUND] })],
      alarmed: ["bullfinchesAlarmed", "BullfinchesAlarmed", [[48, 36], [28, 24]], "ground", once(10, 15)],
      hidden: ["bullfinchesHidden", "BullfinchesHidden", [[48, 26], [28, 12]], "ground", once(8, 12)],
      perch: ["bullfinchesPerch", "BullfinchesPerch", [[21, 14], [10, 12]], "ground", still({ at: [66, GROUND] })],
    }),
    hare: creature([94, GROUND], {
      calm: ["hareCalm", "HareCalm", [[18, 16], [9, 14]], "ground", loop(32, 8)],
      friendly: ["hareFriendly", "HareFriendly", [[18, 16], [9, 14]], "ground", loop(16, 8)],
      alarmed: ["hareAlarmed", "HareAlarmed", [[44, 18], [34, 16]], "ground", once(14, 15)],
      hidden: ["hareHidden", "HareHidden", [[18, 16], [9, 14]], "ground", once(6, 12)],
      hop: ["hareHop", "HareHop", [[18, 16], [9, 14]], "ground", loop(6, 12, { step: 10 })],
    }),
    tracks: creature([70, 56], {
      calm: ["tracksCalm", "TracksCalm", [[40, 8], [20, 6]], "ground", loop(32, 8)],
      friendly: ["tracksFriendly", "TracksFriendly", [[40, 8], [20, 6]], "ground", loop(32, 8)],
      alarmed: ["tracksAlarmed", "TracksAlarmed", [[40, 12], [20, 10]], "ground", once(8, 12)],
      hidden: ["tracksHidden", "TracksHidden", [[40, 8], [20, 6]], "ground", once(6, 12)],
    }),
    auroraGlints: creature([70, 30], {
      calm: ["auroraGlintsCalm", "AuroraGlintsCalm", [[48, 28], [24, 14]], "front", loop(32, 8, GLOW)],
      friendly: ["auroraGlintsFriendly", "AuroraGlintsFriendly", [[26, 22], [13, 11]], "front", loop(24, 8, { ...GLOW, at: "hero" })],
      alarmed: ["auroraGlintsAlarmed", "AuroraGlintsAlarmed", [[48, 28], [24, 14]], "front", once(10, 15, GLOW)],
      hidden: ["auroraGlintsHidden", "AuroraGlintsHidden", [[48, 28], [24, 14]], "front", once(8, 12, GLOW)],
    }),
    arcticFox: creature([96, GROUND], {
      calm: ["arcticFoxCalm", "ArcticFoxCalm", [[20, 14], [9, 12]], "ground", loop(32, 8)],
      friendly: ["arcticFoxFriendly", "ArcticFoxFriendly", [[20, 14], [9, 12]], "ground", loop(16, 8)],
      alarmed: ["arcticFoxAlarmed", "ArcticFoxAlarmed", [[56, 18], [44, 16]], "ground", once(14, 15)],
      hidden: ["arcticFoxHidden", "ArcticFoxHidden", [[24, 24], [11, 20]], "ground", once(11, 12)],
    }),
  },
  reactions: {
    enemyFlash: shared("enemyFlash"),
    bossOut: ["snowBossOut", "BossOut", FULL, "front", once(16, 15)],
    bossSky: ["bossSky", "BossSky", [[160, 28], [0, 0]], "sky", loop(16, 6)],
    bossGround: ["snowBossGround", "BossGround", TURF, "ground", loop(8, 12, TURF_AT)],
    bossSlab: ["snowSlab", "BossSlab", [[30, 48], [15, 5]], "far", once(14, 12, { at: [58, 16] })],
    bossDown: ["bossDown", "BossDown", FULL, "ground", once(20, 12, GLOW)],
    guardianCrack: shared("guardianCrack"),
    prestigeWave: shared("prestigeWave"),
    sway1: ["snowSway1", "Sway1", [[40, 14], [20, 11]], "ground", loop(8, 8, { at: "hero" })],
    sway2: ["snowSway2", "Sway2", [[40, 14], [20, 11]], "ground", loop(8, 12, { at: "hero" })],
    sway3: ["snowSway3", "Sway3", [[40, 22], [20, 19]], "ground", loop(12, 15, { at: "hero" })],
  },
  scenery: {
    day: {
      sparkle: ["snowSparkle", "Sparkle", FULL, "ground", loop(16, 8, GLOW)],
      slide: ["snowSlide", "Slide", [[18, 22], [4, 3]], "far", loop(32, 8, { at: [112, 20] })],
    },
    night: {
      iceGlow: ["snowIceGlow", "IceGlow", TURF, "ground", loop(16, 6, { ...GLOW, ...TURF_AT })],
      bigFlakes: ["snowBigFlakes", "BigFlakes", FULL, "ground", loop(24, 6)],
    },
    always: {},
  },
};
const LOOKS = { hills: ["Hills", HILLS], snow: ["Snow", SNOW] };

const args = process.argv.slice(2);
const only = args.includes("--only") ? args[args.indexOf("--only") + 1] : null;
const pixelfx = args[0] && !args[0].startsWith("--") ? resolve(args[0]) : null;
if (!pixelfx || !existsSync(join(pixelfx, "src", "core", "index.js"))) {
  console.error("usage: node biome_life_sheets.mjs <pixelfx checkout> [--only <module>]");
  process.exit(1);
}

const core = await import(pathToFileURL(join(pixelfx, "src", "core", "index.js")));
const lib = await import(pathToFileURL(join(pixelfx, "tools", "lib", "fxlib.mjs")));
const kit = (await import(pathToFileURL(join(SOURCES, "..", "transitions", "_kit.js")))).default(core);
const scene = (await import(pathToFileURL(join(SOURCES, "_scene.js")))).default(core);
const draws = {}, owner = {};
const fx = { ...core, kit, scene, draws };
for (const name of MODULES) {
  if (!existsSync(join(SOURCES, `${name}.js`))) continue;
  const part = (await import(pathToFileURL(join(SOURCES, `${name}.js`)))).default(fx);
  for (const key of Object.keys(part)) owner[key] = name;
  Object.assign(draws, part);
}

function render(entry, draw) {
  const { width, height, frames, pivot } = entry;
  const frame = core.createFrame(width, height);
  const cells = [];
  for (let i = 0; i < frames; i++) {
    core.clearFrame(frame);
    const painter = core.painterFor(frame).place(pivot[0], pivot[1]);
    const u = entry.loop ? i / frames : frames > 1 ? i / (frames - 1) : 0;
    draw(painter, u, { frames, fps: entry.fps, seconds: entry.fps ? frames / entry.fps : 1, width, height });
    cells.push(core.composite(frame, new Uint8ClampedArray(width * height * 4), null));
  }
  return cells;
}

function strip(entry, cells) {
  const sheet = lib.canvas(entry.width * entry.frames, entry.height, [0, 0, 0, 0]);
  cells.forEach((cell, i) => {
    for (let y = 0; y < entry.height; y++) {
      const row = cell.subarray(y * entry.width * 4, (y + 1) * entry.width * 4);
      sheet.data.set(row, (y * sheet.w + i * entry.width) * 4);
    }
  });
  return sheet.png();
}

function write(entry, cells) {
  mkdirSync(OUT, { recursive: true });
  const png = join(OUT, `${entry.file}.png`);
  writeFileSync(png, strip(entry, cells));
  const meta = png + ".meta";
  if (existsSync(meta)) return;
  const text = readFileSync(META_TEMPLATE, "utf8").replaceAll("maxTextureSize: 2048", `maxTextureSize: ${MAX_SHEET_WIDTH}`);
  const lines = text.split("\n");
  lines[1] = "guid: " + randomUUID().replaceAll("-", "");
  writeFileSync(meta, lines.join("\n"));
}

function seam(entry, cells) {
  if (entry.layer === "light" || entry.width !== STRIP[0]) return 0;
  let worst = 0;
  for (const cell of cells) {
    let left = 0, right = 0;
    for (let y = 0; y < entry.height; y++) {
      left += cell[(y * entry.width) * 4 + 3];
      right += cell[(y * entry.width + entry.width - 1) * 4 + 3];
    }
    worst = Math.max(worst, Math.abs(left - right) / (255 * entry.height));
  }
  return worst;
}

function report(entry, cells) {
  let empty = 0, same = 0, cover = 0, zone = 0;
  const zoneLeft = STRIP[0] - 40, zoneTop = GROUND - 20;
  cells.forEach((cell, n) => {
    let filled = 0, solid = 0;
    for (let i = 3; i < cell.length; i += 4) {
      if (cell[i]) filled++;
      if (cell[i] > 200) solid++;
    }
    if (!filled) empty++;
    cover = Math.max(cover, solid / (entry.width * entry.height));
    if (n && cell.every((value, i) => value === cells[n - 1][i])) same++;
    if (entry.width === STRIP[0] && entry.height === STRIP[1] && entry.layer !== "light")
      for (let y = zoneTop; y < GROUND; y++) for (let x = zoneLeft; x < STRIP[0]; x++) if (cell[(y * entry.width + x) * 4 + 3] > 200) zone++;
  });
  const gap = seam(entry, cells);
  const notes = [
    `${entry.frames}f`,
    entry.fps ? `${(entry.frames / entry.fps).toFixed(2)}s` : "still",
    `cover ${(cover * 100).toFixed(1)}%`,
    empty ? `empty frames: ${empty}` : "",
    same ? `repeated frames: ${same}` : "",
    zone ? `solid pixels in the enemy zone: ${zone}` : "",
    gap > 0.25 ? `edges differ: ${gap.toFixed(2)}` : "",
  ].filter(Boolean);
  console.log(`${entry.file}  ${notes.join("  ")}`);
  return cover;
}

function build(prefix, spec, problems) {
  if (spec.shared) return { ...common[spec.shared], ...spec.more };
  if (typeof spec[0] === "string" && spec[0].includes(":")) return spec;
  if (Array.isArray(spec)) {
    const [key, name, [[width, height], pivot], layer, timing] = spec;
    const entry = { file: `BiomeLife_${prefix}_${name}_${width}x${height}_${timing.frames}f`, width, height, pivot, layer, ...timing };
    if (width * timing.frames > MAX_SHEET_WIDTH) problems.push(`${entry.file}: wider than ${MAX_SHEET_WIDTH}`);
    if (!draws[key]) {
      problems.push(`${entry.file}: no draw function '${key}'`);
      return entry;
    }
    if (only && owner[key] !== only) return entry;
    const cells = render(entry, draws[key]);
    write(entry, cells);
    const cover = report(entry, cells);
    if (key === "rainNear" && cover > frontCover) problems.push(`${entry.file}: front cover ${cover.toFixed(2)} is above ${frontCover}`);
    written++;
    return entry;
  }
  const out = {};
  for (const [key, value] of Object.entries(spec)) out[key] = build(prefix, value, problems);
  return out;
}

function checkNames(id, look, art, problems) {
  for (const weather of look.weathers) if (!art.weather[weather.id]) problems.push(`${id}: no sheets for weather '${weather.id}'`);
  if (look.special && !art.special[look.special.id]) problems.push(`${id}: no sheet for special '${look.special.id}'`);
  for (const name of [...look.day.background, ...look.day.ground, ...look.night.background, ...look.night.ground]) {
    const creature = art.creatures[name];
    if (!creature) problems.push(`${id}: no sheets for creature '${name}'`);
    else for (const mood of MOODS) if (!creature[mood]) problems.push(`${id}: creature '${name}' has no '${mood}' sheet`);
  }
}

const rules = JSON.parse(readFileSync(RULES, "utf8"));
const frontCover = rules.weather.frontCover;
const list = { version: 1, scale: SCALE, strip: STRIP, groundY: GROUND, tints: TINTS, looks: {} };
const problems = [];
let written = 0;
const common = build("Common", COMMON, problems);
for (const [id, [prefix, spec]] of Object.entries(LOOKS)) {
  list.looks[id] = build(prefix, spec, problems);
  const look = rules.looks.find((item) => item.id === id);
  if (!look) problems.push(`${id}: no such look in biome_life.json`);
  else checkNames(id, look, list.looks[id], problems);
}

if (!only) writeFileSync(join(OUT, "biome_life_art.json"), JSON.stringify(list, null, 2) + "\n");
console.log(`${written} sheets written`);
for (const problem of problems) console.error(problem);
process.exit(problems.length && !only ? 1 : 0);
