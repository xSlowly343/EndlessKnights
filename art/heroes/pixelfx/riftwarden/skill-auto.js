import { NEUTRAL, CENTRE, stage, throwSparks, floorRing, pop, seg, ease, clamp, WHITE } from "./_skill-kit.js";

const END = 0.3;
const SNAP = 0.08;

export const meta = {
  name: "Skill auto",
  desc: "Autocast skill family, neutral for tinting: four chevrons close on the target like a reticle, snap shut and pop a thin ring.",
  tags: ["skill", "impact", "auto", "one-shot"],
  size: [96, 80],
  origin: [52, 62],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: NEUTRAL, seed: 23 },
};

export function create(o) {
  const S = stage(o), { C } = S;
  S.lights.add(0, CENTRE, 16, C[3], 0.4, 0.2);
  throwSparks(S, 0, CENTRE, 8, 130, 0.16, [Math.PI - 1.4, Math.PI + 1.4], SNAP);
  let t = 0;
  return {
    update(dt) { t += dt; S.ps.update(dt); S.lights.update(dt); },
    draw(p) {
      if (t >= END) return;
      const close = ease.inQuad(seg(t, 0, SNAP)), f = clamp(1 - seg(t, SNAP + 0.04, END)), d = 17 - 11 * close + 4 * seg(t, SNAP, END);
      for (let i = 0; i < 4; i++) {
        const a = Math.PI / 4 + (i * Math.PI) / 2, x = Math.cos(a) * d, y = CENTRE + Math.sin(a) * d, sx = Math.sign(Math.cos(a)), sy = Math.sign(Math.sin(a));
        const c = t < SNAP + 0.05 ? WHITE : C[4];
        p.line(x, y, x + sx * 5, y, c, f);
        p.line(x, y, x, y + sy * 5, c, f);
        p.line(x + sx, y + sy, x + sx * 4, y + sy, C[2], f * 0.8);
      }
      const hit = (t - SNAP) / (END - SNAP);
      if (hit > 0) {
        pop(p, 0, CENTRE, 12, hit / 0.4, C);
        p.ring(0, CENTRE, 4 + 12 * ease.outQuad(hit), 1.4, hit < 0.3 ? WHITE : C[3], clamp(1 - hit));
        floorRing(p, 0, 4 + 13 * ease.outQuad(hit), clamp(1 - hit) * 0.8, C);
      }
      S.ps.draw(p); S.lights.draw(p);
    },
  };
}
