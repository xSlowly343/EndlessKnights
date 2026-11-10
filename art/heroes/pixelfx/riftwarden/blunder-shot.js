import { chain } from "../../core/index.js";
import * as muzzle from "../projectiles/muzzle-shotgun.js";
import * as tracer from "../projectiles/bullet-tracer.js";
import * as impact from "../impacts/bullet-impact.js";

const FLY = 0.1;

export const meta = {
  name: "Blunder shot",
  desc: "The blunderbuss booms in a wide flash and smoke, three pellets streak out in a spread and burst on the target one after another.",
  tags: ["move", "gun", "shotgun", "projectile", "impact", "one-shot"],
  size: [400, 120],
  origin: [44, 104],
  duration: FLY + 1.6,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { seed: 5, tx: 150, height: 30 },
};

export function create(o) {
  const spread = [-6, 0, 5];
  return chain([
    { fx: muzzle, t: 0, height: o.height },
    ...spread.map((d, i) => ({ fx: tracer, t: i * 0.015, until: FLY + i * 0.015, x: (t) => 8 + clampT((t - i * 0.015) / FLY) * (o.tx - 8), y: (t) => -o.height + d * clampT((t - i * 0.015) / FLY) })),
    ...spread.map((d, i) => ({ fx: impact, t: FLY + i * 0.04, x: o.tx, height: o.height - d })),
  ])(o);
}

function clampT(u) { return Math.max(0, Math.min(1, u)); }
