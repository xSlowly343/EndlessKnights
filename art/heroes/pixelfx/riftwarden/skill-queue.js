import { NEUTRAL, CENTRE, stage, throwSparks, slash, floorRing, pop, pulse, seg, ease, clamp, WHITE } from "./_skill-kit.js";

const END = 0.5;
const STEP = 0.11;
const HITS = [[0, 14], [8, 11], [15, 9]];

export const meta = {
  name: "Skill queue",
  desc: "Queue skill family, neutral for tinting: one line runs through the target and the two behind it, cutting each in turn, front to back.",
  tags: ["skill", "impact", "chain", "one-shot"],
  size: [124, 84],
  origin: [78, 68],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: NEUTRAL, seed: 22 },
};

export function create(o) {
  const S = stage(o), { C } = S;
  HITS.forEach(([x], i) => {
    throwSparks(S, x, CENTRE, 6, 150, 0.2, [-Math.PI / 2 - 1.2, -Math.PI / 2 + 0.5], i * STEP);
  });
  let t = 0, lit = 0;
  return {
    update(dt) {
      t += dt;
      while (lit < HITS.length && t >= lit * STEP) S.lights.add(HITS[lit++][0], CENTRE, 18, C[3], 0.45, 0.2);
      S.ps.update(dt); S.lights.update(dt);
    },
    draw(p) {
      if (t >= END) return;
      const run = seg(t, 0, STEP * 2 + 0.04), gone = clamp(1 - seg(t, STEP * 2 + 0.06, END - 0.08)), head = -60 + 80 * ease.outQuad(run);
      if (gone > 0) {
        p.glowLine(-60, CENTRE, head, CENTRE, 3, C[3], 0.3 * gone);
        p.line(-60, CENTRE, head, CENTRE, (v) => (v > 0.8 ? WHITE : C[3]), (v) => gone * (0.25 + 0.75 * v));
        p.line(-60, CENTRE + 2, head - 4, CENTRE + 2, C[2], (v) => gone * v * 0.8);
      }
      HITS.forEach(([x, r], i) => {
        const u = (t - i * STEP) / 0.26;
        if (u <= 0 || u >= 1) return;
        slash(p, x, CENTRE, r, i % 2 ? 0.5 : -0.5, u, C);
        p.line(x + 1, CENTRE - r - 3, x + 1, CENTRE + r, (v) => (Math.abs(v - 0.5) < 0.25 ? WHITE : C[4]), (v) => clamp(1 - u * 2) * pulse(v));
        pop(p, x, CENTRE, 11 - i * 2, u / 0.4, C);
        floorRing(p, x - 2, 3 + (10 - i * 3) * ease.outQuad(u), clamp(1 - u), C);
      });
      S.ps.draw(p); S.lights.draw(p);
    },
  };
}
