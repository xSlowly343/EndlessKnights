import { fit, clamp, rand, seg, hex, sparkle, WHITE } from "../../core/index.js";
import { card } from "./card-throw.js";

const END = 0.6;

export const meta = {
  name: "Card cut",
  desc: "The cards land as three clean cuts, one after another, crossing on the target; then the spent cards tumble down face over back and red pips scatter.",
  tags: ["trick", "impact", "cards", "one-shot"],
  size: [112, 96],
  origin: [56, 44],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#2a1c3a", "#7a7090", "#c8c0d8", "#f4f0fa", "#ffffff"],
    suit: ["#5a0a1c", "#c41e3a", "#ff6a7a"],
    seed: 72,
  },
};

const CUTS = [[-17, -15, 17, 13, 0], [17, -16, -16, 13, 0.05], [-21, 1, 21, -3, 0.1]];

export function draw(p, t, o) {
  if (t >= END) return;
  const C = fit(o.palette, 5), S = o.suit.map(hex);
  for (const [x0, y0, x1, y1, at] of CUTS) {
    const s = t - at;
    if (s < 0 || s > 0.26) continue;
    const reach = seg(s, 0, 0.035), fade = clamp(1 - seg(s, 0.06, 0.26)), ex = x0 + (x1 - x0) * reach, ey = y0 + (y1 - y0) * reach;
    const L = Math.hypot(x1 - x0, y1 - y0), nx = -(y1 - y0) / L, ny = (x1 - x0) / L, wide = 1 + 1.6 * fade;
    p.glowLine(x0, y0, ex, ey, 5, S[1], 0.45 * fade);
    p.line(x0 + nx * wide, y0 + ny * wide, ex + nx * wide, ey + ny * wide, S[1], (v) => fade * Math.sin(Math.PI * v));
    p.line(x0 - nx * wide, y0 - ny * wide, ex - nx * wide, ey - ny * wide, S[2], (v) => fade * Math.sin(Math.PI * v));
    p.line(x0, y0, ex, ey, WHITE, (v) => (0.3 + fade) * Math.sin(Math.PI * v) * (fade > 0.15 ? 1 : 0));
    if (s < 0.05) sparkle(p, ex, ey, 5, C[4], WHITE);
  }
  if (t > 0.1 && t < 0.2) sparkle(p, 0, -1, 9 * (1 - (t - 0.1) / 0.1), S[2], WHITE);
  for (let i = 0; i < 5; i++) {
    const s = t - 0.03 - i * 0.025, life = 0.42 + rand(o.seed, i) * 0.1;
    if (s < 0 || s > life) continue;
    const vx = (i - 2) * 55 + (rand(o.seed, i + 10) - 0.5) * 30, vy = -90 - rand(o.seed, i + 20) * 90, d = 1 - Math.exp(-5 * s);
    const x = (vx * d) / 4 + Math.sin(s * 11 + i) * 3, y = (vy * d) / 4 + 170 * s * s;
    card(p, x, y, 3.5, 5.5, (rand(o.seed, i + 30) - 0.5) * 1.2 + s * 3 * (i % 2 ? 1 : -1), Math.cos(s * 16 + i * 2), C, S, clamp((life - s) / 0.14));
  }
  for (let i = 0; i < 8; i++) {
    const s = t - 0.05 - rand(o.seed, i + 40) * 0.1, life = 0.22;
    if (s < 0 || s > life) continue;
    const a = rand(o.seed, i + 50) * 6.28, r = 6 + 60 * s * (1 - s * 1.6), x = Math.cos(a) * r, y = Math.sin(a) * r * 0.8 + 100 * s * s;
    p.poly([[x, y - 2], [x + 1.5, y], [x, y + 2], [x - 1.5, y]], i % 2 ? S[1] : S[2], 1 - s / life);
  }
}
