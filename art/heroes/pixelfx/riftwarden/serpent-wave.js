import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Serpent wave",
  desc: "A sea serpent swims along the ground as if it were water, drawn in place so the game moves it: head low over a bow wave, two coils arching behind, coral fins up, spray flying.",
  tags: ["water", "projectile", "creature", "ground", "one-shot"],
  size: [122, 40],
  origin: [100, 32],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#04201e", "#0a4a40", "#12806a", "#2ac8a0", "#7cf0c8", "#d0fff0", "#ffffff"],
    fin: ["#7a1a2a", "#e8504a", "#ffa878"],
    water: ["#0e2e5a", "#1c76bc", "#78ccf4", "#e8faff"],
    eye: "#ffe066",
    seed: 177,
  },
};

const SKULL = [[11, 0.5], [9, -2.4], [4, -4.2], [-2, -5], [-7, -3.6], [-9, 0], [-4, 1.6], [3, 1.4]];
const JAW = [[-6, 1], [8.5, 1.6], [7, 3.8], [-1, 4.8], [-7, 3.4]];

export function serpentHead(p, x, y, ang, s, open, C, F, eye, a = 1) {
  const ca = Math.cos(ang), sa = Math.sin(ang), at = ([px, py]) => [x + (px * ca - py * sa) * s, y + (px * sa + py * ca) * s];
  const turn = (pts, by) => pts.map(([px, py]) => {
    const dx = px + 6, dy = py - 1;
    return [-6 + dx * Math.cos(by) - dy * Math.sin(by), 1 + dx * Math.sin(by) + dy * Math.cos(by)];
  });
  const jaw = turn(JAW, open * 0.7), skull = turn(SKULL, -open * 0.3), fat = (pts, k) => pts.map(([px, py]) => [px * k + 0.4, py * k]);
  for (let i = 0; i < 3; i++) {
    const g = -2.2 - i * 0.42 - open * 0.25, b = turn([[-5 + i * 1.2, -3.6]], -open * 0.3)[0], L = 8 - i * 1.6;
    const A = at(b), T = at([b[0] + Math.cos(g) * L, b[1] + Math.sin(g) * L]), Bq = at([b[0] - 2.6, b[1] + 1.4]);
    p.tri(A[0], A[1], T[0], T[1], Bq[0], Bq[1], F[1], a);
    p.line(A[0], A[1], T[0], T[1], F[2], a);
  }
  p.poly(fat(jaw, 1.14).map(at), C[0], a);
  p.poly(fat(skull, 1.14).map(at), C[0], a);
  if (open > 0.1) p.poly([jaw[0], skull[7], skull[0], jaw[1]].map(at), F[0], a);
  p.poly(jaw.map(at), C[4], a);
  p.poly(skull.map(at), (px, py) => {
    const ly = (-(px - x) * sa + (py - y) * ca) / s;
    return ly < -3.2 ? C[4] : ly < -1 ? C[3] : C[2];
  }, a);
  const line = (a0, b0, c) => { const A = at(a0), B = at(b0); p.line(A[0], A[1], B[0], B[1], c, a); };
  line(skull[2], skull[3], C[5]);
  line(skull[1], skull[2], C[5]);
  if (open > 0.12) for (const fx of [8, 5.2, 2.4]) {
    const up = turn([[fx, 1.4], [fx - 0.6, 1.4 + (fx > 7 ? 3.6 : 2.2)]], -open * 0.3), dn = turn([[fx - 1.4, 2], [fx - 1, 0.2]], open * 0.7);
    line(up[0], up[1], WHITE);
    line(dn[0], dn[1], WHITE);
  }
  const e = at(turn([[3.4, -2.4]], -open * 0.3)[0]);
  p.circle(e[0], e[1], 1.3 * s, C[0], a);
  p.dot(e[0], e[1], eye, a);
  p.light(e[0], e[1], eye, 0.9 * a);
  p.glow(e[0], e[1], 4, eye, 0.5 * a);
}

export function coil(p, pts, w0, w1, C, F, a = 1, scroll = 0) {
  const n = pts.length - 1;
  for (let pass = 0; pass < 2; pass++) for (let i = 1; i <= n; i++) {
    const u = i / n, w = w0 + (w1 - w0) * u, [x0, y0] = pts[i - 1], [x1, y1] = pts[i];
    if (pass === 0) p.beam(x0, y0, x1, y1, w + 2, C[0], a);
    else p.beam(x0, y0, x1, y1, w, (q, v) => (v > 0.72 ? C[1] : (Math.floor(i * 0.7 + scroll) % 2 ? C[3] : C[2])), a);
  }
  for (let i = 1; i <= n; i++) {
    const u = i / n, w = w0 + (w1 - w0) * u, [x0, y0] = pts[i - 1], [x1, y1] = pts[i], dx = x1 - x0, dy = y1 - y0, L = Math.hypot(dx, dy) || 1, nx = dy / L, ny = -dx / L;
    const mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
    p.dot(mx + nx * w * 0.2, my + ny * w * 0.2, C[4], a);
    p.dot(mx - nx * w * 0.32, my - ny * w * 0.32, C[5], a);
    if (i % 3 === 1 && i > 2 && i < n - 1) {
      p.tri(mx + nx * w * 0.4 - (dx / L) * 2, my + ny * w * 0.4 - (dy / L) * 2, mx + nx * w * 0.4 + (dx / L) * 2, my + ny * w * 0.4 + (dy / L) * 2, mx + nx * (w * 0.5 + 3.4) - (dx / L) * 2, my + ny * (w * 0.5 + 3.4) - (dy / L) * 2, F[1], a);
      p.dot(mx + nx * (w * 0.5 + 3) - (dx / L) * 2, my + ny * (w * 0.5 + 3) - (dy / L) * 2, F[2], a);
    }
  }
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), F = fit(o.fin, 3), W = fit(o.water, 4), eye = hex(o.eye), u = clamp(t / FLY), ph = u * Math.PI * 2;
  p.glow(-6, -6, 22, C[2], 0.35);
  for (const [cx, r, off] of [[-58, 7, 2.2], [-31, 9.5, 0]]) {
    const pts = [], rise = 0.8 + 0.2 * Math.sin(ph + off);
    for (let i = 0; i <= 12; i++) { const g = Math.PI + (i / 12) * Math.PI; pts.push([cx + Math.cos(g) * r * 1.25, 1 + Math.sin(g) * r * 1.5 * rise]); }
    coil(p, pts, 5.5, 6.5, C, F, 1, u * 6);
  }
  const neck = [];
  for (let i = 0; i <= 8; i++) { const s = i / 8; neck.push([-17 + s * 12, 1 - Math.sin(s * Math.PI * 0.55) * (7.5 + Math.sin(ph) * 1.2)]); }
  coil(p, neck, 6.5, 5.6, C, F, 1, u * 6);
  serpentHead(p, 0, -7 - Math.sin(ph) * 1.2, 0.1 + Math.sin(ph) * 0.06, 1, 0.25 + 0.3 * u, C, F, eye);
  for (let x = -92; x <= 8; x += 2) {
    const k = clamp((x + 92) / 40), crest = Math.sin(x * 0.32 + ph * 2) * 1.2 + (x > -8 ? (x + 8) * 0.28 : 0);
    p.line(x, 0 - crest * k, x + 2, 0 - Math.sin((x + 2) * 0.32 + ph * 2) * 1.2 * k - (x > -10 ? (x + 10) * 0.28 : 0), k > 0.8 ? W[3] : W[2], k);
    p.line(x, 1.5, x + 2, 1.5, W[1], k * 0.8);
  }
  p.glowLine(-70, 0, 6, 0, 6, W[1], 0.4);
  for (let i = 0; i < 16; i++) {
    const born = rand(o.seed, i) * 0.85, age = u - born;
    if (age < 0 || age > 0.4) continue;
    const src = i % 3 === 0 ? 4 : i % 3 === 1 ? -22 : -50, x = src - age * (30 + rand(o.seed, i + 10) * 60), y = -2 - rand(o.seed, i + 20) * 70 * age + 240 * age * age;
    if (y > 1) continue;
    p.dot(x, y, age < 0.15 ? W[3] : W[2], 1 - age / 0.4);
    p.dot(x + 1, y + 1, W[1], 0.6 * (1 - age / 0.4));
  }
}
