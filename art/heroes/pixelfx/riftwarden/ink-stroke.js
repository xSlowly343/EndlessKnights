import { fit, clamp, rand, noise, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Ink stroke",
  desc: "One stroke of a loaded brush, drawn in place so the game moves it: a wet black head rimmed with moonlight, a tail splitting into dry bristle lines, flung drops, two red petals.",
  tags: ["ink", "projectile", "one-shot"],
  size: [124, 54],
  origin: [102, 27],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#06060c", "#1a1a2a", "#3c3c58", "#8a8ab0", "#d8d8f0", "#ffffff"],
    seal: ["#6a0c14", "#d8322a", "#ff8a6a"],
    seed: 161,
  },
};

export function draw(p, t, o) {
  if (t >= FLY) return;
  const K = fit(o.palette, 6), S = o.seal.map(hex), u = clamp(t / FLY), grow = clamp(u / 0.2), L = 78 * grow;
  const yAt = (d) => Math.sin(d * 3.4 - u * 5) * 5 * d - d * d * 3, wAt = (d) => 9 * Math.pow(1 - d, 0.8) + 1;
  p.glow(-6, 0, 20, K[4], 0.35);
  p.glowLine(-L * 0.7, yAt(0.7), 0, 0, 9, K[3], 0.3);
  for (let i = 0; i <= 60; i += 3) {
    const d = i / 60, x = 6 - d * L, y = yAt(d), w = wAt(d);
    p.disc(x, y, w / 2 + 4.5 - d * 2, K[4], 0.3 * (1 - d * 0.7));
  }
  for (let i = 0; i <= 60; i++) {
    const d = i / 60, x = 6 - d * L, y = yAt(d), w = wAt(d);
    if (d < 0.55) p.circle(x, y, w / 2 + 1, K[3], 1);
  }
  for (let i = 0; i <= 60; i++) {
    const d = i / 60, x = 6 - d * L, y = yAt(d), w = wAt(d);
    if (d < 0.55) {
      p.circle(x, y, w / 2, K[0], 1);
      p.dot(x, y - w / 2 + 1, K[2], 1);
    } else {
      for (let b = -2; b <= 2; b++) {
        const gap = noise(d * 22 + b * 7.3, o.seed + b) < 0.25 + (d - 0.55) * 1.3;
        if (gap) continue;
        const by = y + b * w * 0.34 * (1 + (d - 0.55) * 2.4);
        p.dot(x, by, K[0], 1);
        p.dot(x, by - 1, b === -2 || b === 0 ? K[4] : K[3], 1 - (d - 0.55) * 1.6);
      }
    }
  }
  p.disc(8, 0, 10, K[4], 0.4);
  p.circle(7, 0, 5.6, K[4], 1);
  p.circle(7, 0, 4.6, K[0], 1);
  p.arc(7, 0, 4.6, 1.2, Math.PI * 1.05, Math.PI * 1.75, K[3], 1);
  p.dot(5, -2.4, WHITE, 1);
  p.dot(6, -3, WHITE, 1);
  p.tri(10, -3, 16, 0, 10, 3, K[0], 1);
  p.line(10, -3.5, 16.5, 0, K[4], 1);
  p.line(16.5, 0, 10, 3.5, K[3], 1);
  for (let i = 0; i < 9; i++) {
    const born = rand(o.seed, i) * 0.7, age = u - born;
    if (age < 0 || age > 0.4) continue;
    const x0 = -rand(o.seed, i + 10) * 30, side = i % 2 ? 1 : -1, s = age * FLY * 4;
    const x = x0 - age * 60, y = yAt(-x0 / 78) + side * (3 + age * 22) + 60 * s * s, r = 1.6 - age * 2;
    p.circle(x, y, r + 1, K[4], 1 - age / 0.4);
    p.circle(x, y, Math.max(0.6, r), K[0], 1);
  }
  for (let i = 0; i < 2; i++) {
    const a = u * 9 + i * 2.6, x = -18 - i * 16 - u * 10, y = Math.sin(a) * 9 - 4;
    p.ellipse(x, y, 2.6, 1.2 + Math.abs(Math.cos(a)) * 1.2, S[1], grow);
    p.dot(x - 1, y, S[2], grow);
    p.light(x, y, S[1], 0.5 * grow);
  }
}
