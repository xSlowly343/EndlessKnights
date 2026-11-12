import { NEUTRAL, CENTRE, stage, throwSparks, slash, floorRing, pop, pulse, seg, ease, clamp, WHITE } from "./_skill-kit.js";

const END = 0.4;
const LAND = 0.07;

export const meta = {
  name: "Skill hit",
  desc: "Fast skill family, drawn in neutral tones for tinting: a streak darts in from the hero's side and lands as two crossing crescents with a ring.",
  tags: ["skill", "impact", "slash", "one-shot"],
  size: [152, 84],
  origin: [116, 68],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: NEUTRAL, seed: 21 },
};

export function create(o) {
  const S = stage(o), { C } = S;
  S.lights.add(0, CENTRE, 22, C[3], 0.5, 0.26);
  throwSparks(S, 0, CENTRE, 14, 210, 0.28, [Math.PI - 1.1, Math.PI + 1.1], LAND);
  throwSparks(S, 0, CENTRE, 5, 120, 0.2, [-0.8, 0.8], LAND);
  let t = 0;
  return {
    update(dt) { t += dt; S.ps.update(dt); S.lights.update(dt); },
    draw(p) {
      if (t >= END) return;
      const dart = seg(t, 0, LAND), trail = clamp(1 - seg(t, LAND, 0.2));
      if (trail > 0) {
        const head = -86 + 86 * ease.outQuad(dart), tail = -86 + 86 * seg(t, 0.03, 0.2);
        p.glowLine(tail, CENTRE, head, CENTRE, 4, C[3], 0.35 * trail);
        p.line(tail, CENTRE - 1, head, CENTRE - 1, C[3], (v) => trail * v);
        p.line(tail, CENTRE + 1, head, CENTRE + 1, C[2], (v) => trail * v);
        p.line(tail, CENTRE, head, CENTRE, WHITE, (v) => trail * Math.sqrt(v));
        for (let i = 0; i < 4; i++) p.line(tail - i * 6, CENTRE + (i % 2 ? 4 : -4), tail - i * 6 + 9, CENTRE + (i % 2 ? 4 : -4), C[2], trail * 0.7);
      }
      const hit = (t - LAND) / (END - LAND);
      if (hit <= 0) return void (S.ps.draw(p), S.lights.draw(p));
      slash(p, 0, CENTRE, 15, -0.45, hit, C);
      slash(p, -1, CENTRE, 13, 0.55, hit - 0.16, C);
      p.line(1, CENTRE - 19, 1, CENTRE + 13, (v) => (Math.abs(v - 0.5) < 0.25 ? WHITE : C[4]), (v) => clamp(1 - hit * 2.2) * pulse(v));
      pop(p, 0, CENTRE, 16, hit / 0.35, C);
      floorRing(p, -2, 5 + 21 * ease.outQuad(hit), clamp(1 - hit), C);
      S.ps.draw(p); S.lights.draw(p);
    },
  };
}
