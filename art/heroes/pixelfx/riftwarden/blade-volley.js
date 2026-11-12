import { fit, clamp, rand, hex, sparkle, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Blade volley",
  desc: "Three spectral swords thrown point first, drawn in place so the game moves them: rolling on their axes, edges catching light, cut-lines of air behind.",
  tags: ["steel", "projectile", "one-shot"],
  size: [120, 48],
  origin: [98, 24],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#141a2c", "#3a4a6e", "#6e86b4", "#a8c0e4", "#dcecff", "#ffffff"],
    hilt: ["#7a4a0c", "#f0b424", "#fff0a0"],
    seed: 141,
  },
};

export function sword(p, tx, ty, ang, len, S, G, o = {}) {
  const a = o.a ?? 1, roll = o.roll ?? 1, ca = Math.cos(ang), sa = Math.sin(ang), nx = -sa, ny = ca;
  const hx = tx - ca * len, hy = ty - sa * len, w = 1.9 * (0.35 + 0.65 * Math.abs(roll));
  const sx = tx - ca * 4, sy = ty - sa * 4;
  if (!o.bare) p.poly([[tx + ca * 1.5, ty + sa * 1.5], [sx + nx * (w + 1.2), sy + ny * (w + 1.2)], [hx + nx * (w + 1.2), hy + ny * (w + 1.2)], [hx - nx * (w + 1.2), hy - ny * (w + 1.2)], [sx - nx * (w + 1.2), sy - ny * (w + 1.2)]], S[0], a);
  p.poly([[tx, ty], [sx + nx * w, sy + ny * w], [hx + nx * w, hy + ny * w], [hx, hy]], o.white ? WHITE : roll > 0 ? S[4] : S[2], a);
  p.poly([[tx, ty], [sx - nx * w, sy - ny * w], [hx - nx * w, hy - ny * w], [hx, hy]], o.white ? WHITE : roll > 0 ? S[2] : S[3], a);
  p.line(hx, hy, tx, ty, o.white ? WHITE : S[5], a);
  const gw = 3.6;
  p.beam(hx + nx * gw, hy + ny * gw, hx - nx * gw, hy - ny * gw, 2.4, o.white ? WHITE : (u) => (u < 0.3 ? G[2] : G[1]), a);
  p.beam(hx - ca * 1, hy - sa * 1, hx - ca * 5, hy - sa * 5, 2, o.white ? WHITE : G[0], a);
  p.circle(hx - ca * 6, hy - sa * 6, 1.4, o.white ? WHITE : G[1], a);
  p.dot(hx, hy, o.white ? WHITE : G[2], a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const S = fit(o.palette, 6), G = o.hilt.map(hex), u = clamp(t / FLY), grow = clamp(u / 0.2);
  const blades = [[-12, -11, 0.07, 1.9], [-15, 11, -0.07, 3.6], [8, 0, 0, 0]];
  p.glow(-2, 0, 22, S[2], 0.35);
  for (const [x, y, tilt, ph] of blades) {
    for (let k = 0; k < 3; k++) {
      const yy = y + (k - 1) * 2.5, L = (44 - k * 7 + (ph ? 0 : 12)) * grow;
      for (let d = 0; d < L; d += 1) {
        if ((Math.floor(d + u * 60 + k * 3) % 7) < 2) continue;
        const f = d / L;
        p.dot(x - 22 - d, yy + tilt * -d, f < 0.25 ? S[4] : f < 0.6 ? S[3] : S[2], (1 - f) * (k === 1 ? 1 : 0.6));
      }
    }
  }
  for (const [x, y, tilt, ph] of blades) {
    const roll = Math.cos(u * 14 + ph), wob = Math.sin(u * 20 + ph) * 0.8;
    p.glowLine(x - 20, y + wob, x, y + wob, 5, S[3], 0.35);
    sword(p, x, y + wob, tilt, ph ? 17 : 21, S, G, { roll });
    if (Math.abs(roll) > 0.8) sparkle(p, x - 6 - (1 - Math.abs(roll)) * 30, y + wob, 3.5, S[4], WHITE, 0.5);
    p.dot(x + 1, y + wob, WHITE, 1);
  }
  for (let i = 0; i < 8; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.3) continue;
    const x = -6 - rand(o.seed, i + 10) * 30 - age * 80, y = (rand(o.seed, i + 20) - 0.5) * 30;
    p.dot(x, y, age < 0.12 ? WHITE : S[3], 1 - age / 0.3);
    p.dot(x + 1, y, S[2], 0.6 - age);
  }
}
