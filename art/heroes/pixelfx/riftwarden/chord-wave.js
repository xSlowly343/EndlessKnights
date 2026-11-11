import { fit, clamp, hex, sparkle, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Chord wave",
  desc: "A played chord made visible, drawn in place so the game moves it: three ribbons of sound in three colours with notes riding them, a pair of beamed notes in front.",
  tags: ["music", "projectile", "wave", "one-shot"],
  size: [120, 64],
  origin: [96, 34],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#2a0a2e", "#ff5aa8", "#ffb0d4"],
    second: ["#06282e", "#2ee0c8", "#b0fff0"],
    third: ["#2e2406", "#ffd23c", "#fff3b0"],
    seed: 111,
  },
};

export function note(p, x, y, R, tilt = 0, a = 1, stem = 8) {
  p.ellipse(x, y, 3.4, 2.7, R[0], a);
  p.ellipse(x, y, 2.5, 1.9, R[1], a);
  p.dot(x - 1, y - 1, R[2], a);
  p.line(x + 2, y, x + 2 + tilt, y - stem, R[0], a);
  p.line(x + 3, y, x + 3 + tilt, y - stem, R[1], a);
  return [x + 3 + tilt, y - stem];
}

export function flag(p, x, y, R, a = 1) {
  p.line(x, y, x + 3, y + 2, R[1], a);
  p.line(x, y + 1, x + 4, y + 4, R[1], a);
  p.dot(x + 4, y + 5, R[2], a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const P1 = fit(o.palette, 3), P2 = o.second.map(hex), P3 = o.third.map(hex), RB = [P1, P2, P3];
  const u = clamp(t / FLY), grow = clamp(u / 0.2), len = 20 + 62 * grow;
  const wave = (x, j) => (j - 1) * 6 + Math.sin(x * 0.17 - u * 11 + j * 1.3) * 4 * Math.min(1, -x / 10 + 0.3);
  p.glow(-len * 0.4, 0, 22, P1[1], 0.2);
  for (let j = 0; j < 3; j++) {
    for (let x = -len; x <= -2; x += 1) {
      const k = 1 + x / len, y = wave(x, j);
      p.dot(x, y, k > 0.75 ? RB[j][2] : RB[j][1], 0.15 + 0.85 * k);
      if (k > 0.45) p.dot(x, y + 1, RB[j][1], k * 0.7);
      if ((Math.floor(x) & 7) === 0) p.light(x, y, RB[j][1], 0.35 * k);
    }
  }
  for (let j = 0; j < 3; j++) {
    const x = -18 - j * 19 - ((u * 20) % 6);
    if (x < -len + 4) continue;
    const y = wave(x, (j + 1) % 3) - 1, top = note(p, x, y, RB[(j + 1) % 3], 0, 1, 7);
    flag(p, top[0], top[1], RB[(j + 1) % 3]);
    p.light(x, y, RB[(j + 1) % 3][1], 0.6);
  }
  p.glow(3, 0, 13, P3[1], 0.35);
  const bob = Math.sin(u * 14) * 1.5, a = note(p, -2, 3 + bob, P1, 0, 1, 10), b = note(p, 7, 0 + bob, P1, 0, 1, 9);
  p.beam(a[0], a[1], b[0], b[1], 2.4, P1[1], 1);
  p.line(a[0], a[1] - 1, b[0], b[1] - 1, P1[2], 1);
  p.line(a[0], a[1] + 3, b[0], b[1] + 3, P1[1], 1);
  if (u < 0.4) sparkle(p, 9, -9 + bob, 4 * (1 - u / 0.4), P3[2], WHITE);
}
