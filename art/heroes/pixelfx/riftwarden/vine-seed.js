import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Vine seed",
  desc: "A snapping flytrap bud flies on a whip of vine, drawn in place so the game moves it: jaws chattering, tendrils curling behind, leaves and pollen torn loose in its wake.",
  tags: ["nature", "projectile", "creature", "one-shot"],
  size: [120, 46],
  origin: [100, 24],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0c1a0e", "#1c3c18", "#2f6a22", "#52a02e", "#8cd040", "#c8f070", "#f4ffc0"],
    mouth: ["#4a0a3a", "#a81a7a", "#f04ab4", "#ffb0e0"],
    seed: 183,
  },
};

export function maw(p, x, y, ang, s, open, C, M, a = 1) {
  const ca = Math.cos(ang), sa = Math.sin(ang);
  const lobe = (side) => {
    const tilt = side * (0.12 + open * 0.62), ct = Math.cos(tilt), st = Math.sin(tilt);
    const at = (px, py) => { const rx = px * ct - py * st, ry = px * st + py * ct; return [x + (rx * ca - ry * sa) * s, y + (rx * sa + ry * ca) * s]; };
    const rim = [], back = [];
    for (let i = 0; i <= 8; i++) { const u = i / 8; back.push(at(-3 + u * 16, side * (1 + Math.sin(u * Math.PI) * 6.4 + (1 - u) * 1.5))); rim.push(at(-3 + u * 16, side * 0.6)); }
    const pts = [...rim, ...back.reverse()];
    p.poly(pts.map(([px, py]) => [px + (px - x) * 0.1, py + (py - y) * 0.1 + side * 0.6]), C[0], a);
    p.poly(pts, (px, py) => {
      const lx = ((px - x) * ca + (py - y) * sa) / s, ly = (-(px - x) * sa + (py - y) * ca) / s, rx = lx * ct + ly * st, ry = (-lx * st + ly * ct) * side;
      if (ry < 2.2 && open > 0.2) return rx > 9 ? M[1] : M[2];
      const spot = Math.sin(rx * 1.9 + side) * Math.cos(ry * 2.1) > 0.55;
      return spot ? C[2] : ry > 5.4 ? (side < 0 ? C[5] : C[2]) : ry > 3.4 ? (side < 0 ? C[4] : C[3]) : C[3];
    }, a);
    for (let i = 0; i < 6; i++) {
      const u = 0.12 + i * 0.16, b = at(-3 + u * 16, side * 0.4), tip = at(-3 + u * 16 + 0.8, -side * (2.2 + (i % 2) * 1.4));
      p.line(b[0], b[1], tip[0], tip[1], i % 2 ? C[6] : WHITE, a);
    }
    const e0 = at(13, side * 0.6), e1 = at(-3, side * 0.6);
    p.line(e1[0], e1[1], e0[0], e0[1], C[5], a);
  };
  if (open > 0.25) { p.glow(x + ca * 5 * s, y + sa * 5 * s, 9 * s, M[2], 0.45 * a * open); }
  lobe(1);
  lobe(-1);
  p.circle(x - ca * 3 * s, y - sa * 3 * s, 2.6 * s, C[0], a);
  p.circle(x - ca * 3 * s, y - sa * 3 * s, 1.8 * s, C[3], a);
}

export function leaf(p, x, y, ang, L, C, a = 1) {
  const ca = Math.cos(ang), sa = Math.sin(ang), w = L * 0.3;
  p.poly([[x, y], [x + ca * L * 0.45 - sa * w, y + sa * L * 0.45 + ca * w], [x + ca * L, y + sa * L], [x + ca * L * 0.45 + sa * w, y + sa * L * 0.45 - ca * w]], (px, py) => ((px - x) * -sa + (py - y) * ca > 0 ? C[3] : C[4]), a);
  p.line(x, y, x + ca * L, y + sa * L, C[5], a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), M = fit(o.mouth, 4), u = clamp(t / FLY), bob = Math.sin(u * Math.PI * 2) * 1.6;
  let px = -5, py = bob;
  for (let i = 1; i <= 26; i++) {
    const d = i / 26, x = -5 - d * 78, y = bob * (1 - d) + Math.sin(d * 9 - u * 12) * 6 * Math.sin(d * Math.PI);
    p.beam(px, py, x, y, 4.6 * (1 - d * 0.7) + 1.6, C[0], 1);
    px = x; py = y;
  }
  px = -5; py = bob;
  for (let i = 1; i <= 26; i++) {
    const d = i / 26, x = -5 - d * 78, y = bob * (1 - d) + Math.sin(d * 9 - u * 12) * 6 * Math.sin(d * Math.PI), w = 4.6 * (1 - d * 0.7);
    p.beam(px, py, x, y, w, (q, v) => (v < 0.35 ? C[4] : i % 4 === 0 ? C[2] : C[3]), 1);
    if (i % 5 === 2) { leaf(p, x, y, -1.9 - Math.sin(d * 9 - u * 12) * 0.5, 8 - d * 3, C); leaf(p, x, y, 2 + Math.sin(d * 9 - u * 12) * 0.5, 7 - d * 3, C); }
    if (i % 5 === 4) { p.line(x, y, x - 1.5, y - 4 + d * 2, C[5], 1); p.line(x, y, x - 1.5, y + 4 - d * 2, C[5], 1); }
    px = x; py = y;
  }
  for (let k = 0; k < 2; k++) {
    let qx = -14, qy = bob + (k ? 3 : -3);
    for (let i = 1; i <= 10; i++) {
      const g = i * 0.75 + u * 8 + k * 3, r = 5 * (1 - i / 11), x = -14 - i * 2.2 + Math.cos(g) * r, y = bob + (k ? 6 : -6) + Math.sin(g) * r;
      p.line(qx, qy, x, y, i < 4 ? C[5] : C[4], 1);
      qx = x; qy = y;
    }
  }
  p.glow(2, bob, 18, C[3], 0.3);
  maw(p, 0, bob, Math.sin(u * Math.PI * 2) * 0.08, 1.05, 0.5 + 0.5 * Math.sin(u * Math.PI * 4 - 0.6), C, M);
  for (let i = 0; i < 12; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.35) continue;
    const x = -8 - rand(o.seed, i + 10) * 50 - age * 70, y = (rand(o.seed, i + 20) - 0.5) * 26 + age * 14;
    if (i % 3 === 0) leaf(p, x, y, age * 20 + i, 5, C, 1 - age / 0.35);
    else { p.dot(x, y, i % 3 === 1 ? C[6] : M[3], 1 - age / 0.35); p.light(x, y, i % 3 === 1 ? C[4] : M[2], 0.5); }
  }
}
