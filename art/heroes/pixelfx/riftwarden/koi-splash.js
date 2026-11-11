import { fit, clamp, rand, seg, ease, hex, WHITE } from "../../core/index.js";

const END = 0.6;

export const meta = {
  name: "Koi splash",
  desc: "The koi dives into its target: a crown of water stands up and folds over, the tail flicks once on the way down, ripples run out flat and drops patter back.",
  tags: ["water", "impact", "creature", "one-shot"],
  size: [112, 88],
  origin: [56, 60],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0e2e5a", "#1c76bc", "#36a2e0", "#78ccf4", "#c4ecff", "#ffffff"],
    scales: ["#7a2408", "#e8641a", "#ffa63c", "#fff4e0"],
    seed: 102,
  },
};

export function draw(p, t, o) {
  if (t >= END) return;
  const W = fit(o.palette, 6), K = o.scales.map(hex);
  const glow = clamp(1 - t / 0.35);
  if (glow > 0) p.glow(0, -4, 24, W[2], 0.4 * glow);
  for (let i = 0; i < 3; i++) {
    const s = t - i * 0.07;
    if (s < 0 || s > 0.4) continue;
    const k = s / 0.4, rx = 5 + 28 * ease.outCubic(k), ry = rx * 0.24;
    for (let g = 0; g < 40; g++) {
      const a = (g / 40) * 6.283, far = Math.sin(a) < 0;
      p.dot(Math.cos(a) * rx, 4 + Math.sin(a) * ry, far ? W[2] : k < 0.3 ? WHITE : W[4], (1 - k) * (far ? 0.6 : 1));
    }
  }
  const crown = Math.sin(Math.PI * seg(t, 0, 0.38)), lean = seg(t, 0.05, 0.4);
  if (crown > 0.02) {
    for (let i = 0; i < 9; i++) {
      const side = (i - 4) / 4, bx = side * 9, H = (18 + rand(o.seed, i) * 14) * (1 - Math.abs(side) * 0.45) * crown;
      const tipx = bx + side * (8 + 14 * lean), wd = 3.2 - Math.abs(side);
      let px = bx, py = 3;
      for (let k = 1; k <= 8; k++) {
        const s = k / 8, x = lerp2(bx, tipx, s * s), y = 3 - H * Math.sin(s * Math.PI * 0.5) + lean * lean * 10 * s * s;
        p.beam(px, py, x, y, Math.max(1, wd * (1 - s * 0.8)), s > 0.8 ? WHITE : s > 0.45 ? W[4] : i % 2 ? W[2] : W[3], 1);
        px = x; py = y;
      }
      p.dot(px, py - 1, WHITE, 1);
      p.light(px, py, W[4], 0.5 * crown);
    }
    p.ellipse(0, 3, 10 * crown + 2, 2.5, W[4], crown);
    p.ellipse(0, 2, 6 * crown, 1.5, WHITE, crown);
  }
  if (t < 0.14) {
    const k = t / 0.14, y = -12 + 14 * k, sway = Math.sin(k * 9) * 3;
    p.tri(0, y + 6, -6 + sway, y - 5, 1 + sway, y - 1, K[1], 1);
    p.tri(0, y + 6, 7 + sway, y - 4, 1 + sway, y - 1, K[2], 1);
    p.line(0, y + 6, -6 + sway, y - 5, K[3], 1);
    p.circle(0, y + 7, 2.5, K[3], 1 - k);
  }
  for (let i = 0; i < 16; i++) {
    const born = rand(o.seed, i + 20) * 0.12, s = t - born, life = 0.3 + rand(o.seed, i + 30) * 0.18;
    if (s < 0 || s > life) continue;
    const a = -Math.PI / 2 + (rand(o.seed, i + 40) - 0.5) * 2.2, sp = 90 + rand(o.seed, i + 50) * 90;
    const x = Math.cos(a) * sp * s, y = 2 + Math.sin(a) * sp * s + 420 * s * s;
    if (y > 5) continue;
    p.dot(x, y, s < 0.1 ? WHITE : W[4], 1);
    p.dot(x, y - 1, W[3], 0.7);
    if (i % 4 === 0) p.dot(x + 1, y, K[2], 1);
  }
}

function lerp2(a, b, t) {
  return a + (b - a) * t;
}
