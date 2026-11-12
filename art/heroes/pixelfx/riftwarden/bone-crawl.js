import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Bone crawl",
  desc: "A skeleton hand scuttles on its fingertips, drawn in place so the game moves it: knuckles arched, the wrist stump burning with soul-fire, grave dust behind.",
  tags: ["bone", "projectile", "creature", "ground", "one-shot"],
  size: [112, 36],
  origin: [92, 30],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#1a1814", "#3a352c", "#5e5648", "#8a806c", "#b8ad96", "#e4dcc8", "#fffaf0"],
    soul: ["#0c2a22", "#1c6a4c", "#3cb47c", "#8cf0b4", "#e0fff0"],
    seed: 171,
  },
};

export function bone(p, x0, y0, x1, y1, w, C, a = 1, floor = Infinity) {
  if (y0 > floor && y1 > floor) return;
  if (y0 > floor) { const k = (floor - y1) / (y0 - y1); x0 = x1 + (x0 - x1) * k; y0 = floor; }
  if (y1 > floor) { const k = (floor - y0) / (y1 - y0); x1 = x0 + (x1 - x0) * k; y1 = floor; }
  p.beam(x0, y0, x1, y1, w + 1.8, C[1], a);
  p.beam(x0, y0, x1, y1, w, (u, v) => (v < 0.45 ? C[5] : C[4]), a);
  if (y0 < floor) { p.circle(x0, y0, w * 0.5 + 0.9, C[1], a); p.circle(x0, y0, w * 0.5 + 0.2, C[5], a); p.dot(x0 - 0.5, y0 - 0.5, C[6], a); }
  if (y1 < floor) { p.circle(x1, y1, w * 0.5 + 0.7, C[1], a); p.circle(x1, y1, w * 0.5, C[4], a); }
}

function leg(p, hx, hy, fx, fy, l1, l2, C, a) {
  const dx = fx - hx, dy = fy - hy, d = Math.min(Math.hypot(dx, dy), l1 + l2 - 0.2), base = Math.atan2(dy, dx);
  const bend = Math.acos(clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1, 1));
  const kx = hx + Math.cos(base - bend) * l1, ky = hy + Math.sin(base - bend) * l1;
  bone(p, hx, hy, kx, ky, 2, C, a);
  bone(p, kx, ky, fx, fy, 1.6, C, a);
  p.dot(fx, fy, C[6], a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), S = fit(o.soul, 5), u = clamp(t / FLY), run = u * Math.PI * 4, bob = Math.abs(Math.sin(run)) * 1.6;
  const wx = -22, wy = -9 - bob;
  for (let k = 0; k < 4; k++) {
    let px = wx - 3, py = wy + (k - 1.5) * 1.6;
    for (let i = 1; i <= 12; i++) {
      const d = i / 12, x = wx - 3 - d * (44 - Math.abs(k - 1.5) * 9), y = wy + (k - 1.5) * (1.6 + d * 3.4) - d * d * 7 + Math.sin(d * 7 - u * 14 + k * 1.9) * 2.6 * d;
      p.beam(px, py, x, y, Math.max(1, 3.4 * (1 - d)), d < 0.18 ? S[4] : d < 0.45 ? S[3] : d < 0.75 ? S[2] : S[1], 1 - d * 0.8);
      px = x; py = y;
    }
  }
  p.glow(wx - 4, wy, 12, S[2], 0.6);
  p.glow(-8, -8, 18, S[2], 0.25);
  for (let i = 0; i < 2; i++) leg(p, wx + 6, wy - 1 + i, wx + 1 + Math.cos(run + 2 + i * Math.PI) * 5, Math.min(0, -Math.sin(run + 2 + i * Math.PI) * 3), 6, 6, C, 1);
  bone(p, wx - 3, wy + 1.5, wx + 2, wy + 0.5, 3, C);
  bone(p, wx - 3, wy - 1.5, wx + 2, wy - 1, 3, C);
  for (let i = 0; i < 4; i++) {
    const kx = -9 + i * 1.6, ky = wy - 4.5 + i * 0.8;
    bone(p, wx + 3 + i * 0.4, wy - 1 + i * 0.5, kx, ky, 1.8, C);
  }
  for (let i = 3; i >= 0; i--) {
    const ph = run + i * Math.PI * 0.5, kx = -9 + i * 1.6, ky = wy - 4.5 + i * 0.8;
    leg(p, kx, ky, -4 + i * 1.8 + Math.cos(ph) * 5.5, Math.min(0, -Math.sin(ph) * 4), 7, 7.5, C, 1);
  }
  for (let i = 0; i < 12; i++) {
    const born = rand(o.seed, i) * 0.85, age = u - born;
    if (age < 0 || age > 0.4) continue;
    const x = -8 - rand(o.seed, i + 20) * 30 - age * 60, y = -rand(o.seed, i + 40) * 3 - age * 14 * rand(o.seed, i + 60);
    p.disc(x, y, 1.2 + age * 6, C[3], 0.5 * (1 - age / 0.4));
    if (i % 3 === 0) p.dot(x + 3, y - 2 - age * 20, age < 0.2 ? S[4] : S[3], 1 - age / 0.4);
  }
}
