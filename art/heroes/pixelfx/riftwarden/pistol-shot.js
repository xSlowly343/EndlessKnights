import { chain } from "../../core/index.js";
import * as muzzle from "../projectiles/muzzle-pistol.js";
import * as tracer from "../projectiles/bullet-tracer.js";
import * as impact from "../impacts/bullet-impact.js";

const FLY = 0.12;

export const meta = {
  name: "Pistol shot",
  desc: "The pistol cracks with a spiky flash and a curl of smoke, a tracer streaks across the strip in a blink and the round bursts on the target in sparks and grit.",
  tags: ["move", "pistol", "projectile", "impact", "one-shot"],
  size: [380, 110],
  origin: [44, 96],
  duration: FLY + 1.4,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { seed: 4, tx: 150, height: 30 },
};

export function create(o) {
  return chain([
    { fx: muzzle, t: 0, height: o.height },
    { fx: tracer, t: 0, until: FLY, x: (t) => 8 + (t / FLY) * (o.tx - 8), y: -o.height },
    { fx: impact, t: FLY, x: o.tx - 116 + 116, height: o.height },
  ])(o);
}
