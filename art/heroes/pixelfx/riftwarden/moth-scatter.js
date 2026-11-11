import { fit, clamp, rand, seg, ease, hex, sparkle, WHITE } from "../../core/index.js";
import { moth } from "./moth-flock.js";

const END = 0.6;

export const meta = {
  name: "Moth scatter",
  desc: "The moths hit and break formation: a soft puff of wing dust, the flock spiralling out and up on beating wings, scales that glitter as they sink.",
  tags: ["nature", "impact", "swarm", "one-shot"],
  size: [128, 104],
  origin: [64, 58],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#241a3a", "#4a3a78", "#7d6cc0", "#b3a4ee", "#dcd2ff", "#f6f2ff"],
    dust: "#8ef0e0",
    seed: 52,
  },
};

export function draw(p, t, o) {
  if (t >= END) return;
  const C = fit(o.palette, 6), D = hex(o.dust), k = seg(t, 0, 0.5), out = ease.outCubic(k);
  const puff = clamp(1 - t / 0.3);
  if (puff > 0) {
    p.disc(0, 0, 5 + 9 * (1 - puff), C[2], 0.5 * puff);
    p.disc(-1, -2, 3 + 6 * (1 - puff), C[4], 0.45 * puff);
    p.glow(0, 0, 20, C[3], 0.4 * puff);
  }
  if (t < 0.07) sparkle(p, 0, 0, 7 * (1 - t / 0.07), C[5], WHITE);
  for (let i = 0; i < 22; i++) {
    const born = rand(o.seed, i + 60) * 0.22, age = t - born, life = 0.25 + rand(o.seed, i + 70) * 0.12;
    if (age < 0 || age > life) continue;
    const a = rand(o.seed, i + 80) * 6.28, r = 4 + rand(o.seed, i + 90) * 20 * ease.outQuad(clamp(age / 0.2));
    const x = Math.cos(a) * r + Math.sin(age * 20 + i) * 1.5, y = Math.sin(a) * r * 0.7 + age * age * 90;
    const tw = Math.sin(age * 40 + i * 2) > 0;
    p.dot(x, y, tw ? WHITE : i % 3 ? D : C[4], 1 - age / life);
    if (tw) p.light(x, y, D, 0.4);
  }
  const gone = clamp(1 - seg(t, 0.36, 0.56));
  for (let i = 0; i < 7; i++) {
    const a0 = (i / 7) * 6.28 + rand(o.seed, i) * 0.5, turn = (i % 2 ? 1 : -1) * (1.2 + rand(o.seed, i + 10));
    const r = 3 + (16 + rand(o.seed, i + 20) * 16) * out, a = a0 + turn * out;
    const x = Math.cos(a) * r, y = Math.sin(a) * r * 0.75 - 12 * k * k;
    const size = (i % 3 === 0 ? 5.5 : 3.5 + rand(o.seed, i + 30) * 1.5) * (0.45 + 0.55 * gone);
    if (gone > 0.05) moth(p, x, y, size, Math.sin(t * 55 + i * 1.7), C, gone);
  }
}
