import { Particles, Lights, P, Rng, fit, sparkle, flare, band, clamp, seg, bump, ease, WHITE } from "../../core/index.js";

export const NEUTRAL = ["#34343e", "#6c6c7a", "#a4a4b2", "#d0d0da", "#ececf2", "#ffffff"];
export const CENTRE = -14;

export const pulse = (v) => Math.sin(Math.PI * v);

export function stage(o, sparks) {
  const C = fit(o.palette, 6), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[4], C[3], C[2]], haze: C[2] }), lights = new Lights();
  return { C, R, ps, lights, sparks };
}

export function throwSparks(S, x, y, count, speed, life, cone, delay = 0) {
  for (let i = 0; i < count; i++) {
    const a = cone[0] + S.R.range(0, cone[1] - cone[0]), sp = S.R.range(speed * 0.4, speed);
    S.ps.spawn(P.SPARK, x + S.R.range(-2, 2), y + S.R.range(-2, 2), Math.cos(a) * sp, Math.sin(a) * sp - 20, S.R.range(life * 0.5, life), undefined, undefined, delay);
  }
}

export function slash(p, x, y, r, tilt, u, C) {
  const f = clamp(1 - seg(u, 0.35, 1)), open = ease.outQuart(seg(u, 0, 0.3));
  if (f <= 0) return;
  p.arc(x - r + 2 + open * 4, y, r + open * 2, (v) => 1 + 4 * f * pulse(v), tilt - 1.2, tilt + 1.2, (v) => (Math.abs(v - 0.5) < 0.22 * f ? WHITE : Math.abs(v - 0.5) < 0.34 ? C[4] : C[3]), 0.3 + 0.7 * f);
}

export function floorRing(p, x, r, k, C, flat = 0.2) {
  band(p, x, 0, r, flat, 2, k, [C[1], C[2], C[3], C[5]]);
}

export function pop(p, x, y, size, u, C) {
  if (u >= 1) return;
  flare(p, x, y, size * (1 - u), 0.6, [C[2], C[3], C[5], WHITE]);
  sparkle(p, x, y, size * 0.6 * (1 - u), C[5], WHITE);
}

export { seg, bump, ease, clamp, sparkle, WHITE };
