import { fit, clamp, rng, bolt, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Thunder orb",
  desc: "Ball lightning, drawn in place so the game moves it: a white core in two counter-turning rings, arcs snapping off it, a forked tail that re-strikes every frame.",
  tags: ["lightning", "projectile", "one-shot"],
  size: [120, 52],
  origin: [98, 26],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0a0820", "#2a2470", "#5a54d8", "#8c9cff", "#c4d8ff", "#f0f6ff", "#ffffff"],
    spark: "#fff27a",
    seed: 131,
  },
};

export function strike(p, path, C, w, a) {
  for (let i = 1; i < path.length; i++) p.beam(path[i - 1][0], path[i - 1][1], path[i][0], path[i][1], w + 2, C[2], a * 0.8);
  for (let i = 1; i < path.length; i++) p.beam(path[i - 1][0], path[i - 1][1], path[i][0], path[i][1], w, (u, v) => (v < 0.5 ? WHITE : C[4]), a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), Y = hex(o.spark), u = clamp(t / FLY), grow = clamp(u / 0.2), frame = Math.floor(t * 30), R = rng(o.seed, frame);
  p.glow(0, 0, 22, C[3], 0.55);
  p.glow(-30, 0, 16, C[2], 0.3 * grow);
  for (let k = 0; k < 3; k++) {
    const L = (64 - k * 14) * grow, y1 = (k - 1) * 11 + R.range(-4, 4);
    const path = bolt(R, -5, (k - 1) * 2, -5 - L, y1, { jag: 0.26, depth: 4 });
    for (let i = 1; i < path.length; i++) {
      const d = i / path.length;
      p.line(path[i - 1][0], path[i - 1][1], path[i][0], path[i][1], d < 0.25 ? WHITE : d < 0.55 ? C[4] : d < 0.8 ? C[3] : C[2], 1 - d * 0.6);
      if (i % 4 === 0) p.light(path[i][0], path[i][1], C[3], 0.6);
    }
    const end = path[path.length - 1];
    p.dot(end[0], end[1], Y, 1);
  }
  for (let k = 0; k < 4; k++) {
    const a = R.range(0, Math.PI * 2), L = R.range(9, 16);
    const path = bolt(R, Math.cos(a) * 5, Math.sin(a) * 5, Math.cos(a) * L, Math.sin(a) * L, { jag: 0.4, depth: 2 });
    p.polyline(path, k % 2 ? C[5] : C[4], 1);
    p.dot(Math.cos(a) * L, Math.sin(a) * L, WHITE, 1);
  }
  const spin = u * 7;
  p.arc(0, 0, 10, 1.4, spin, spin + 2.2, C[4], (a) => Math.sin(a * Math.PI));
  p.arc(0, 0, 10, 1.4, spin + Math.PI, spin + Math.PI + 2.2, C[3], (a) => Math.sin(a * Math.PI));
  p.arc(0, 0, 7.5, 1.2, -spin * 1.4, -spin * 1.4 + 1.8, C[5], (a) => Math.sin(a * Math.PI));
  p.circle(0, 0, 5.4, C[2], 1);
  p.circle(0, 0, 4.4, (d) => (d < 0.55 ? WHITE : d < 0.8 ? C[5] : C[4]), 1);
  p.dot(-1.5, -1.5, Y, frame % 2 ? 1 : 0);
  for (let i = 0; i < 5; i++) {
    const x = R.range(-46, -8) * grow, y = R.range(-13, 13);
    p.dot(x, y, i % 2 ? Y : WHITE, 1);
    p.dot(x - 1, y, C[3], 0.8);
  }
}
