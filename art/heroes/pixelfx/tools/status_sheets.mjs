import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { randomUUID } from "node:crypto";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..", "..", "..");
const SOURCES = resolve(HERE, "..", "statuses");
const RESOURCES = join(REPO, "Assets", "Game", "Art", "Resources");
const OUT = join(RESOURCES, "Statuses");
const META_TEMPLATE = join(RESOURCES, "Heroes", "Seeker", "Seeker_Void_Crescent_87x42_8f.png.meta");
const MODULES = ["body", "marks", "elements", "icons"];
const SIZES = { small: { s: 1, tag: "S" }, large: { s: 2, tag: "L" } };
const BODY = 32;
const LOOP = { frames: 8, fps: 12, loops: true };
const FLASH = { frames: 6, fps: 20 };
const SHEETS = [
  ["poison1", "Poison1", [32, 32], [16, 31], "body", LOOP, true],
  ["poison2", "Poison2", [32, 32], [16, 31], "body", LOOP, true],
  ["poison3", "Poison3", [32, 32], [16, 31], "body", LOOP, true],
  ["burnBody", "BurnBody", [32, 32], [16, 0], "top", LOOP, true],
  ["ember", "Ember", [32, 32], [16, 16], "chest", LOOP, true],
  ["fracture1", "Fracture1", [32, 32], [16, 16], "chest", LOOP, true],
  ["fracture2", "Fracture2", [32, 32], [16, 16], "chest", LOOP, true],
  ["fracture3", "Fracture3", [32, 32], [16, 16], "chest", LOOP, true],
  ["fracture4", "Fracture4", [32, 32], [16, 16], "chest", LOOP, true],
  ["corrosion", "Corrosion", [32, 32], [16, 16], "chest", FLASH, true],
  ["burnTop1", "BurnTop1", [32, 20], [16, 16], "head", LOOP, false],
  ["burnTop2", "BurnTop2", [32, 20], [16, 16], "head", LOOP, false],
  ["burnTop3", "BurnTop3", [32, 20], [16, 16], "head", LOOP, false],
  ["stun", "Stun", [28, 14], [14, 13], "head", LOOP, false],
  ["blind", "Blind", [24, 10], [12, 5], "eyes", LOOP, false],
  ["curse", "Curse", [16, 18], [8, 17], "head", { frames: 12, fps: 12, loops: true }, false],
  ["markOn", "MarkOn", [24, 24], [12, 12], "chest", FLASH, false],
  ["mark", "Mark", [24, 24], [12, 12], "chest", LOOP, false],
  ["earth1", "Earth1", [40, 14], [20, 11], "feet", LOOP, false],
  ["earth2", "Earth2", [40, 14], [20, 11], "feet", LOOP, false],
  ["earth3", "Earth3", [40, 14], [20, 11], "feet", LOOP, false],
  ["water1", "Water1", [40, 24], [20, 21], "feet", LOOP, false],
  ["water2", "Water2", [40, 24], [20, 21], "feet", LOOP, false],
  ["lightning", "Lightning", [32, 40], [16, 38], "feet", FLASH, false],
  ["air", "Air", [48, 32], [12, 16], "chest", FLASH, false],
  ["fractureChip", "FractureChip", [32, 32], [16, 16], "chest", FLASH, false]
];

const pixelfx = process.argv[2] ? resolve(process.argv[2]) : null;
if (!pixelfx || !existsSync(join(pixelfx, "src", "core", "index.js"))) {
  console.error("usage: node status_sheets.mjs <pixelfx checkout>");
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
    if (entry.masked) continue;
    for (let y = 0; y < entry.height; y++) for (const x of [0, entry.width - 1]) if (cell[(y * entry.width + x) * 4 + 3] === 255) edge++;
    for (let x = 0; x < entry.width; x++) if (cell[x * 4 + 3] === 255) edge++;
  }
  console.log(`${entry.file}${empty ? `  empty frames: ${empty}` : ""}${edge ? `  solid pixels on edges: ${edge}` : ""}`);
  return edge;
}

const list = { version: 1, sizes: {} };
let written = 0, clipped = 0;
for (const [size, { s, tag }] of Object.entries(SIZES)) {
  const sheets = {};
  for (const [key, name, [w, h], [px, py], anchor, timing, masked] of SHEETS) {
    const width = w * s, height = h * s;
    const entry = { file: `Status_${name}_${tag}_${width}x${height}_${timing.frames}f`, width, height, frames: timing.frames, pivot: [px * s, py === 0 ? 0 : py * s + (s - 1)], fps: timing.fps, anchor };
    if (timing.loops) entry.loops = true;
    if (masked) entry.masked = true;
    const cells = render(entry, draws[key], { s, w: BODY * s, h: BODY * s });
    write(entry, cells);
    clipped += report(entry, cells);
    sheets[key] = entry;
    written++;
  }
  list.sizes[size] = { body: BODY * s, sheets };
}

const order = draws.icons.order;
const icons = { file: `Status_Icons_9x9_${order.length}f`, width: 9, height: 9, frames: order.length, pivot: [4, 4], fps: 1, order };
write(icons, render(icons, draws.icons, { s: 1, w: 9, h: 9 }));
list.icons = icons;
written++;

writeFileSync(join(OUT, "statuses.json"), JSON.stringify(list, null, 2) + "\n");
console.log(`${written} sheets written${clipped ? `, ${clipped} solid edge pixels to check` : ""}`);
