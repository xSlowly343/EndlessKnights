import { P, B, Bits } from "../../core/index.js";
import { NEUTRAL, CENTRE, stage, throwSparks, floorRing, pop, pulse, seg, bump, ease, clamp, sparkle, WHITE } from "./_skill-kit.js";

const END = 0.76;
const SLAM = 0.18;
const TOP = -47;

export const meta = {
  name: "Skill strong",
  desc: "Heavy skill family, neutral for tinting: light gathers over the target, a column slams to the ground, a wide shock ring rolls out, rubble and dust settle.",
  tags: ["skill", "impact", "heavy", "one-shot"],
  size: [128, 92],
  origin: [78, 72],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: NEUTRAL, seed: 24 },
};

export function create(o) {
  const S = stage(o), { C, R } = S, bits = new Bits({ ground: () => 0 });
  for (let i = 0; i < 10; i++) {
    const a = -Math.PI / 2 + R.range(-1.3, 1.3), d = R.range(22, 34);
    S.ps.spawn(P.STREAK, Math.cos(a) * d * 1.3, CENTRE - 8 + Math.sin(a) * d * 0.8, -Math.cos(a) * 150, -Math.sin(a) * 110, SLAM - 0.03, C[4], 1, R.range(0, 0.06));
  }
  throwSparks(S, 0, -2, 20, 260, 0.4, [-Math.PI + 0.2, -0.75], SLAM);
  for (let i = 0; i < 7; i++) S.ps.spawn(P.DUST, R.range(-20, 14), -2, R.range(-50, 30), R.range(-26, -6), R.range(0.3, 0.5), C[2], R.range(3, 6), SLAM + R.range(0, 0.08));
  let t = 0, slammed = false;
  return {
    update(dt) {
      t += dt;
      if (!slammed && t >= SLAM) {
        slammed = true;
        S.lights.add(0, -8, 30, C[4], 0.6, 0.35);
        for (let i = 0; i < 7; i++) bits.add({ kind: B.CHUNK, x: R.range(-8, 6), y: -2, vx: R.range(-120, 60), vy: R.range(-210, -110), a: R.range(0, 6), va: R.range(-9, 9), s: R.range(1.6, 3), c: C[i % 2 ? 3 : 2], bounce: 0.3, life: R.range(0.3, 0.48) });
      }
      S.ps.update(dt); bits.update(dt); S.lights.update(dt);
    },
    draw(p) {
      if (t >= END) return;
      const charge = seg(t, 0, SLAM);
      if (t < SLAM) {
        p.glow(0, CENTRE - 10, 6 + 10 * charge, C[3], 0.3 * charge);
        sparkle(p, 0, TOP + 8, 3 + 5 * charge, C[5], WHITE);
        floorRing(p, 0, 20 - 10 * charge, 0.5 + 0.5 * charge, C, 0.22);
      } else {
        const hit = (t - SLAM) / (END - SLAM), column = clamp(1 - seg(hit, 0.05, 0.4)), w = 9 * column;
        if (column > 0) {
          p.glowLine(0, TOP, 0, 0, 9, C[3], 0.5 * column);
          p.rect(-w / 2 - 1, TOP, w + 2, -TOP, (x) => C[3], (x, y) => column * (0.35 + 0.65 * clamp((y - TOP) / 24)));
          p.rect(-w / 2 + 1, TOP, Math.max(1, w - 2), -TOP, hit < 0.12 ? WHITE : C[5], (x, y) => column * clamp((y - TOP) / 16));
          p.line(-w / 2 - 3, TOP + 6, -w / 2 - 3, 0, C[2], (v) => column * v);
          p.line(w / 2 + 3, TOP + 10, w / 2 + 3, 0, C[2], (v) => column * v);
        }
        pop(p, 0, -3, 24, hit / 0.3, C);
        const roll = ease.outCubic(hit);
        floorRing(p, -3, 6 + 22 * roll, clamp(1.2 - hit * 1.2), C, 0.26);
        floorRing(p, -3, 3 + 15 * roll, clamp(1 - hit * 1.6), C, 0.26);
        for (let i = 0; i < 5; i++) {
          const x = -22 + i * 10, h = (i === 2 ? 15 : 9 + (i % 2) * 3) * bump(hit, 0, 0.45 + (i % 2) * 0.1);
          if (h > 1.5) {
            p.tri(x - 3, 0, x + 3, 0, x + (i - 2), -h, C[2]);
            p.line(x - 2, -1, x + (i - 2), -h, C[4], 1);
          }
        }
      }
      S.ps.draw(p); bits.draw(p); S.lights.draw(p);
    },
  };
}
