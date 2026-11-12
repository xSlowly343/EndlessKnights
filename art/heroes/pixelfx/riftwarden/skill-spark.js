import { NEUTRAL, CENTRE, stage, throwSparks, sparkle, seg, clamp, WHITE } from "./_skill-kit.js";

const END = 0.2;

export const meta = {
  name: "Skill spark",
  desc: "Small show of a skill that lost its turn, neutral for tinting: one four-point glint on the target and a few sparks.",
  tags: ["skill", "impact", "small", "one-shot"],
  size: [64, 64],
  origin: [34, 46],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: NEUTRAL, seed: 25 },
};

export function create(o) {
  const S = stage(o), { C } = S;
  S.lights.add(0, CENTRE, 11, C[3], 0.4, 0.14);
  throwSparks(S, 0, CENTRE, 5, 100, 0.12, [Math.PI - 1.3, Math.PI + 1.3]);
  let t = 0;
  return {
    update(dt) { t += dt; S.ps.update(dt); S.lights.update(dt); },
    draw(p) {
      if (t >= END) return;
      const u = t / END, f = clamp(1 - seg(u, 0.4, 1)), arm = (3 + 7 * Math.sin(Math.PI * Math.min(1, u * 1.6) * 0.5)) * f;
      sparkle(p, 0, CENTRE, arm, C[5], WHITE);
      p.ring(0, CENTRE, 2 + 7 * u, 1.2, u < 0.4 ? WHITE : C[3], f);
      for (let i = 0; i < 4; i++) {
        const a = Math.PI / 4 + (i * Math.PI) / 2, d = 4 + 8 * u;
        p.dot(Math.cos(a) * d, CENTRE + Math.sin(a) * d, C[4], f);
      }
      S.ps.draw(p); S.lights.draw(p);
    },
  };
}
