import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Rift crack",
  desc: "A crack in the air running at its target, drawn in place so the game moves it: a black split with a hot magenta edge, a wedge of dark at its tip, slivers falling out of it.",
  tags: ["shadow", "projectile", "rift", "one-shot"],
  size: [112, 56],
  origin: [92, 28],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#07030f", "#1c0b38", "#47148a", "#8a2be0", "#d24cf0", "#ff9af4", "#ffffff"],
    star: "#8ef0e0",
    seed: 91,
  },
};

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), u = clamp(t / FLY), grow = clamp(u / 0.2), len = 20 + 56 * grow;
  const pts = [[4, 0]];
  for (let i = 1, x = 0; x > -len; i++) {
    x -= 5 + rand(o.seed, i) * 6;
    pts.push([Math.max(x, -len), (i % 2 ? -1 : 1) * (2 + rand(o.seed, i + 20) * 5) * Math.min(1, -x / 14)]);
  }
  for (let i = 1; i < pts.length; i++) {
    const [ax, ay] = pts[i - 1], [bx, by] = pts[i], k = 1 + bx / len, w = 0.6 + 2.4 * k;
    p.glowLine(ax, ay, bx, by, 5, C[3], 0.4 * k);
    p.line(ax, ay - w, bx, by - w * 0.8, k > 0.6 ? C[5] : C[4], 0.3 + 0.7 * k);
    p.line(ax, ay + w, bx, by + w * 0.8, C[3], 0.3 + 0.7 * k);
    p.beam(ax, ay, bx, by, Math.max(1, w * 1.4), C[0], 1);
  }
  for (let i = 0; i < 5; i++) {
    const x = -4 - rand(o.seed, i + 40) * (len - 8), q = pts.find((v) => v[0] <= x) || pts[pts.length - 1];
    if (Math.sin(u * 20 + i * 2) > 0.2) p.dot(x, q[1] * 0.5, hex(o.star), 1);
  }
  p.glow(5, 0, 10, C[4], 0.5);
  p.poly([[11, 0], [3, -6], [-1, 0], [3, 6]], C[5], 1);
  p.poly([[9, 0], [3, -4.5], [0.5, 0], [3, 4.5]], C[0], 1);
  p.line(3, -6, 11, 0, WHITE, 1);
  p.dot(11, 0, WHITE, 1);
  p.dot(4, -1, hex(o.star), 1);
  for (let i = 0; i < 7; i++) {
    const born = rand(o.seed, i + 60) * 0.7, age = u - born;
    if (age < 0 || age > 0.4) continue;
    const x = -6 - rand(o.seed, i + 70) * (len - 10), y = (rand(o.seed, i + 80) - 0.5) * 6 + age * age * 60, a = age * 12 + i;
    p.tri(x, y - 2, x + 1.5 + Math.cos(a), y + 1, x - 1.5, y + 1 + Math.sin(a), age < 0.15 ? C[5] : C[3], 1 - age / 0.4);
  }
}
