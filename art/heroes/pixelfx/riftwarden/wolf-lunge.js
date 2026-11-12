import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Wolf lunge",
  desc: "A spirit wolf at full stretch, drawn in place so the game moves it: ears flat, jaws opening as it closes, the body streaming out behind as ribbons of pale mist.",
  tags: ["spirit", "projectile", "creature", "one-shot"],
  size: [124, 52],
  origin: [100, 28],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#061422", "#0e3a5a", "#1c7a9c", "#4cc4d8", "#a4f0f0", "#e8ffff", "#ffffff"],
    eye: "#ffe066",
    seed: 151,
  },
};

const SKULL = [[13, -1], [11, -3.5], [5, -5], [2, -7], [-1, -12], [-3.5, -6.5], [-6, -11], [-8, -5], [-12, -2], [-12, 4], [-4, 3], [3, 1.5], [12, 1.5]];
const JAW = [[-4, 2], [10, 2.5], [9, 5], [-2, 6.5], [-8, 5]];

export function wolfHead(p, x, y, ang, s, open, W, eye, a = 1, dark = false) {
  const ca = Math.cos(ang), sa = Math.sin(ang);
  const at = ([px, py]) => [x + (px * ca - py * sa) * s, y + (px * sa + py * ca) * s];
  const turn = (pts, hinge, by) => pts.map(([px, py]) => {
    const dx = px - hinge[0], dy = py - hinge[1];
    return [hinge[0] + dx * Math.cos(by) - dy * Math.sin(by), hinge[1] + dx * Math.sin(by) + dy * Math.cos(by)];
  });
  const jaw = turn(JAW, [-4, 2], open * 0.75), skull = turn(SKULL, [-4, 2], -open * 0.22);
  const grow = (pts, k) => pts.map(([px, py]) => [px * k + (px > 0 ? 0.6 : -0.6) * (k - 1) * 4, py * k]);
  p.poly(grow(jaw, 1.12).map(at), dark ? W[4] : W[1], a);
  p.poly(grow(skull, 1.12).map(at), dark ? W[4] : W[1], a);
  p.poly(jaw.map(at), dark ? W[0] : W[2], a);
  p.poly(skull.map(at), (px, py) => {
    const ly = (-(px - x) * sa + (py - y) * ca) / s;
    return dark ? (ly < -4 ? W[1] : W[0]) : ly < -3.5 ? W[5] : ly < 0 ? W[4] : W[3];
  }, a);
  const line = (a0, b0, c) => { const A = at(a0), B = at(b0); p.line(A[0], A[1], B[0], B[1], c, a); };
  line(skull[3], skull[4], dark ? W[5] : WHITE);
  line(skull[5], skull[6], W[5]);
  line(skull[1], skull[2], dark ? W[5] : WHITE);
  line(skull[7], skull[8], dark ? W[4] : W[5]);
  for (const fx of [9.5, 6.5, 3.5]) {
    const up = turn([[fx, 1.5], [fx - 0.8, 4]], [-4, 2], -open * 0.22), dn = turn([[fx - 1.2, 2.6], [fx - 0.6, 0.4]], [-4, 2], open * 0.75);
    if (open > 0.15) { line(up[0], up[1], WHITE); line(dn[0], dn[1], WHITE); }
  }
  const e = at(turn([[4.5, -3.2]], [-4, 2], -open * 0.22)[0]), n = at(turn([[12.5, -1.5]], [-4, 2], -open * 0.22)[0]);
  p.circle(e[0], e[1], 1.5 * s, W[0], a);
  p.dot(e[0], e[1], eye, a);
  p.dot(e[0] + ca, e[1] + sa, WHITE, a);
  p.glow(e[0], e[1], 5, eye, 0.7 * a);
  p.dot(n[0], n[1], W[0], a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const W = fit(o.palette, 7), u = clamp(t / FLY), grow = clamp(u / 0.2), open = clamp((u - 0.35) / 0.5), bob = Math.sin(u * Math.PI * 2) * 1.5;
  const eye = hex(o.eye);
  p.glow(0, bob, 22, W[2], 0.4);
  for (let k = 0; k < 5; k++) {
    let px = -10, py = bob + (k - 2) * 2.2;
    const L = (70 - Math.abs(k - 2) * 12) * grow;
    for (let i = 1; i <= 16; i++) {
      const d = i / 16, x = -10 - d * L, y = bob * (1 - d) + (k - 2) * (2.2 + d * 5) + Math.sin(d * 6 - u * 12 + k * 1.7) * 3.4 * d;
      p.beam(px, py, x, y, Math.max(1, (5 - Math.abs(k - 2) * 1.2) * (1 - d)), d < 0.2 ? W[4] : d < 0.5 ? W[3] : d < 0.8 ? W[2] : W[1], 1 - d * 0.85);
      px = x; py = y;
    }
  }
  for (let leg = 0; leg < 2; leg++) {
    const reach = Math.sin(u * Math.PI * 2 + leg * 1.1) * 0.5 + 0.5, sx = -6 - leg * 4, sy = bob + 4;
    const kx = sx + 6 + reach * 6, ky = sy + 5 - reach * 2, fx = kx + 6 + reach * 3, fy = ky + 2 + (1 - reach) * 3;
    p.beam(sx, sy, kx, ky, 3, leg ? W[2] : W[3], leg ? 0.7 : 1);
    p.beam(kx, ky, fx, fy, 2, leg ? W[2] : W[4], leg ? 0.7 : 1);
    p.dot(fx + 1, fy, WHITE, 1);
  }
  wolfHead(p, 0, bob, -0.08 + open * 0.06, 1, open, W, eye);
  for (let i = 0; i < 10; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.35) continue;
    const x = -10 - rand(o.seed, i + 10) * 40 - age * 70, y = (rand(o.seed, i + 20) - 0.5) * 24 - age * 12;
    p.dot(x, y, age < 0.14 ? WHITE : W[4], 1 - age / 0.35);
    p.light(x, y, W[3], 0.5);
  }
}
