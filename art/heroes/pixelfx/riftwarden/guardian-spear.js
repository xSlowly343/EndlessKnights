import { fit, clamp, rand, hex, sparkle, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Guardian spear",
  desc: "A spear of light on a single wing, drawn in place so the game moves it: leaf blade white-hot, halo at the socket, the wing beating once, ribbons of sky and gold dust trailing.",
  tags: ["holy", "projectile", "summon", "one-shot"],
  size: [124, 50],
  origin: [104, 30],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#4a2c08", "#9a6c18", "#e8b030", "#ffd84a", "#fff09a", "#fffbe6", "#ffffff"],
    sky: ["#1c3a8c", "#3a7ae0", "#7cc0ff", "#d8f0ff"],
    seed: 185,
  },
};

export function wing(p, x, y, ang, s, spread, C, S, a = 1) {
  const n = 7, ca = Math.cos(ang), sa = Math.sin(ang), at = (px, py) => [x + (px * ca - py * sa) * s, y + (px * sa + py * ca) * s];
  const arm = [];
  for (let i = 0; i <= n; i++) { const f = i / n, g = -0.35 - spread * 1.15 * Math.pow(f, 0.8); arm.push([Math.cos(g) * 15 * f * (0.5 + 0.5 * spread), Math.sin(g) * 15 * f * (0.5 + 0.5 * spread)]); }
  for (let i = n; i >= 1; i--) {
    const f = i / n, [bx, by] = arm[i], g = 0.35 + (1 - f) * 0.9 * spread + (1 - spread) * 0.7 - spread * f * 0.55, L = (5 + 9 * f) * (0.6 + 0.4 * spread);
    const B = at(bx, by), T = at(bx - Math.cos(g) * L, by + Math.sin(g) * L);
    p.beam(B[0], B[1], T[0], T[1], 4.2, i % 2 ? S[1] : C[2], a);
    p.beam(B[0], B[1], T[0], T[1], 2.8, (u, v) => (u > 0.75 ? (i % 2 ? S[2] : C[4]) : v < 0.5 ? C[6] : C[5]), a);
  }
  for (let i = 1; i <= n; i++) { const A = at(arm[i - 1][0], arm[i - 1][1]), B = at(arm[i][0], arm[i][1]); p.beam(A[0], A[1], B[0], B[1], 3.4 - i * 0.2, (u, v) => (v < 0.5 ? C[6] : C[4]), a); }
  const tip = at(arm[n][0], arm[n][1]);
  p.dot(tip[0], tip[1], WHITE, a);
  p.light(tip[0], tip[1], C[4], a);
}

export function spear(p, x, y, ang, L, C, S, a = 1, hot = 1) {
  const ca = Math.cos(ang), sa = Math.sin(ang), at = (u, v) => [x - ca * u - sa * v, y - sa * u + ca * v];
  const tail = at(L, 0), sock = at(11, 0);
  p.glowLine(x, y, tail[0], tail[1], 5, C[3], 0.4 * a * hot);
  p.beam(sock[0], sock[1], tail[0], tail[1], 3.2, C[1], a);
  p.beam(sock[0], sock[1], tail[0], tail[1], 1.6, (u) => (Math.floor(u * 9) % 3 === 0 ? C[3] : C[5]), a);
  const butt = at(L + 1.5, 0);
  p.circle(butt[0], butt[1], 2, C[2], a);
  p.dot(butt[0], butt[1], C[5], a);
  const blade = [at(-1, 0), at(5, 3.4), at(11, 1.6), at(11, -1.6), at(5, -3.4)];
  p.poly(blade.map(([px, py]) => [px + (px - x) * 0.14 + 0.4, py + (py - y) * 0.14 + 0.4]), C[1], a);
  p.poly(blade, (px, py) => (Math.abs((px - x) * sa - (py - y) * ca) < 1.1 ? WHITE : (px - x) * sa - (py - y) * ca > 0 ? C[5] : C[4]), a);
  for (const side of [-1, 1]) { const g0 = at(11.5, side * 1), g1 = at(13.5, side * 4.6), g2 = at(10, side * 4); p.tri(g0[0], g0[1], g1[0], g1[1], g2[0], g2[1], C[3], a); p.dot(g1[0], g1[1], C[5], a); }
  const gem = at(12.5, 0);
  p.circle(gem[0], gem[1], 1.9, S[1], a);
  p.dot(gem[0], gem[1], S[3], a);
  p.glow(gem[0], gem[1], 6, S[2], 0.6 * a);
  p.glow(x - ca * 4, y - sa * 4, 10, C[4], 0.5 * a * hot);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), S = fit(o.sky, 4), u = clamp(t / FLY), beat = 0.55 + 0.45 * Math.cos(u * Math.PI * 2), bob = Math.sin(u * Math.PI * 2) * 1.2;
  for (let k = 0; k < 3; k++) {
    let px = -34, py = bob + (k - 1) * 1.5;
    for (let i = 1; i <= 14; i++) {
      const d = i / 14, x = -34 - d * (60 - Math.abs(k - 1) * 12), y = bob * (1 - d) + (k - 1) * (1.5 + d * 9) + Math.sin(d * 6 - u * 12 + k * 2.1) * 3 * d;
      p.beam(px, py, x, y, Math.max(1, 3 * (1 - d)), k === 1 ? (d < 0.3 ? C[5] : d < 0.6 ? C[3] : C[2]) : d < 0.3 ? S[3] : d < 0.6 ? S[2] : S[1], 1 - d * 0.85);
      px = x; py = y;
    }
  }
  p.glow(-8, bob, 24, C[2], 0.35);
  wing(p, -17, bob - 1, 0, 1.15, beat, C, S);
  spear(p, 0, bob, 0, 36, C, S);
  p.ring(-12.5, bob, 6.5, 1, C[4], (v, g) => (Math.abs(Math.cos(g)) < 0.45 ? 1 : 0));
  sparkle(p, 1, bob, 4 + 2 * Math.sin(u * Math.PI * 4), C[4], WHITE, 0.4);
  for (let i = 0; i < 12; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.35) continue;
    const x = -14 - rand(o.seed, i + 10) * 60 - age * 60, y = (rand(o.seed, i + 20) - 0.5) * 30 + age * 22;
    p.dot(x, y, age < 0.14 ? WHITE : i % 2 ? C[4] : S[2], 1 - age / 0.35);
    if (i % 4 === 0) { p.dot(x - 1, y, C[4], 1 - age / 0.35); p.dot(x + 1, y, C[4], 1 - age / 0.35); p.dot(x, y - 1, C[4], 1 - age / 0.35); p.dot(x, y + 1, C[4], 1 - age / 0.35); }
    p.light(x, y, C[3], 0.5);
  }
}
