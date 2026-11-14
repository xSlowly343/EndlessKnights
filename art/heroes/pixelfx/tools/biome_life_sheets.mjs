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
const MODULES = ["weather", "light", "critters", "reactions", "scenery"];
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

const HILLS = {
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
  light: {
    morning: ["lightMorning", "LightMorning", FULL, "light", still(GLOW)],
    evening: ["lightEvening", "LightEvening", FULL, "light", still(GLOW)],
    night: ["lightNight", "LightNight", FULL, "light", still(GLOW)],
  },
  sky: {
    sun: ["sun", "Sun", [[21, 21], [10, 10]], "sky", loop(8, 4, GLOW)],
    sunLow: ["sunLow", "SunLow", [[27, 27], [13, 13]], "sky", loop(8, 4, GLOW)],
    moon: ["moon", "Moon", [[23, 23], [11, 11]], "sky", loop(8, 4, GLOW)],
    stars: ["stars", "Stars", SKY, "sky", loop(16, 4, GLOW)],
  },
  creatures: {
    butterflies: {
      calm: ["butterfliesCalm", "ButterfliesCalm", [[28, 22], [14, 11]], "ground", loop(24, 12)],
      friendly: ["butterfliesFriendly", "ButterfliesFriendly", [[11, 9], [5, 8]], "front", loop(16, 8, { at: "heroHead" })],
      alarmed: ["butterfliesAlarmed", "ButterfliesAlarmed", [[28, 30], [14, 19]], "ground", once(10, 15)],
      hidden: ["butterfliesHidden", "ButterfliesHidden", [[28, 22], [14, 11]], "ground", once(8, 12)],
    },
    starlings: {
      calm: ["starlingsCalm", "StarlingsCalm", [[56, 24], [28, 12]], "sky", loop(24, 12, { drift: 3 })],
      friendly: ["starlingsFriendly", "StarlingsFriendly", [[34, 8], [17, 7]], "ground", loop(16, 8, { at: "ground" })],
      alarmed: ["starlingsAlarmed", "StarlingsAlarmed", [[72, 44], [36, 40]], "far", once(16, 15)],
      hidden: ["starlingsHidden", "StarlingsHidden", [[56, 24], [28, 12]], "sky", once(10, 12)],
    },
    rabbit: {
      calm: ["rabbitCalm", "RabbitCalm", [[18, 16], [9, 14]], "ground", loop(32, 8)],
      friendly: ["rabbitFriendly", "RabbitFriendly", [[18, 16], [9, 14]], "ground", loop(16, 8)],
      alarmed: ["rabbitAlarmed", "RabbitAlarmed", [[44, 18], [34, 16]], "ground", once(14, 15)],
      hidden: ["rabbitHidden", "RabbitHidden", [[18, 16], [9, 14]], "ground", once(6, 12)],
      hop: ["rabbitHop", "RabbitHop", [[18, 16], [9, 14]], "ground", loop(6, 12, { step: 10 })],
    },
    fireflies: {
      calm: ["firefliesCalm", "FirefliesCalm", [[48, 28], [24, 14]], "front", loop(32, 8, GLOW)],
      friendly: ["firefliesFriendly", "FirefliesFriendly", [[26, 22], [13, 11]], "front", loop(24, 8, { ...GLOW, at: "hero" })],
      alarmed: ["firefliesAlarmed", "FirefliesAlarmed", [[48, 28], [24, 14]], "front", once(10, 15, GLOW)],
      hidden: ["firefliesHidden", "FirefliesHidden", [[48, 28], [24, 14]], "front", once(8, 12, GLOW)],
    },
    owl: {
      calm: ["owlCalm", "OwlCalm", [[13, 12], [6, 11]], "ground", loop(32, 8, { on: "perch" })],
      friendly: ["owlFriendly", "OwlFriendly", [[13, 12], [6, 11]], "ground", loop(16, 8, { on: "perch" })],
      alarmed: ["owlAlarmed", "OwlAlarmed", [[44, 34], [36, 31]], "ground", once(14, 15, { on: "perch" })],
      hidden: ["owlHidden", "OwlHidden", [[13, 12], [6, 11]], "ground", once(8, 12, { on: "perch" })],
      perch: ["owlPerch", "OwlPerch", [[13, 16], [6, 15]], "ground", still({ top: 11 })],
    },
  },
  reactions: {
    enemyFlash: ["enemyFlash", "EnemyFlash", [[22, 40], [11, 38]], "ground", once(10, 15, { ...GLOW, at: "enemy" })],
    bossOut: ["bossOut", "BossOut", FULL, "front", once(16, 15)],
    bossSky: ["bossSky", "BossSky", [[160, 28], [0, 0]], "sky", loop(16, 6)],
    bossGrass: ["bossGrass", "BossGrass", TURF, "ground", loop(8, 12)],
    bossDown: ["bossDown", "BossDown", FULL, "ground", once(20, 12, GLOW)],
    guardianCrack: ["guardianCrack", "GuardianCrack", [[160, 30], [0, 0]], "sky", once(14, 12, GLOW)],
    prestigeWave: ["prestigeWave", "PrestigeWave", FULL, "front", once(16, 15, GLOW)],
    sway1: ["sway1", "Sway1", [[40, 14], [20, 11]], "ground", loop(8, 8, { at: "hero" })],
    sway2: ["sway2", "Sway2", [[40, 14], [20, 11]], "ground", loop(8, 12, { at: "hero" })],
    sway3: ["sway3", "Sway3", [[40, 22], [20, 19]], "ground", loop(12, 15, { at: "hero" })],
  },
  scenery: {
    day: {
      grass: ["grass", "Grass", TURF, "ground", loop(16, 8)],
      cloudShadows: ["cloudShadows", "CloudShadows", [[160, 20], [0, 0]], "ground", still({ scroll: 4, top: 34 })],
      flowers: ["flowers", "Flowers", TURF, "ground", loop(8, 6)],
    },
    night: {
      grass: ["grass", "Grass", TURF, "ground", loop(16, 8)],
      flowersClose: ["flowersClose", "FlowersClose", TURF, "ground", once(6, 8)],
      flowers: ["flowersNight", "FlowersNight", TURF, "ground", still()],
      castleLights: ["castleLights", "CastleLights", [[30, 20], [15, 19]], "hill", loop(8, 6, { ...GLOW, at: [43, 25] })],
      mist: ["nightMist", "NightMist", FULL, "ground", loop(16, 6)],
    },
    always: {
      castle: ["castle", "Castle", [[30, 26], [15, 19]], "hill", still({ at: [43, 25] })],
      mill: ["mill", "Mill", [[21, 30], [10, 23]], "hill", loop(8, 8, { at: [99, 32] })],
    },
  },
};
const LOOKS = { hills: ["Hills", HILLS] };

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
const fx = { ...core, kit, scene };
const draws = {}, owner = {};
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
