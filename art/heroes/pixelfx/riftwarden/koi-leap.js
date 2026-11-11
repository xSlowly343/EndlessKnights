import { fit, clamp, rand, lerp, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Koi leap",
  desc: "A spirit koi leaping in an arc, drawn in place so the game moves it: nose up at the start and down at the end, tail beating, water streaming off its back.",
  tags: ["water", "projectile", "creature", "one-shot"],
  size: [112, 64],
  origin: [88, 32],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0e2e5a", "#1c76bc", "#36a2e0", "#78ccf4", "#c4ecff", "#ffffff"],
    scales: ["#7a2408", "#e8641a", "#ffa63c", "#fff4e0"],
    seed: 101,
  },
};

export function koi(p, x, y, heading, wag, len, K, W, a = 1) {
  const spine = [];
  let px = x, py = y, ang = heading + Math.PI;
  for (let i = 0; i <= 12; i++) {
    const s = i / 12;
    spine.push([px, py, s]);
    ang += wag * s * 0.22;
    px += Math.cos(ang) * (len / 12);
    py += Math.sin(ang) * (len / 12);
  }
  const width = (s) => 1 + 3.6 * Math.pow(Math.sin(Math.PI * clamp(0.14 + 0.8 * s)), 0.9) * (1 - s * 0.35);
  const [tx, ty] = spine[12], ta = ang;
  for (const side of [-0.75, 0.75]) {
    const fx = tx + Math.cos(ta + side) * 7, fy = ty + Math.sin(ta + side) * 7;
    p.tri(tx - Math.cos(ta) * 2, ty - Math.sin(ta) * 2, fx, fy, tx + Math.cos(ta) * 4, ty + Math.sin(ta) * 4, K[1], a);
    p.line(tx, ty, fx, fy, K[2], a);
  }
  for (const [sx, sy, s] of spine) p.circle(sx, sy, width(s) + 1, K[0], a);
  const nx = Math.cos(heading + Math.PI / 2), ny = Math.sin(heading + Math.PI / 2), back = ny < 0 ? 1 : -1;
  p.tri(spine[4][0], spine[4][1], spine[7][0], spine[7][1], spine[6][0] + nx * back * 7, spine[6][1] + ny * back * 7, K[1], a);
  for (const [sx, sy, s] of spine) {
    const patch = (s > 0.12 && s < 0.34) || (s > 0.52 && s < 0.74);
    p.circle(sx, sy, width(s), patch ? K[1] : K[3], a);
    if (patch) p.circle(sx + nx * back, sy + ny * back, width(s) * 0.6, K[2], a);
    else p.dot(sx + nx * back * width(s) * 0.5, sy + ny * back * width(s) * 0.5, WHITE, a);
  }
  p.dot(spine[1][0] + nx * back * 1.5, spine[1][1] + ny * back * 1.5, W[0], a);
  p.dot(x + Math.cos(heading), y + Math.sin(heading), K[2], a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const W = fit(o.palette, 6), K = o.scales.map(hex), u = clamp(t / FLY), grow = clamp(u / 0.2);
  const heading = lerp(-0.75, 0.75, u), wag = Math.sin(u * 15) * 0.45;
  p.glow(-8, 0, 20, W[1], 0.3);
  for (let i = 0; i < 16; i++) {
    const d = (i + 1) / 16;
    if (d > grow) break;
    const x = -14 - d * 58, y = Math.sin(heading) * -14 * d + d * d * 16 + Math.sin(d * 9 + u * 10) * 2;
    p.dot(x, y, d < 0.3 ? W[4] : d < 0.65 ? W[3] : W[2], 1 - d * 0.7);
    if (i % 2 === 0) p.dot(x, y + 1, W[2], 0.8 - d * 0.6);
    if (i % 4 === 0) p.light(x, y, W[3], 0.4);
  }
  for (let i = 0; i < 9; i++) {
    const born = rand(o.seed, i) * 0.75, age = u - born;
    if (age < 0 || age > 0.35) continue;
    const x = -6 - rand(o.seed, i + 10) * 14 - age * 90, y = (rand(o.seed, i + 20) - 0.5) * 10 + age * age * 110;
    p.dot(x, y, age < 0.15 ? WHITE : W[3], 1 - age / 0.35);
    p.dot(x, y - 1, W[2], 0.6 - age);
  }
  koi(p, 4, 0, heading, wag, 22, K, W);
}
