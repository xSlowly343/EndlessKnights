import { fit, clamp, rand, sparkle, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Glacier lance",
  desc: "A lance of blue ice, drawn in place so the game moves it: faceted and rimmed with light, two splinters circling it, a wake of frost breath and snowflakes.",
  tags: ["ice", "projectile", "one-shot"],
  size: [120, 44],
  origin: [98, 22],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0c1c3a", "#1c4a80", "#3a86c8", "#72c0ee", "#b4e6ff", "#e8f8ff", "#ffffff"],
    seed: 121,
  },
};

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), u = clamp(t / FLY), grow = clamp(u / 0.2);
  p.glow(0, 0, 18, C[3], 0.4);
  for (let i = 0; i < 9; i++) {
    const d = (i + 1) / 9;
    if (d > grow) break;
    const x = -14 - d * 62, y = Math.sin(d * 6 + u * 8 + i) * 3;
    p.disc(x, y, 3 + d * 5, i % 2 ? C[3] : C[4], 0.34 * (1 - d));
  }
  for (let k = -1; k <= 1; k += 2)
    for (let x = -12; x > -12 - 56 * grow; x -= 1) {
      const d = (-12 - x) / 56;
      if ((Math.floor(x + u * 40) & 3) === 0) continue;
      p.dot(x, k * (2.5 + d * 4), d < 0.3 ? C[5] : d < 0.6 ? C[4] : C[3], 1 - d);
    }
  for (let i = 0; i < 10; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.4) continue;
    const x = -10 - rand(o.seed, i + 10) * 34 - age * 60, y = (rand(o.seed, i + 20) - 0.5) * 22 + age * 10, k = 1 - age / 0.4;
    if (i % 3 === 0) sparkle(p, x, y, 2.4 * k, C[4], WHITE, 0.4);
    else {
      p.dot(x, y, WHITE, k);
      p.dot(x - 1, y, C[4], k * 0.8);
      p.dot(x, y - 1, C[4], k * 0.8);
    }
  }
  const tip = [15, 0], top = [1, -4.6], tail = [-20, 0], bot = [1, 4.6], notch = [-9, 0];
  p.poly([[17, 0], [1, -6], [-23, 0], [1, 6]], C[0], 1);
  p.poly([tip, top, notch], C[5], 1);
  p.poly([top, tail, notch], C[3], 1);
  p.poly([tip, bot, notch], C[2], 1);
  p.poly([bot, tail, notch], C[1], 1);
  p.line(tail[0], 0, tip[0], 0, WHITE, (s) => 0.5 + 0.5 * s);
  p.line(top[0], top[1], tip[0], tip[1], C[6], 1);
  p.line(top[0], top[1], notch[0], 0, C[4], 1);
  p.line(bot[0], bot[1], notch[0], 0, C[3], 1);
  const g = -18 + ((u * 2.2) % 1) * 34;
  p.line(g, -3, g + 3, 3, WHITE, 0.9);
  sparkle(p, 15, 0, 4 + Math.sin(u * 30) * 1.5, C[5], WHITE, 0.6);
  for (let i = 0; i < 2; i++) {
    const a = u * 9 + i * Math.PI, x = -4 + Math.cos(a) * 9, y = Math.sin(a) * 8, front = Math.cos(a + 1.2) > 0;
    p.poly([[x + 4, y], [x, y - 1.6], [x - 4, y], [x, y + 1.6]], front ? C[5] : C[2], 1);
    p.dot(x + 3, y, WHITE, front ? 1 : 0.4);
  }
}
