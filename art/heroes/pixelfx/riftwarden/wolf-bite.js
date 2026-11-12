import { Particles, Lights, P, PF, Rng, fit, clamp, seg, ease, hex, sparkle, WHITE } from "../../core/index.js";
import { wolfHead } from "./wolf-lunge.js";

const END = 0.86, SNAP = 0.15;

export const meta = {
  name: "Wolf bite",
  desc: "Spectral jaws open round the target and snap shut on a line of white; a cold moon swells behind and the wolf howls itself away against it into mist and motes.",
  tags: ["spirit", "impact", "creature", "one-shot"],
  size: [116, 78],
  origin: [64, 46],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#061422", "#0e3a5a", "#1c7a9c", "#4cc4d8", "#a4f0f0", "#e8ffff", "#ffffff"],
    eye: "#ffe066",
    seed: 152,
  },
};

function jawRow(p, side, gap, W, a) {
  const gum = (x) => side * (gap + 5 + (1 - (x / 19) * (x / 19)) * 4);
  for (const pass of [0, 1, 2]) {
    let px = -19, py = gum(-19);
    for (let x = -17; x <= 19; x += 2) {
      const y = gum(x);
      if (pass === 0) p.beam(px, py + side * 2, x, y + side * 2, 7.5, W[1], a);
      else if (pass === 1) p.beam(px, py + side * 2, x, y + side * 2, 5.5, (u, v) => (v < 0.4 ? W[3] : W[2]), a);
      else p.line(px, py + side * 4.4, x, y + side * 4.4, W[4], a);
      px = x; py = y;
    }
  }
  for (let i = 0; i < 7; i++) {
    const x = (i - 3) * 5.4, base = gum(x), big = i === 1 || i === 5, L = (big ? 10 : i === 3 ? 4.5 : 6) * -side, hw = big ? 2.5 : 1.8;
    p.tri(x - hw - 1.2, base, x + hw + 1.2, base, x, base + L - side * 1.4, W[1], a);
    p.tri(x - hw, base, x + hw, base, x, base + L, WHITE, a);
    p.line(x + hw * 0.5, base, x, base + L, W[4], a);
  }
}

export function create(o) {
  const W = fit(o.palette, 7), eye = hex(o.eye), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => 16, sparks: [WHITE, W[4], W[2]], haze: W[2] }), lights = new Lights();
  let t = 0, snapped = false;
  lights.add(0, 0, 26, W[3], 0.4, 0.2);
  return {
    update(dt) {
      t += dt;
      if (!snapped && t >= SNAP) {
        snapped = true;
        lights.add(0, 0, 40, W[4], 1, 0.24);
        for (let i = 0; i < 22; i++) {
          const dir = i % 2 ? 1 : -1, a = R.range(-0.5, 0.5), sp = R.range(90, 240) * (dir > 0 ? 0.7 : 1);
          ps.spawn(P.SPARK, 0, 0, dir * Math.cos(a) * sp, Math.sin(a) * sp - 30, R.range(0.2, 0.42));
        }
        for (let i = 0; i < 8; i++) ps.spawn(P.VAPOUR, R.range(-14, 12), R.range(-8, 8), R.range(-60, 40), R.range(-20, 10), R.range(0.3, 0.5), W[3], R.range(4, 7), 0, PF.BACK);
      }
      if (t > 0.3 && t < 0.66 && R.chance(dt * 46)) ps.spawn(P.EMBER, R.range(-20, 14), R.range(-10, 12), R.range(-14, 14), R.range(-60, -24), R.range(0.25, 0.45), 0, R.range(1, 2));
      if (t > 0.5 && t < 0.7 && R.chance(dt * 30)) ps.spawn(P.VAPOUR, R.range(-12, 6), R.range(-22, 2), R.range(-20, 20), R.range(-30, -8), R.range(0.2, 0.3), W[4], R.range(3, 6));
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const moon = ease.outBack(seg(t, SNAP, SNAP + 0.14), 1.4) * (1 - seg(t, 0.6, 0.8));
      if (moon > 0.02) {
        const r = 17 * moon, mx = -3, my = -14;
        p.glow(mx, my, r + 14, W[3], 0.5 * moon);
        p.circle(mx, my, r + 1, W[3], Math.min(1, moon * 2.5));
        p.circle(mx, my, r, (d, g) => (Math.cos(g - 0.6) * d > 0.5 ? W[4] : W[5]), Math.min(1, moon * 2.5));
        p.circle(mx + 6, my - 5, r * 0.16, W[4], Math.min(1, moon * 2.5));
        p.circle(mx - 7, my - 6, r * 0.2, W[4], Math.min(1, moon * 2.5));
        p.circle(mx + 9, my + 5, r * 0.12, W[4], Math.min(1, moon * 2.5));
        p.ring(mx, my, r + 4 + seg(t, SNAP, 0.6) * 10, 1, W[3], (v, g) => (Math.sin(g * 9 + t * 6) > 0 ? 0.6 * moon * (1 - seg(t, 0.3, 0.6)) : 0));
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      if (t < 0.3) {
        const openK = ease.outBack(seg(t, 0, 0.09), 2), gap = t < SNAP - 0.02 ? 4 + 9 * openK + 2 * seg(t, 0.09, SNAP - 0.02) : 15 - 21 * ease.inQuad(seg(t, SNAP - 0.02, SNAP + 0.012));
        const a = t < 0.06 ? seg(t, 0, 0.06) : 1 - seg(t, 0.2, 0.3);
        p.glow(0, 0, 30, W[2], 0.4 * a);
        jawRow(p, -1, gap, W, a);
        jawRow(p, 1, gap, W, a);
        
      }
      if (t >= SNAP && t < SNAP + 0.16) {
        const f = 1 - (t - SNAP) / 0.16;
        p.glowLine(-30, 0, 26, 0, 8, W[4], 0.8 * f);
        p.beam(-34 * (1.2 - f * 0.2), 0, 28, 0, 1 + 4 * f * f, (u, v) => (v < 0.5 ? WHITE : W[4]), (u) => Math.sin(u * Math.PI) * f);
        sparkle(p, 0, 0, 16 * f, W[4], WHITE);
        for (const side of [-1, 1]) p.arc(0, 0, 10 + 20 * (1 - f * f), 2, side < 0 ? Math.PI * 0.75 : -Math.PI * 0.25, side < 0 ? Math.PI * 1.25 : Math.PI * 0.25, f > 0.5 ? WHITE : W[4], f);
      }
      if (t > 0.26 && t < 0.72) {
        const k = seg(t, 0.26, 0.4), up = ease.outCubic(k), fade = 1 - seg(t, 0.56, 0.72), howl = seg(t, 0.36, 0.5);
        const hx = -5, hy = 2 - 12 * up, ang = -0.2 - 0.85 * ease.inOutQuad(seg(t, 0.3, 0.46));
        for (let i = 0; i < 6; i++) {
          const y = hy + 4 + i * 3, wv = Math.sin(i * 1.3 + t * 9) * 2;
          p.disc(hx - 6 + wv, y + 6, 7 - i * 0.6, i < 2 ? W[1] : W[2], 0.6 * fade * up * (1 - i / 7));
        }
        wolfHead(p, hx, hy, ang, 1.2, howl * (0.55 + 0.1 * Math.sin(t * 40)), W, eye, fade * Math.min(1, k * 2), true);
        if (howl > 0 && fade > 0.3) for (let i = 0; i < 3; i++) {
          const s = (t * 3.2 + i / 3) % 1, r = 12 + s * 14, c = ang - 0.02;
          p.arc(hx + Math.cos(c) * 6, hy + Math.sin(c) * 6, r, 1, c - 0.42, c + 0.42, s < 0.4 ? WHITE : W[4], (1 - s) * fade * howl);
        }
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
