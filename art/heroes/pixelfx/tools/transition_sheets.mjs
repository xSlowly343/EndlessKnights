import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { randomUUID } from "node:crypto";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..", "..", "..");
const SOURCES = resolve(HERE, "..", "transitions");
const RESOURCES = join(REPO, "Assets", "Game", "Art", "Resources");
const OUT = join(RESOURCES, "Transitions");
const META_TEMPLATE = join(RESOURCES, "Heroes", "Seeker", "Seeker_Void_Crescent_87x42_8f.png.meta");
const PARTS = { portalOpen: "open", portalIdle: "idle", portalClose: "close", stepIn: "stepIn", arrive: "arrive", allyOut: "allyOut", allyIn: "allyIn", loading: "loading" };
const DARK = [0x1b, 0x22, 0x36, 255];
const LIGHT = [0xc4, 0xd6, 0xea, 255];

const args = process.argv.slice(2);
const flags = { only: [] };
let pixelfx = null;
for (let i = 0; i < args.length; i++) {
  if (args[i] === "--preview") flags.preview = resolve(args[++i]);
  else if (args[i] === "--only") flags.only.push(args[++i].toLowerCase());
  else if (args[i] === "--gif") flags.gif = true;
  else pixelfx = resolve(args[i]);
}
if (!pixelfx || !existsSync(join(pixelfx, "src", "core", "index.js"))) {
  console.error("usage: node transition_sheets.mjs <pixelfx checkout> [--only <place>]... [--preview <dir>] [--gif]");
  process.exit(1);
}

const core = await import(pathToFileURL(join(pixelfx, "src", "core", "index.js")));
const lib = await import(pathToFileURL(join(pixelfx, "tools", "lib", "fxlib.mjs")));
const kit = (await import(pathToFileURL(join(SOURCES, "_kit.js")))).default(core);
const fx = { ...core, kit };
const list = JSON.parse(readFileSync(join(OUT, "transitions.json"), "utf8"));

function moduleName(file) {
  const [group, place] = file.split("_");
  return group === "RiftSky" ? "rift-sky" : place.toLowerCase();
}

async function load(name) {
  const path = join(SOURCES, `${name}.js`);
  return existsSync(path) ? (await import(pathToFileURL(path))).default(fx) : null;
}

function render(entry, draw) {
  const { width, height, frames, pivot } = entry;
  const frame = core.createFrame(width, height);
  const cells = [];
  for (let i = 0; i < frames; i++) {
    core.clearFrame(frame);
    const painter = core.painterFor(frame).place(pivot[0], pivot[1]);
    const u = entry.loops ? i / frames : i / (frames - 1);
    draw(painter, u, { frames, fps: entry.fps, seconds: (entry.loops ? frames : frames - 1) / entry.fps });
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

function writeMeta(png) {
  const meta = png + ".meta";
  if (existsSync(meta)) return;
  const lines = readFileSync(META_TEMPLATE, "utf8").split("\n");
  lines[1] = "guid: " + randomUUID().replaceAll("-", "");
  writeFileSync(meta, lines.join("\n"));
}

function same(a, b) {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) if (a[i] !== b[i]) return false;
  return true;
}

function covered(cell) {
  let n = 0;
  for (let i = 3; i < cell.length; i += 4) if (cell[i]) n++;
  return n;
}

function touchesSides(entry, cells) {
  let n = 0;
  for (const cell of cells)
    for (let y = 0; y < entry.height; y++) {
      if (cell[(y * entry.width) * 4 + 3] === 255) n++;
      if (cell[(y * entry.width + entry.width - 1) * 4 + 3] === 255) n++;
    }
  return n;
}

function preview(name, rows) {
  const pad = 6, label = 12;
  const width = Math.max(...rows.map((r) => pad + r.cells.length * (r.entry.width * r.scale + pad)));
  const height = rows.reduce((h, r) => h + label + r.entry.height * r.scale + pad, pad);
  const sheet = lib.canvas(width * 2, height, DARK);
  sheet.rect(width, 0, width, height, LIGHT);
  let y = pad;
  for (const r of rows) {
    const w = r.entry.width * r.scale, h = r.entry.height * r.scale;
    sheet.text(`${r.part} x${r.scale}`.toUpperCase(), pad, y, 1, [150, 160, 190]);
    sheet.text(`${r.part} x${r.scale}`.toUpperCase(), width + pad, y, 1, [60, 70, 100]);
    r.cells.forEach((cell, i) => {
      const big = lib.upscale(cell, r.entry.width, r.entry.height, r.scale);
      sheet.paste(big, w, h, pad + i * (w + pad), y + label);
      sheet.paste(big, w, h, width + pad + i * (w + pad), y + label);
    });
    y += label + h + pad;
  }
  lib.save(join(flags.preview, `${name}.png`), sheet.png());
  if (!flags.gif) return;
  for (const [tone, fill] of [["dark", DARK], ["light", LIGHT]])
    for (const r of rows) {
      const frames = r.cells.map((cell) => {
        const one = lib.canvas(r.entry.width, r.entry.height, fill);
        one.paste(cell, r.entry.width, r.entry.height, 0, 0);
        return { color: one.data };
      });
      lib.writeVideo(join(flags.preview, "gif", `${name}-${r.part}-${tone}.gif`), { w: r.entry.width, h: r.entry.height, frames, fps: r.entry.fps }, r.scale, { loops: r.entry.loops ? 3 : 1 });
    }
}

const groups = [...Object.values(list.places), list.riftSky];
let failed = 0, written = 0;
for (const group of groups) {
  const entries = Object.entries(group).filter(([, v]) => typeof v === "object");
  const name = moduleName(entries[0][1].file);
  if (flags.only.length && !flags.only.includes(name)) continue;
  const mod = await load(name);
  if (!mod) {
    console.log(`${name}: no source yet`);
    continue;
  }
  const rows = [], done = {};
  for (const [key, entry] of entries) {
    const part = PARTS[key] ?? key;
    if (!mod[part]) {
      console.error(`${name}.${part}: missing`);
      failed++;
      continue;
    }
    const cells = render(entry, mod[part]);
    done[part] = cells;
    mkdirSync(OUT, { recursive: true });
    const png = join(OUT, `${entry.file}.png`);
    writeFileSync(png, strip(entry, cells));
    writeMeta(png);
    written++;
    const last = covered(cells[cells.length - 1]), sides = touchesSides(entry, cells);
    console.log(`${entry.file}  last frame ${last} px${sides ? `  solid pixels on side edges: ${sides}` : ""}`);
    rows.push({ part, entry, cells, scale: part.startsWith("ally") ? 2 : 3 });
  }
  if (done.open && done.idle && !same(done.open[done.open.length - 1], done.idle[0])) {
    console.error(`${name}: last open frame differs from first idle frame`);
    failed++;
  }
  if (flags.preview) preview(name, rows);
}
console.log(`${written} sheets written`);
process.exit(failed ? 1 : 0);
