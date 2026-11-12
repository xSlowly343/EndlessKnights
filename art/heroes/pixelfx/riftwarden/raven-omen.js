import { Particles, Lights, P, PF, Rng, fit, clamp, lerp, seg, ease, sparkle, WHITE } from "../../core/index.js";
import { raven, rune, plume } from "./raven-dive.js";

const END = 0.9, HIT = 0.18, FLARE = 0.5, DROP = 0.62;

export const meta = {
  name: "Raven omen",
  desc: "A raven stoops through a ring of runes and breaks on the target into a fan of black quills; runes light one by one between them, flare, and the feathers drift down.",
  tags: ["shadow", "impact", "creature", "summon", "one-shot"],
  size: [116, 76],
  origin: [74, 46],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#05060e", "#10142a", "#1e2850", "#34468a", "#6a84c8", "#b4ccf4", "#ffffff"],
    rune: ["#0c4a5e", "#2cb4d8", "#7cf0ff", "#e8ffff"],
    seed: 182,
  },
};

const QUILLS = 11, A0 = Math.PI * 0.94, A1 = Math.PI * 2.06;

export function create(o) {
  const C = fit(o.palette, 7), N = fit(o.rune, 4), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => 16, sparks: [WHITE, N[2], N[0]], haze: C[2] }), lights = new Lights();
  const fall = [];
  let t = 0, hit = false, flared = false, dropped = false;
  lights.add(0, 0, 24, N[1], 0.4, 0.2);
  const quill = (i) => {
    const f = i / (QUILLS - 1), ang = lerp(A0, A1, f), L = (i % 2 ? 19 : 25) * (0.8 + 0.2 * Math.sin(f * Math.PI));
    return [ang, Math.min(L, Math.sin(ang) < -0.3 ? 30 / -Math.sin(ang) : 99, Math.cos(ang) > 0.3 ? 25 / Math.cos(ang) : 99)];
  };
  return {
    update(dt) {
      t += dt;
      if (t < HIT && R.chance(dt * 80)) ps.spawn(P.MOTE, lerp(-32, 0, t / HIT) + R.range(-3, 3), lerp(-24, 0, t / HIT) + R.range(-3, 3), R.range(-60, -20), R.range(-50, -10), R.range(0.15, 0.3), N[2]);
      if (!hit && t >= HIT) {
        hit = true;
        lights.add(0, 0, 26, N[1], 0.5, 0.2);
        for (let i = 0; i < 22; i++) {
          const a = R.range(A0, A1), sp = R.range(80, 220);
          ps.spawn(P.SPARK, 0, 0, Math.cos(a) * sp * (Math.cos(a) > 0 ? 0.35 : 1), Math.sin(a) * sp, R.range(0.2, 0.36));
        }
        for (let i = 0; i < 8; i++) ps.spawn(P.VAPOUR, R.range(-10, 8), R.range(-8, 8), R.range(-50, 30), R.range(-40, 10), R.range(0.25, 0.4), C[3], R.range(3, 6), 0, PF.BACK);
      }
      if (!flared && t >= FLARE) {
        flared = true;
        lights.add(0, -4, 28, N[1], 0.45, 0.18);
        for (let i = 0; i < QUILLS - 1; i++) {
          const [a0, L0] = quill(i), [a1, L1] = quill(i + 1), g = (a0 + a1) / 2, rr = Math.min(L0, L1) * 0.78;
          for (let k = 0; k < 2; k++) ps.spawn(P.STAR, Math.cos(g) * rr, Math.sin(g) * rr, Math.cos(g) * R.range(10, 40), Math.sin(g) * R.range(10, 40) - 14, R.range(0.2, 0.36), N[2], 1);
        }
      }
      if (!dropped && t >= DROP) {
        dropped = true;
        for (let i = 0; i < QUILLS; i++) {
          const [ang, L] = quill(i);
          fall.push({ x: Math.cos(ang) * L * 0.5, y: Math.sin(ang) * L * 0.5, ang, L: L * 0.5, vx: Math.cos(ang) * R.range(10, 34), vy: R.range(-30, 10), ph: R.range(0, 6), life: R.range(0.2, 0.27) });
        }
      }
      for (const f of fall) { f.vy += 150 * dt; f.x += (f.vx + Math.sin(t * 9 + f.ph) * 30) * dt; f.y += f.vy * dt; f.ang += Math.cos(t * 9 + f.ph) * 4 * dt; }
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const k = ease.outCubic(seg(t, 0, 0.1)) * (1 - seg(t, HIT, HIT + 0.12));
      if (k > 0.02) {
        p.ring(0, 0, 19 * k, 1, N[1], (v, g) => (Math.sin(g * 12 + t * 10) > -0.2 ? k : 0));
        p.ring(0, 0, 14 * k, 1, N[0], k);
        for (let i = 0; i < 6; i++) { const g = i * 1.047 - t * 3; rune(p, Math.cos(g) * 16.5 * k, Math.sin(g) * 16.5 * k, i, N[2], N[3], k, 0.4); }
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      if (t < HIT + 0.02) {
        const k = ease.inQuad(seg(t, 0.03, HIT)), x = lerp(-32, -3, k), y = lerp(-24, -3, k);
        for (let i = 3; i >= 1; i--) p.line(x - i * 5, y - i * 3.6, x - i * 5 - 9, y - i * 3.6 - 6.4, i === 1 ? N[2] : N[1], 0.8 - i * 0.18);
        raven(p, x, y, 0.62, 1.15, 0.9 - 1.5 * seg(t, 0.03, 0.12), C, N, seg(t, 0.02, 0.06));
      }
      if (t >= HIT && t < DROP) {
        const open = ease.outBack(seg(t, HIT, HIT + 0.14), 1.8), flare = t >= FLARE ? 1 - seg(t, FLARE, DROP) : 0, spin = flare > 0 ? (1 - flare) * 0.22 : 0;
        p.glow(0, -4, 30, N[0], 0.5 * open);
        for (let i = 0; i < QUILLS; i++) {
          const [ang, L] = quill(i), late = clamp(open * 1.25 - Math.abs(i - 5) * 0.05);
          plume(p, Math.cos(ang) * 3, Math.sin(ang) * 3, ang + spin, L * late, 3.2 + (i % 2 ? 0 : 0.8), C, N, 1);
        }
        for (let i = 0; i < QUILLS - 1; i++) {
          const on = HIT + 0.1 + i * 0.022;
          if (t < on) continue;
          const [a0, L0] = quill(i), [a1, L1] = quill(i + 1), g = (a0 + a1) / 2 + spin, rr = Math.min(L0, L1) * 0.86 + flare * 2, pop = t - on < 0.05 || flare > 0.5;
          rune(p, Math.cos(g) * rr, Math.sin(g) * rr, i, pop ? N[3] : N[2], N[3], 1, pop ? 0.5 : 0.25);
          if (flare > 0.3) p.line(Math.cos(g) * 5, Math.sin(g) * 5, Math.cos(g) * (rr - 4), Math.sin(g) * (rr - 4), N[2], flare);
        }
        p.circle(0, 0, 3.4, C[0], 1);
        p.circle(0, 0, 2.2, flare > 0.4 ? WHITE : N[2], 1);
        p.glow(0, 0, 7, N[2], 0.4 + flare * 0.3);
      }
      if (t >= HIT && t < HIT + 0.13) {
        const f = 1 - (t - HIT) / 0.13;
        sparkle(p, 0, 0, 10 * f, N[2], WHITE, 0.25);
        p.arc(0, 0, 8 + 20 * (1 - f * f), 2, A0, A1, f > 0.5 ? WHITE : N[2], f);
      }
      if (t >= FLARE && t < FLARE + 0.1) {
        const f = 1 - (t - FLARE) / 0.1;
        p.arc(0, 0, 14 + 14 * (1 - f), 1.5, A0, A1, WHITE, f);
        sparkle(p, 0, 0, 7 * f, N[3], WHITE, 0.2);
      }
      for (const f of fall) {
        const s = t - DROP;
        if (s > f.life || f.y > 15) continue;
        plume(p, f.x - Math.cos(f.ang) * f.L * 0.8, f.y - Math.sin(f.ang) * f.L * 0.8, f.ang, f.L * 1.6, 2.8, C, N, s / f.life > 0.7 ? 0.5 : 1);
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
