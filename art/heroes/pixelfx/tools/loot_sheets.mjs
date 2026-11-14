import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { randomUUID } from "node:crypto";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..", "..", "..");
const SOURCES = resolve(HERE, "..", "loot");
const RESOURCES = join(REPO, "Assets", "Game", "Art", "Resources");
const OUT = join(RESOURCES, "Loot");
const META_TEMPLATE = join(RESOURCES, "Heroes", "Seeker", "Seeker_Void_Crescent_87x42_8f.png.meta");
const MODULES = ["loot"];
const RARITIES = ["simple", "fine", "uncommon", "rare", "epic", "legendary", "mythic", "ancient", "rift", "astral", "primordial"];
const LOOP = { frames: 8, fps: 12, loops: true };
const SPIN = { frames: 6, fps: 12, loops: true };
const OPEN = { frames: 12, fps: 16 };
const BEAM_SECONDS = [1.2, 1.2, 1.4, 1.6, 1.8, 2.2, 2.4, 2.6, 3, 3, 3.4];
const SHEETS = [
  ...RARITIES.map((name, r) => [`beam${r}`, `Beam_${name[0].toUpperCase()}${name.slice(1)}`, [24, 50], [12, 47], LOOP, { rarity: name, seconds: BEAM_SECONDS[r] }]),
  ["setMark", "SetMark", [24, 50], [12, 47], LOOP, {}],
  ["uniqueMark", "UniqueMark", [24, 50], [12, 47], LOOP, {}],
  ["bundle", "Bundle", [12, 12], [6, 11], LOOP, {}],
  ["coin", "Coin", [8, 8], [4, 4], SPIN, {}],
  ["gem", "Gem", [8, 10], [4, 5], SPIN, {}],
  ["spark", "Spark", [10, 10], [5, 5], SPIN, {}],
  ["chestOpen", "ChestOpen", [32, 32], [16, 29], OPEN, {}],
  ["letterOpen", "LetterOpen", [32, 32], [16, 29], OPEN, {}]
];

const pixelfx = process.argv[2] ? resolve(process.argv[2]) : null;
if (!pixelfx || !existsSync(join(pixelfx, "src", "core", "index.js"))) {
  console.error("usage: node loot_sheets.mjs <pixelfx checkout>");
  process.exit(1);
}

const core = await import(pathToFileURL(join(pixelfx, "src", "core", "index.js")));
const lib = await import(pathToFileURL(join(pixelfx, "tools", "lib", "fxlib.mjs")));
const kit = (await import(pathToFileURL(join(SOURCES, "..", "transitions", "_kit.js")))).default(core);
const fx = { ...core, kit };
const draws = {};
for (const name of MODULES) Object.assign(draws, (await import(pathToFileURL(join(SOURCES, `${name}.js`)))).default(fx));

function render(entry, draw, geometry) {
  const { width, height, frames, pivot } = entry;
  const frame = core.createFrame(width, height);
  const cells = [];
  for (let i = 0; i < frames; i++) {
    core.clearFrame(frame);
    const painter = core.painterFor(frame).place(pivot[0], pivot[1]);
    const u = entry.loops ? i / frames : frames > 1 ? i / (frames - 1) : 0;
    draw(painter, u, { frames, fps: entry.fps, seconds: (entry.loops ? frames : frames - 1) / entry.fps }, geometry);
    cells.push(core.composite(frame, new Uint8ClampedArray(width * height * 4), null));
  }
  return cells;
}

function strip(entry, cells) {
  const sheet = lib.canvas(entry.width * entry.frames, entry.height, [0, 0, 0, 0]);
  cells.forEach((cell, i) => {
    for (let k = 0; k < entry.width * entry.height; k++) {
      const x = i * entry.width + (k % entry.width), y = Math.floor(k / entry.width);
      sheet.data.set(cell.subarray(k * 4, k * 4 + 4), (y * sheet.w + x) * 4);
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
  const lines = readFileSync(META_TEMPLATE, "utf8").split("\n");
  lines[1] = "guid: " + randomUUID().replaceAll("-", "");
  writeFileSync(meta, lines.join("\n"));
}

function report(entry, cells) {
  let edge = 0, empty = 0;
  for (const cell of cells) {
    let filled = 0;
    for (let i = 3; i < cell.length; i += 4) if (cell[i]) filled++;
    if (!filled) empty++;
    for (let y = 0; y < entry.height; y++) for (const x of [0, entry.width - 1]) if (cell[(y * entry.width + x) * 4 + 3] === 255) edge++;
    for (let x = 0; x < entry.width; x++) if (cell[x * 4 + 3] === 255) edge++;
  }
  console.log(`${entry.file}${empty ? `  empty frames: ${empty}` : ""}${edge ? `  solid pixels on edges: ${edge}` : ""}`);
  return edge;
}

const list = { version: 1, beams: [], sheets: {} };
let written = 0, clipped = 0;
for (const [key, name, [width, height], pivot, timing, extra] of SHEETS) {
  const entry = { file: `Loot_${name}_${width}x${height}_${timing.frames}f`, width, height, frames: timing.frames, pivot, fps: timing.fps, ...extra };
  if (timing.loops) entry.loops = true;
  const cells = render(entry, draws[key], {});
  write(entry, cells);
  clipped += report(entry, cells);
  if (extra.rarity) list.beams.push(entry);
  else list.sheets[key] = entry;
  written++;
}

writeFileSync(join(OUT, "loot.json"), JSON.stringify(list, null, 2) + "\n");
console.log(`${written} sheets written${clipped ? `, ${clipped} solid edge pixels to check` : ""}`);
