import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Golem rubble",
  desc: "Six stones of a golem tumble through the air in loose formation, drawn in place so the game moves them: rune-lit, tied to each other by threads of amber light, shedding dust.",
  tags: ["earth", "projectile", "summon", "one-shot"],
  size: [116, 46],
  origin: [96, 24],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#16131a", "#2c2830", "#454048", "#625a5e", "#857a76", "#a89c90", "#d0c6b4"],
    rune: ["#5e3a0c", "#e8a218", "#ffd23c", "#fff08a"],
    seed: 175,
  },
};

export function rock(p, x, y, r, rot, C, id, a = 1, lit = 0) {
  const pts = [], n = 7;
  for (let i = 0; i < n; i++) {
    const g = rot + (i / n) * Math.PI * 2, rr = r * (0.72 + 0.36 * rand(id, i));
    pts.push([x + Math.cos(g) * rr, y + Math.sin(g) * rr * 0.9]);
  }
  p.poly(pts.map(([px, py]) => [x + (px - x) * (1 + 1.3 / r), y + (py - y) * (1 + 1.3 / r)]), C[0], a);
  p.poly(pts, (px, py) => {
    const d = ((px - x) * -0.6 + (py - y) * -0.8) / r, e = Math.sin((px - x) * 1.3 + rot * 2 + id) * 0.14;
    return lit > 0.5 ? WHITE : d + e > 0.38 ? C[6] : d + e > 0.05 ? C[5] : d + e > -0.3 ? C[4] : d + e > -0.6 ? C[3] : C[2];
  }, a);
}

export function block(p, pts, cx, cy, C, a = 1, hot = 0) {
  const span = Math.max(...pts.map(([x, y]) => Math.hypot(x - cx, y - cy)));
  p.poly(pts.map(([x, y]) => [x + Math.sign(x - cx) * 1.1, y + Math.sign(y - cy) * 1.1]), C[0], a);
  p.poly(pts, (x, y) => {
    const d = ((x - cx) * -0.55 + (y - cy) * -0.85) / span + Math.sin(x * 1.7 + y * 2.3) * 0.08;
    return hot > 0.5 ? C[6] : d > 0.45 ? C[6] : d > 0.12 ? C[5] : d > -0.25 ? C[4] : d > -0.55 ? C[3] : C[2];
  }, a);
}

export function glyph(p, x, y, s, kind, c, hot, a = 1) {
  const G = [[[-1, -1, 1, -1], [1, -1, 0, 1], [0, 1, -1, 0]], [[-1, -1, -1, 1], [-1, 0, 1, -1], [-1, 0, 1, 1]], [[0, -1, 0, 1], [-1, -0.3, 1, -0.3], [-1, 1, 0, 0.3]], [[-1, -1, 1, 1], [1, -1, -1, 1], [0, -1, 0, -0.2]]][kind % 4];
  for (const [x0, y0, x1, y1] of G) p.line(x + x0 * s, y + y0 * s, x + x1 * s, y + y1 * s, c, a);
  p.dot(x, y, hot, a);
  p.glow(x, y, s * 2 + 2, c, 0.3 * a);
}

const STONES = [[0, 0, 7.5], [-19, -8, 5.5], [-22, 9, 5], [-39, -1, 6], [-55, -10, 4], [-58, 8, 4.5]];

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), N = fit(o.rune, 4), u = clamp(t / FLY);
  const at = STONES.map(([x, y, r], i) => [x + Math.sin(u * 6.3 + i * 2.2) * 2, y + Math.cos(u * 6.3 + i * 1.7) * 2.4, r]);
  for (let i = 0; i < 12; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.45) continue;
    const x = -10 - rand(o.seed, i + 10) * 56 - age * 50, y = (rand(o.seed, i + 20) - 0.5) * 26 + age * 14;
    p.disc(x, y, 1.5 + age * 7, C[4], 0.42 * (1 - age / 0.45));
  }
  for (let i = 1; i < at.length; i++) {
    const j = i < 3 ? 0 : i - 2, [ax, ay] = at[i], [bx, by] = at[j];
    p.glowLine(ax, ay, bx, by, 3, N[1], 0.22);
    p.line(ax, ay, bx, by, (v) => (Math.sin(v * 14 - u * 30 + i) > 0.2 ? N[3] : N[1]), (v) => 0.5 + 0.5 * Math.sin(v * Math.PI));
  }
  for (let i = at.length - 1; i >= 0; i--) {
    const [x, y, r] = at[i], spin = u * (i % 2 ? 5 : -4) + i;
    for (let k = 3; k >= 1; k--) p.circle(x - k * 4, y, r * (1 - k * 0.16), C[2], 0.3 - k * 0.07);
    rock(p, x, y, r, spin, C, o.seed + i);
    if (r > 4.4) glyph(p, x - 0.5, y, r > 7 ? 2.4 : 1.6, i, N[2], N[3]);
  }
  for (let i = 0; i < 8; i++) {
    const born = rand(o.seed, i + 50) * 0.8, age = u - born;
    if (age < 0 || age > 0.3) continue;
    const x = -6 - rand(o.seed, i + 60) * 60 - age * 80, y = (rand(o.seed, i + 70) - 0.5) * 28 + age * age * 90;
    p.dot(x, y, i % 3 ? C[5] : N[3], 1 - age / 0.3);
    p.dot(x - 1, y, C[3], 0.6 * (1 - age / 0.3));
  }
}
