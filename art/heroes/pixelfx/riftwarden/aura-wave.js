import { NEUTRAL, stage, floorRing, seg, ease, clamp, WHITE } from "./_skill-kit.js";
import { P } from "../../core/index.js";

const END = 0.5;
const REACH = 126;
const FLAT = 0.065;

export const meta = {
  name: "Aura wave",
  desc: "An aura switching on, neutral for tinting: a flat ring leaves the hero's feet and rolls along the ground across the strip, lifting short wisps as it goes.",
  tags: ["skill", "aura", "wave", "ground", "one-shot"],
  size: [288, 48],
  origin: [144, 32],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: NEUTRAL, seed: 26 },
};

export function create(o) {
  const S = stage(o), { C, R } = S;
  S.lights.add(0, -4, 18, C[3], 0.4, 0.2);
  for (let i = 0; i < 16; i++) {
    const at = R.range(0.05, 0.75), side = i % 4 === 0 ? -1 : 1, x = side * REACH * ease.outQuad(at) * R.range(0.9, 1);
    S.ps.spawn(P.MOTE, x, -1, side * R.range(10, 40), R.range(-70, -30), R.range(0.12, 0.2), C[4], 1, at * END * 0.8);
  }
  let t = 0;
  return {
    update(dt) { t += dt; S.ps.update(dt); S.lights.update(dt); },
    draw(p) {
      if (t >= END) return;
      const u = t / END, r = 8 + REACH * ease.outQuad(u), f = clamp(1 - seg(u, 0.55, 1));
      floorRing(p, 0, r, f, C, FLAT);
      floorRing(p, 0, r * 0.82, f * 0.7, C, FLAT);
      floorRing(p, 0, r * 0.6, f * 0.4, C, FLAT);
      for (const side of [-1, 1]) {
        const x = side * r;
        p.line(x, 0, x, -5 * f - 2, WHITE, f);
        p.line(x - side * 2, 0, x - side * 2, -3 * f - 1, C[4], f);
        p.light(x, -2, C[3], 0.5 * f);
      }
      S.ps.draw(p); S.lights.draw(p);
    },
  };
}
