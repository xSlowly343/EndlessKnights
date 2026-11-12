import { fit, clamp, rand, lerp, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Firework rocket",
  desc: "A festival rocket on its stick, drawn in place so the game moves it: striped paper tube, nose climbing then tipping over, gold sparks pouring from the tail, smoke behind.",
  tags: ["fire", "projectile", "festival", "one-shot"],
  size: [120, 56],
  origin: [98, 26],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#7a4a0c", "#c48412", "#f0b424", "#ffe066", "#fff6c4", "#ffffff"],
    paper: ["#7a1030", "#e8324a", "#fff0e0", "#32b4e8"],
    seed: 181,
  },
};

export function draw(p, t, o) {
  if (t >= FLY) return;
  const G = fit(o.palette, 6), PP = o.paper.map(hex), u = clamp(t / FLY), grow = clamp(u / 0.2), heading = lerp(-0.55, 0.5, u);
  const ca = Math.cos(heading), sa = Math.sin(heading), at = (d, s = 0) => [d * ca - s * sa, d * sa + s * ca];
  const [tx, ty] = at(-9);
  p.glow(tx, ty, 16, G[2], 0.6);
  for (let i = 0; i < 8; i++) {
    const d = (i + 1) / 8;
    if (d > grow) break;
    const x = tx - d * 62, y = ty + Math.sin(heading) * -16 * d + d * d * 18, r = 2 + d * 4.5;
    p.disc(x, y, r, hex("#8a8494"), 0.36 * (1 - d));
    p.disc(x - 1, y - 1, r * 0.6, hex("#c8c0d0"), 0.3 * (1 - d));
  }
  for (let i = 0; i < 34; i++) {
    const born = rand(o.seed, i) * 0.9, age = u - born;
    if (age < 0 || age > 0.45) continue;
    const spread = (rand(o.seed, i + 40) - 0.5) * 1.5, sp = 60 + rand(o.seed, i + 80) * 90, s = age * 0.55;
    const x = tx - Math.cos(heading + spread) * sp * s - age * 30, y = ty - Math.sin(heading + spread) * sp * s + 260 * s * s, k = 1 - age / 0.45;
    p.line(x, y, x + 2.2 * ca, y + 2.2 * sa - 1, age < 0.1 ? WHITE : age < 0.25 ? G[3] : age < 0.36 ? G[2] : G[1], k);
    if (i % 3 === 0) p.light(x, y, G[2], 0.6 * k);
    if (i % 7 === 0 && age > 0.2) { p.dot(x, y - 1, WHITE, 1); p.dot(x - 1, y, G[3], 1); p.dot(x + 1, y, G[3], 1); p.dot(x, y + 1, G[3], 1); }
  }
  const [sx, sy] = at(-26, 2.5), [bx, by] = at(-6, 2.5);
  p.line(sx, sy, bx, by, G[0], 1);
  const [ax, ay] = at(-8), [nx, ny] = at(5);
  p.beam(ax, ay, nx, ny, 7.4, PP[0], 1);
  p.beam(ax, ay, nx, ny, 5.6, (d, v) => (Math.floor(d * 5) % 2 ? PP[2] : v < 0.5 ? PP[1] : PP[0]), 1);
  const [c0x, c0y] = at(5, -3.6), [c1x, c1y] = at(5, 3.6), [c2x, c2y] = at(12.5, 0);
  p.tri(c0x, c0y, c1x, c1y, c2x, c2y, PP[3], 1);
  const [hx, hy] = at(6, -2), [h2x, h2y] = at(11, -0.4);
  p.line(hx, hy, h2x, h2y, WHITE, 1);
  const [rx, ry] = at(-1, -2.4);
  p.dot(rx, ry, WHITE, 1);
  const flick = 3.5 + Math.sin(u * 50) * 1.5, [fx, fy] = at(-9 - flick);
  p.beam(tx, ty, fx, fy, 4, (d) => (d < 0.4 ? WHITE : G[3]), 1);
  p.circle(tx, ty, 2.6, WHITE, 1);
}
