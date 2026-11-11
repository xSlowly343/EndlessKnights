import { fit, clamp, rand, hex } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Bramble run",
  desc: "A bramble racing along the ground, drawn in place so the game moves it: a hooked shoot leads, thorns punch up behind it and sink back, soil jumps.",
  tags: ["nature", "projectile", "ground", "one-shot"],
  size: [112, 48],
  origin: [88, 38],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0c1a0e", "#163418", "#225224", "#357530", "#4f9a3a", "#7cc048", "#b4e070"],
    soil: ["#33281c", "#6e5a40", "#b89f78"],
    seed: 81,
  },
};

export function thorn(p, x, y, h, lean, w, C, a = 1) {
  if (h < 1.5) return;
  p.tri(x - w - 1, y, x + w + 1, y, x + lean, y - h - 1, C[0], a);
  p.tri(x - w, y, x + w, y, x + lean, y - h, C[3], a);
  p.tri(x - w, y, x, y, x + lean, y - h, C[4], a);
  p.line(x - w, y, x + lean, y - h, C[6], a);
  p.dot(x + lean, y - h, C[6], a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), S = o.soil.map(hex), u = clamp(t / FLY), grow = clamp(u / 0.2), len = 18 + 54 * grow;
  const stem = (x) => -2 - Math.sin(x * 0.3 + u * 5) * 1.6;
  for (let x = -len; x <= 0; x += 1) {
    const k = 1 + x / len;
    p.dot(x, stem(x) + 1, C[1], 0.3 + 0.7 * k);
    p.dot(x, stem(x), k > 0.5 ? C[4] : C[2], 0.3 + 0.7 * k);
    if (k > 0.6) p.dot(x, stem(x) - 1, C[3], 1);
  }
  const step = 8, shift = (u * 46) % step;
  for (let x = -shift - 4; x > -len; x -= step) {
    const d = -x / len, n = Math.round((x - u * 46) / step), up = clamp(d / 0.12) * Math.pow(1 - d, 0.8);
    thorn(p, x, stem(x), 3 + 13 * up, (n % 2 ? 2 : -1.5) + 2 * up, 2 + up, C);
    if (n % 2 === 0) p.ellipse(x + 3, stem(x) - 2, 2, 1.2, C[5], clamp(1.4 - d * 1.4));
  }
  p.glow(0, -6, 12, C[4], 0.3);
  p.tri(-9, -1, -5, -6, 5, -9, C[0], 1);
  p.tri(-8, -2, 4, -4, 9, -11, C[0], 1);
  p.tri(-7, -2, 2, -5, 7, -10, C[4], 1);
  p.tri(-7, -3, -3, -6, 7, -10, C[5], 1);
  p.line(-5, -5, 7, -10, C[6], 1);
  p.arc(6, -6, 4.5, 1.6, -1.7, 1.2, C[4], 1);
  p.dot(9, -4, C[6], 1);
  for (let i = 0; i < 9; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.3) continue;
    const s = age / 0.3, x = 2 - rand(o.seed, i + 10) * 16 - s * 16, y = -1 - Math.sin(s * Math.PI) * (4 + rand(o.seed, i + 20) * 7);
    p.dot(x, y, S[i % 3], 1);
  }
}
