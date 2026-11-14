import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { randomUUID } from "node:crypto";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..", "..", "..");
const SOURCES = resolve(HERE, "..", "milestones");
const RESOURCES = join(REPO, "Assets", "Game", "Art", "Resources");
const OUT = join(RESOURCES, "Milestones");
const META_TEMPLATE = join(RESOURCES, "Heroes", "Seeker", "Seeker_Void_Crescent_87x42_8f.png.meta");
const MODULES = ["hero", "artifact", "rift", "badges"];
const FPS = 15;
const GROUPS = 4;
const MEDIUM = [40, 50];
const SMALL = [24, 24];
const AT_FEET = [20, 46];
const OVERHEAD = [20, 49];
const AHEAD = [6, 47];
const GATE = [3, 47];
const CENTRE = [12, 12];
const SHEETS = [
  ["levelUp", "LevelUp", MEDIUM, AT_FEET, 12, "medium"],
  ["prestige", "Prestige", [160, 60], [28, 50], 15, "large"],
  ...[...Array(GROUPS).keys()].map((g) => [`artifactOpen${g}`, `ArtifactOpen${g + 1}`, MEDIUM, OVERHEAD, 14, "medium"]),
  ...[...Array(GROUPS).keys()].map((g) => [`artifactAwaken${g}`, `ArtifactAwaken${g + 1}`, MEDIUM, OVERHEAD, 11, "medium"]),
  ...[...Array(GROUPS).keys()].map((g) => [`artifactEnchant${g}`, `ArtifactEnchant${g + 1}`, MEDIUM, OVERHEAD, 11, "medium"]),
  ["riftStep", "RiftStep", MEDIUM, AHEAD, 12, "medium"],
  ["featureOpen", "FeatureOpen", MEDIUM, AHEAD, 12, "medium"],
  ["giftOffer", "GiftOffer", MEDIUM, OVERHEAD, 9, "medium"],
  ["giftChosen", "GiftChosen", MEDIUM, AT_FEET, 9, "medium"],
  ["gate", "Gate", MEDIUM, GATE, 12, "medium"],
  ["achievement", "Achievement", SMALL, CENTRE, 12, "small"],
  ["collection", "Collection", SMALL, CENTRE, 12, "small"],
  ["campTier", "CampTier", SMALL, CENTRE, 12, "small"],
  ["quest", "Quest", SMALL, CENTRE, 9, "small"],
  ["trial", "Trial", SMALL, CENTRE, 9, "small"]
];

const pixelfx = process.argv[2] ? resolve(process.argv[2]) : null;
if (!pixelfx || !existsSync(join(pixelfx, "src", "core", "index.js"))) {
  console.error("usage: node milestone_sheets.mjs <pixelfx checkout>");
  process.exit(1);
}

const core = await import(pathToFileURL(join(pixelfx, "src", "core", "index.js")));
const lib = await import(pathToFileURL(join(pixelfx, "tools", "lib", "fxlib.mjs")));
const kit = (await import(pathToFileURL(join(SOURCES, "..", "transitions", "_kit.js")))).default(core);
const fx = { ...core, kit };
const draws = {};
for (const name of MODULES) Object.assign(draws, (await import(pathToFileURL(join(SOURCES, `${name}.js`)))).default(fx));

function render(entry, draw) {
  const { width, height, frames, pivot } = entry;
  const frame = core.createFrame(width, height);
  const cells = [];
  for (let i = 0; i < frames; i++) {
    core.clearFrame(frame);
    const painter = core.painterFor(frame).place(pivot[0], pivot[1]);
    draw(painter, i / (frames - 1), { frames, fps: entry.fps, seconds: (frames - 1) / entry.fps });
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
  let edge = 0, empty = 0, same = 0, cover = 0;
  cells.forEach((cell, n) => {
    let filled = 0, solid = 0;
    for (let i = 3; i < cell.length; i += 4) {
      if (cell[i]) filled++;
      if (cell[i] > 200) solid++;
    }
    if (!filled) empty++;
    cover = Math.max(cover, solid / (entry.width * entry.height));
    if (n && cell.every((value, i) => value === cells[n - 1][i])) same++;
    for (let y = 0; y < entry.height; y++) for (const x of [0, entry.width - 1]) if (cell[(y * entry.width + x) * 4 + 3] === 255) edge++;
    for (let x = 0; x < entry.width; x++) for (const y of [0, entry.height - 1]) if (cell[(y * entry.width + x) * 4 + 3] === 255) edge++;
  });
  console.log(`${entry.file}  ${(entry.frames / entry.fps).toFixed(2)}s  cover ${(cover * 100).toFixed(0)}%${empty ? `  empty frames: ${empty}` : ""}${same ? `  repeated frames: ${same}` : ""}${edge ? `  solid pixels on edges: ${edge}` : ""}`);
  return edge;
}

const list = { version: 1, sheets: {} };
let written = 0, clipped = 0;
for (const [key, name, [width, height], pivot, frames, size] of SHEETS) {
  const entry = { file: `Milestone_${name}_${width}x${height}_${frames}f`, width, height, frames, pivot, fps: FPS, size };
  const cells = render(entry, draws[key]);
  write(entry, cells);
  clipped += report(entry, cells);
  list.sheets[key] = entry;
  written++;
}

writeFileSync(join(OUT, "milestones.json"), JSON.stringify(list, null, 2) + "\n");
console.log(`${written} sheets written${clipped ? `, ${clipped} solid edge pixels to check` : ""}`);
