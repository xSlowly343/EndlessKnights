import { fit, clamp, rand, seg, ease, hex, WHITE } from "../../core/index.js";
import { note, flag } from "./chord-wave.js";

const END = 0.6;

export const meta = {
  name: "Chord ring",
  desc: "The chord lands and rings out: comic hit ticks, sound pushing away in bracketed pulses of three colours to both sides, and notes knocked loose that float up and wobble.",
  tags: ["music", "impact", "wave", "one-shot"],
  size: [128, 96],
  origin: [64, 52],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#2a0a2e", "#ff5aa8", "#ffb0d4"],
    second: ["#06282e", "#2ee0c8", "#b0fff0"],
    third: ["#2e2406", "#ffd23c", "#fff3b0"],
    seed: 112,
  },
};

export function draw(p, t, o) {
  if (t >= END) return;
  const RB = [fit(o.palette, 3), o.second.map(hex), o.third.map(hex)];
  const glow = clamp(1 - t / 0.3);
  if (glow > 0) p.glow(0, 0, 22, RB[0][1], 0.4 * glow);
  for (let i = 0; i < 4; i++) {
    const s = t - i * 0.07;
    if (s < 0 || s > 0.32) continue;
    const k = s / 0.32, r = 6 + 34 * ease.outCubic(k), R = RB[i % 3], span = 0.95 - 0.35 * k, wd = 3 * (1 - k) + 1;
    for (const mid of [0, Math.PI]) {
      p.arc(0, 0, r + 1, wd + 1, mid - span, mid + span, R[0], 0.6 * (1 - k));
      p.arc(0, 0, r, wd, mid - span, mid + span, (a) => (Math.abs(a - 0.5) < 0.18 && k < 0.5 ? WHITE : k < 0.6 ? R[2] : R[1]), 1 - k * k);
    }
    p.glowRing(0, 0, r, 5, R[1], 0.3 * (1 - k));
  }
  if (t < 0.12) {
    const k = t / 0.12;
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * 6.283 + 0.39, r0 = 4 + 9 * k, r1 = r0 + 6 * (1 - k) + 2;
      p.line(Math.cos(a) * r0, Math.sin(a) * r0, Math.cos(a) * r1, Math.sin(a) * r1, k < 0.5 ? WHITE : RB[2][2], 1);
    }
    p.circle(0, 0, 5 * (1 - k), WHITE, 1);
  }
  for (let i = 0; i < 6; i++) {
    const s = t - 0.04 - i * 0.035, life = 0.36 + rand(o.seed, i) * 0.1;
    if (s < 0 || s > life) continue;
    const k = s / life, dir = (i - 2.5) / 2.5, x = dir * (10 + 26 * ease.outCubic(k)) + Math.sin(s * 16 + i) * 2.5;
    const y = -4 - 34 * ease.outQuad(k) + (i % 2 ? 6 : 0), R = RB[i % 3], a = clamp((1 - k) / 0.3), tilt = Math.sin(s * 16 + i) * 2;
    const top = note(p, x, y, R, tilt, a, 7);
    if (i % 2) flag(p, top[0], top[1], R, a);
    p.light(x, y, R[1], 0.6 * a);
  }
  for (let i = 0; i < 10; i++) {
    const born = 0.05 + rand(o.seed, i + 20) * 0.3, age = t - born;
    if (age < 0 || age > 0.14) continue;
    const x = (rand(o.seed, i + 30) - 0.5) * 80, y = (rand(o.seed, i + 40) - 0.5) * 50 - 6, R = RB[i % 3], k = Math.sin((age / 0.14) * Math.PI);
    p.dot(x, y, WHITE, 1);
    if (k > 0.5) { p.dot(x - 1, y, R[1], 1); p.dot(x + 1, y, R[1], 1); p.dot(x, y - 1, R[1], 1); p.dot(x, y + 1, R[1], 1); }
  }
}
