import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Raven dive",
  desc: "A herald raven in a stoop, drawn in place so the game moves it: beak forward, wings beating once and folding back, loose feathers and cold runes strung out along its wake.",
  tags: ["shadow", "projectile", "creature", "one-shot"],
  size: [120, 54],
  origin: [100, 28],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#05060e", "#10142a", "#1e2850", "#34468a", "#6a84c8", "#b4ccf4", "#ffffff"],
    rune: ["#0c4a5e", "#2cb4d8", "#7cf0ff", "#e8ffff"],
    seed: 181,
  },
};

const RUNES = [[[0, -2, 0, 2], [0, -2, 2, 0], [0, 0, 2, 2]], [[-2, -2, 2, 2], [2, -2, -2, 2]], [[0, -2, 0, 2], [-2, 0, 0, -2], [2, 0, 0, -2]], [[-2, -2, -2, 2], [-2, -2, 2, 0], [2, 0, -2, 2]], [[-2, 2, 0, -2], [0, -2, 2, 2], [-1, 0, 1, 0]], [[0, -2, 0, 2], [0, -1, 2, -2], [0, 1, 2, 0]]];

export function rune(p, x, y, kind, c, hot, a = 1, k = 0.6) {
  for (const [x0, y0, x1, y1] of RUNES[kind % RUNES.length]) p.line(x + x0, y + y0, x + x1, y + y1, c, a);
  p.dot(x, y, hot, a);
  p.glow(x, y, 6, c, k * a);
}

export function plume(p, x, y, ang, L, w, C, N, a = 1) {
  if (L < 2) return;
  const ca = Math.cos(ang), sa = Math.sin(ang), at = (u, v) => [x + ca * L * u - sa * w * v, y + sa * L * u + ca * w * v];
  const pts = [at(0, 0), at(0.25, 0.75), at(0.6, 1), at(0.85, 0.7), at(1, 0), at(0.85, -0.7), at(0.6, -1), at(0.25, -0.75)];
  p.poly(pts.map(([px, py]) => [px + Math.sign(px - x - ca * L * 0.5) * 0.9, py + Math.sign(py - y - sa * L * 0.5) * 0.9]), C[3], a);
  p.poly(pts, (px, py) => ((px - x) * -sa + (py - y) * ca > 0 ? C[1] : C[2]), a);
  const [tx, ty] = at(1, 0), [qx, qy] = at(0.7, 0);
  p.line(x, y, qx, qy, N[1], a);
  for (const u of [0.35, 0.55, 0.75]) for (const side of [-1, 1]) { const [bx, by] = at(u, 0), [ex, ey] = at(u + 0.12, side * 0.8); p.line(bx, by, ex, ey, C[3], a); }
  p.dot(tx, ty, N[2], a);
}

export function raven(p, x, y, ang, s, wing, C, N, a = 1) {
  const ca = Math.cos(ang), sa = Math.sin(ang), at = (px, py) => [x + (px * ca - py * sa) * s, y + (px * sa + py * ca) * s];
  const P = (list) => list.map(([px, py]) => at(px, py));
  for (const far of [1, 0]) {
    const up = wing * (far ? 0.8 : 1), ab = 1 - Math.abs(up), tipx = -7 - 5 * ab, tipy = -17 * up;
    const pts = [[2, -1], [-1, -1 - 7 * up], [tipx, tipy], [tipx - 3.5, tipy + 5 * up + 1.5], [tipx - 1, tipy + 9 * up + 2.5], [-10, -3 * up + 3.5], [-6, 2]];
    p.poly(P(pts.map(([px, py]) => [px * 1.07 - 0.3, py * 1.07 - (far ? 1.5 : 0)])), far ? C[0] : C[3], a);
    p.poly(P(pts.map(([px, py]) => [px, py - (far ? 1.5 : 0)])), far ? C[1] : C[2], a);
    const A = at(2, -1), B = at(-1, -1 - 7 * up), T = at(tipx, tipy);
    p.line(A[0], A[1], B[0], B[1], far ? C[3] : C[4], a);
    p.line(B[0], B[1], T[0], T[1], far ? C[3] : C[5], a);
    if (!far) for (let i = 0; i < 4; i++) { const F0 = at(-2 - i * 1.6, -3 * up - i * up), F1 = at(tipx - 3 + i * 1.6, tipy + (4 + i * 2.2) * up + 2); p.line(F0[0], F0[1], F1[0], F1[1], C[1], a); }
  }
  p.poly(P([[-7, 0], [-15, -2.4], [-16.5, 0.4], [-15, 3], [-7, 2.4]]), C[1], a);
  p.poly(P([[5, -2.6], [0, -3.6], [-8, -1.8], [-9, 1.8], [-2, 3.6], [5, 2.2]]), C[2], a);
  const b0 = at(-7, -1.4), b1 = at(2, -3.2);
  p.line(b0[0], b0[1], b1[0], b1[1], C[4], a);
  const h = at(6, -1.6);
  p.circle(h[0], h[1], 3 * s, C[2], a);
  p.poly(P([[8, -2.8], [14, -0.4], [8.4, 0.6]]), C[4], a);
  p.poly(P([[8.4, -0.4], [14, -0.4], [8.4, 0.9]]), C[3], a);
  const c0 = at(4, -4), c1 = at(7.4, -4.4);
  p.line(c0[0], c0[1], c1[0], c1[1], C[5], a);
  const e = at(7, -2);
  p.dot(e[0], e[1], N[3], a);
  p.light(e[0], e[1], N[2], a);
  p.glow(e[0], e[1], 4, N[1], 0.6 * a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), N = fit(o.rune, 4), u = clamp(t / FLY), wing = Math.cos(u * Math.PI * 2) * (1 - u * 0.5), bob = Math.sin(u * Math.PI * 2) * 1.5;
  for (let k = 0; k < 3; k++) {
    let px = -14, py = bob + (k - 1) * 2.4;
    for (let i = 1; i <= 14; i++) {
      const d = i / 14, x = -14 - d * (66 - Math.abs(k - 1) * 14), y = bob * (1 - d) + (k - 1) * (2.4 + d * 7) + Math.sin(d * 6 - u * 12 + k * 2) * 3 * d;
      p.beam(px, py, x, y, Math.max(1, 2.2 * (1 - d)), d < 0.2 ? N[1] : N[0], 0.8 - d * 0.7);
      px = x; py = y;
    }
  }
  p.glow(-6, bob, 22, N[1], 0.3);
  for (let i = 0; i < 4; i++) {
    const s = (u * 0.9 + i * 0.25) % 1, x = -22 - s * 62, y = (rand(o.seed, i) - 0.5) * 24 + Math.sin(s * 8 + i) * 2;
    rune(p, x, y, i + 1, s < 0.3 ? N[3] : N[2], N[3], 1 - s, 0.5);
  }
  for (let i = 0; i < 4; i++) {
    const s = (u * 0.8 + i * 0.27 + 0.1) % 1, x = -16 - s * 64, y = (rand(o.seed, i + 9) - 0.5) * 26 + s * 6;
    plume(p, x, y, 2.6 + Math.sin(s * 9 + i) * 0.8, 9, 2, C, N, s > 0.7 ? 0.5 : 1);
  }
  raven(p, -2, bob, 0.06, 1.15, wing, C, N);
  p.line(13, bob + 0.4, 17, bob + 0.6, WHITE, 0.9);
}
