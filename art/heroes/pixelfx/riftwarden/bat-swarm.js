import { Particles, Lights, P, PF, Rng, fit, clamp, seg, ease, hex, sparkle, WHITE } from "../../core/index.js";
import { bat } from "./bat-flight.js";

const END = 0.9, OPEN = 0.12, CLOSE = 0.56, CX = -2, CY = -3;

export const meta = {
  name: "Bat swarm",
  desc: "A black moon opens behind the target and a colony pours out of it, wheels round in a tightening whirl that rakes three red cuts, then streams away overhead as the moon shuts.",
  tags: ["shadow", "impact", "creature", "summon", "one-shot"],
  size: [112, 76],
  origin: [70, 44],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0a0614", "#1c1030", "#3a1c68", "#6b2cc0", "#9a48ec", "#c684ff", "#ecc8ff"],
    eye: "#ff3a4a",
    seed: 174,
  },
};

const CUTS = [[0.22, -0.5], [0.3, 0.55], [0.38, -0.05]];

export function create(o) {
  const C = fit(o.palette, 7), eye = hex(o.eye), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => 16, sparks: [WHITE, hex("#ff8a9a"), eye], haze: C[2] }), lights = new Lights();
  const bats = [];
  for (let i = 0; i < 9; i++) bats.push({ born: 0.06 + i * 0.03, a0: R.range(0, 6.3), w: R.range(13, 17) * (i % 2 ? 1 : -1), r: R.range(13, 22), tilt: R.range(0.55, 0.9), s: R.range(0.8, 1.15), ph: R.range(0, 6), leave: CLOSE - 0.1 + i * 0.022, vx: R.range(-90, -10), vy: R.range(-150, -100) });
  let t = 0, cut = 0;
  lights.add(CX, CY, 30, C[3], 0.6, 0.25);
  const where = (b, time) => {
    const k = seg(time, b.born, b.born + 0.12), ang = b.a0 + b.w * (time - b.born), r = b.r * ease.outCubic(k) * (1 - 0.35 * seg(time, 0.2, 0.42));
    return [CX + 2 + Math.cos(ang) * r * 1.1, CY + 2 + Math.sin(ang) * r * b.tilt, Math.sin(ang) * b.w < 0 ? 1 : -1];
  };
  return {
    update(dt) {
      t += dt;
      if (cut < CUTS.length && t >= CUTS[cut][0]) {
        const ang = CUTS[cut][1];
        lights.add(2, 0, 30, eye, 0.8, 0.16);
        for (let i = 0; i < 9; i++) {
          const dir = i % 2 ? 1 : -1, sp = R.range(90, 220) * (dir > 0 ? 0.6 : 1);
          ps.spawn(P.SPARK, 2, 0, dir * Math.cos(ang) * sp + R.range(-30, 30), dir * Math.sin(ang) * sp + R.range(-40, 20), R.range(0.18, 0.36));
        }
        cut++;
      }
      if (t > 0.1 && t < CLOSE && R.chance(dt * 50)) ps.spawn(P.VAPOUR, CX + R.range(-16, 14), CY + R.range(-14, 14), R.range(-20, 20), R.range(-30, 0), R.range(0.25, 0.4), C[2], R.range(3, 6), 0, PF.BACK);
      if (t > CLOSE - 0.06 && t < END - 0.4 && R.chance(dt * 60)) ps.spawn(P.EMBER, R.range(-24, 12), R.range(-26, 4), R.range(-20, 10), R.range(10, 40), R.range(0.2, 0.34), C[4], R.range(1, 2));
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const k = ease.outBack(seg(t, 0, OPEN), 1.5) * (1 - ease.inCubic(seg(t, CLOSE, CLOSE + 0.14)));
      if (k > 0.02) {
        const r = 18 * k;
        p.glow(CX, CY, r + 14, C[3], 0.55 * k);
        p.circle(CX, CY, r + 1.5, C[4], 1);
        p.circle(CX, CY, r, (d, g) => (d > 0.9 ? C[2] : d > 0.7 + 0.08 * Math.sin(g * 5 + t * 14) ? C[1] : C[0]), 1);
        p.arc(CX, CY, r + 1.5, 1.5, -2.6, -0.9, C[6], 1);
        p.arc(CX + 3, CY - 1, r * 0.8, (u) => 3 * Math.sin(u * Math.PI) * k, 0.9, 2.5, C[2], 1);
        for (let i = 0; i < 10; i++) {
          const g = i * 0.628 + t * 3, rr = r + 4.5 + (i % 2) * 2;
          if (Math.sin(g) * rr + CY < -32) continue;
          p.dot(CX + Math.cos(g) * rr, CY + Math.sin(g) * rr, i % 2 ? C[5] : C[4], k);
          if (i % 2) p.line(CX + Math.cos(g) * (rr + 1), CY + Math.sin(g) * (rr + 1), CX + Math.cos(g + 0.14) * (rr + 3), CY + Math.sin(g + 0.14) * (rr + 3), C[4], k);
        }
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      for (const [at, ang] of CUTS) {
        const f = 1 - (t - at) / 0.13;
        if (f <= 0 || f > 1) continue;
        const ca = Math.cos(ang), sa = Math.sin(ang), L0 = -30 * (1.15 - f * 0.15), L1 = 24;
        p.glowLine(ca * L0, sa * L0, ca * L1, sa * L1, 7, eye, 0.7 * f);
        p.beam(ca * L0, sa * L0, ca * L1, sa * L1, 1 + 3.4 * f * f, (u, v) => (v < 0.45 ? WHITE : eye), (u) => Math.sin(u * Math.PI) * f);
        sparkle(p, 2, 0, 9 * f, eye, WHITE);
      }
      for (const b of bats) {
        if (t < b.born) continue;
        let x, y, dir, a = Math.min(1, (t - b.born) / 0.05);
        if (t < b.leave) [x, y, dir] = where(b, t);
        else {
          const s = t - b.leave, [lx, ly] = where(b, b.leave);
          x = lx + b.vx * s + Math.sin(s * 22 + b.ph) * 3; y = ly + b.vy * s * (0.5 + s * 2.2); dir = -1;
          a = clamp((y + 36) / 10) * (1 - seg(s, 0.12, 0.24));
        }
        if (a <= 0.02 || y < -33) continue;
        bat(p, x, y, b.s, Math.sin(t * 46 + b.ph), dir, C, eye, a);
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
