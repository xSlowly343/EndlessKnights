import { fit, clamp, rand, lerp, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Phoenix glide",
  desc: "A small firebird gliding, drawn in place so the game moves it: wings beat twice, three tail plumes of flame snake behind and shed embers.",
  tags: ["fire", "projectile", "creature", "one-shot"],
  size: [120, 60],
  origin: [94, 30],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#3a0a08", "#8a1f17", "#d8401c", "#ff8a2a", "#ffc23d", "#ffe98a", "#ffffff"],
    seed: 111,
  },
};

function wing(p, x, y, lift, C, far) {
  const side = lift < 0 ? 1 : -1, up = Math.abs(lift);
  for (let i = 4; i >= 0; i--) {
    const elev = 0.25 + up * (1.25 - i * 0.24), L = (far ? 13 : 16) - i * 2.2;
    const tx = x - Math.cos(elev) * L - i * 1.2, ty = y + side * Math.sin(elev) * L;
    p.beam(x - i * 1.2, y, tx, ty, 3.6, far ? C[0] : C[1], 1);
    p.beam(x - i * 1.2, y, tx, ty, 2.4, (u, v) => (far ? (u > 0.6 ? C[1] : C[2]) : u < 0.3 ? C[5] : u < 0.65 ? C[4] : v < 0.5 ? C[3] : C[2]), 1);
    if (!far) p.dot(tx, ty, C[5], 1);
  }
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), u = clamp(t / FLY), grow = clamp(u / 0.2), flap = Math.sin(u * Math.PI * 4 + 0.6);
  const bob = -flap * 1.5;
  p.glow(0, bob, 20, C[3], 0.45);
  p.glow(-22, 0, 16, C[2], 0.25 * grow);
  for (let k = 0; k < 3; k++) {
    let px = -5, py = bob + (k - 1) * 1.5;
    for (let i = 1; i <= 22; i++) {
      const d = i / 22;
      if (d > grow) break;
      const x = -5 - d * (58 - k * 8), y = bob * (1 - d) + (k - 1) * (2 + d * 9) + Math.sin(d * 7 - u * 14 + k * 2) * 3.5 * d;
      const wd = 3.2 * (1 - d) + 0.8;
      p.beam(px, py, x, y, wd, (a, v) => (d < 0.25 ? (v < 0.5 ? C[6] : C[5]) : d < 0.55 ? (v < 0.5 ? C[5] : C[4]) : d < 0.8 ? C[3] : C[2]), 1 - d * d * 0.7);
      if (i % 5 === 0) p.light(x, y, C[3], 0.5);
      px = x; py = y;
    }
    p.circle(px, py, 1.6 * grow, C[4], grow);
    p.dot(px, py, WHITE, grow);
  }
  wing(p, 1, bob - 1, flap * 0.7 + 0.3, C, true);
  p.beam(-6, bob + 1.5, 4, bob - 0.5, 5, (a, v) => (v > 0.75 ? C[2] : a > 0.5 ? C[5] : C[4]), 1);
  p.circle(6.5, bob - 2, 2.6, (d) => (d > 0.7 ? C[3] : C[5]), 1);
  p.tri(8.5, bob - 3, 12.5, bob - 1.2, 8.5, bob - 0.6, C[6], 1);
  p.dot(7, bob - 2.6, C[0], 1);
  p.line(5, bob - 4, 1, bob - 7.5, C[5], 1);
  p.line(4, bob - 4, -1.5, bob - 5.5, C[4], 1);
  p.dot(0.5, bob - 8, WHITE, 1);
  wing(p, 0, bob, flap, C, false);
  for (let i = 0; i < 14; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.4) continue;
    const x = -8 - rand(o.seed, i + 20) * 30 - age * 70, y = (rand(o.seed, i + 40) - 0.5) * 18 - age * 14 + Math.sin(age * 20 + i) * 2;
    p.dot(x, y, age < 0.12 ? WHITE : age < 0.26 ? C[5] : C[3], 1 - age / 0.4);
    p.light(x, y, C[3], 0.5 * (1 - age / 0.4));
  }
  p.dot(lerp(13, 15, flap * 0.5 + 0.5), bob - 1.2, WHITE, 0.8);
}
