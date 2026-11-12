import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Bat flight",
  desc: "Five bats in a ragged wedge, drawn in place so the game moves them: scalloped wings beating out of step, red eyes, a wake of violet dusk behind.",
  tags: ["shadow", "projectile", "creature", "one-shot"],
  size: [120, 48],
  origin: [98, 25],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0a0614", "#1c1030", "#3a1c68", "#6b2cc0", "#9a48ec", "#c684ff", "#ecc8ff"],
    eye: "#ff3a4a",
    seed: 173,
  },
};

export function bat(p, x, y, s, flap, dir, C, eye, a = 1) {
  const lift = flap * 6 * s, fold = 1 - Math.abs(flap) * 0.22;
  for (const side of [-1, 1]) {
    const wx = (px) => x + side * px * s * fold, sh = [x + side * 1.2 * s, y - 0.5 * s];
    const wrist = [wx(5), y - 2.6 * s - lift * 0.7], tip = [wx(11), y - 0.5 * s - lift], m1 = [wx(8), y + 2.6 * s - lift * 0.45], m2 = [wx(4.4), y + 3.4 * s - lift * 0.15];
    const pts = [sh, wrist, tip, [wx(9), y + 0.8 * s - lift * 0.7], m1, [wx(6), y + 1.4 * s - lift * 0.35], m2, [wx(2.4), y + 1.8 * s], [x, y + 2.2 * s]];
    p.poly(pts.map(([px, py]) => [px + (px > x ? 0.8 : -0.8), py + 0.8]), C[0], a);
    p.poly(pts, side * dir > 0 ? C[2] : C[3], a);
    p.line(wrist[0], wrist[1], m1[0], m1[1], side * dir > 0 ? C[3] : C[4], a);
    p.line(wrist[0], wrist[1], m2[0], m2[1], side * dir > 0 ? C[3] : C[4], a);
    p.line(sh[0], sh[1], wrist[0], wrist[1], C[5], a);
    p.line(wrist[0], wrist[1], tip[0], tip[1], C[5], a);
    p.dot(tip[0], tip[1], C[6], a);
    p.dot(wrist[0], wrist[1], C[6], a);
  }
  p.ellipse(x, y + 0.3 * s, 2 * s, 2.8 * s, C[1], a);
  p.circle(x + dir * 0.6 * s, y - 2.6 * s, 1.9 * s, C[1], a);
  p.tri(x + dir * 0.6 * s - 2.2 * s, y - 3 * s, x + dir * 0.6 * s - 0.6 * s, y - 3.6 * s, x + dir * 0.6 * s - 1.8 * s, y - 5.8 * s, C[2], a);
  p.tri(x + dir * 0.6 * s + 2.2 * s, y - 3 * s, x + dir * 0.6 * s + 0.6 * s, y - 3.6 * s, x + dir * 0.6 * s + 1.8 * s, y - 5.8 * s, C[2], a);
  p.dot(x + dir * 1.5 * s, y - 2.8 * s, eye, a);
  p.dot(x + dir * 1.5 * s - dir * Math.max(1.6, 1.6 * s), y - 2.8 * s, eye, a);
  p.light(x + dir * 1 * s, y - 2.8 * s, eye, 0.9 * a);
}

const WEDGE = [[-2, 0, 1.3], [-24, -11, 1.05], [-27, 10, 1], [-48, -3, 0.9], [-62, 12, 0.75]];

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), eye = hex(o.eye), u = clamp(t / FLY);
  for (let k = 0; k < 4; k++) {
    let px = -6, py = (k - 1.5) * 7;
    for (let i = 1; i <= 14; i++) {
      const d = i / 14, x = -6 - d * (80 - Math.abs(k - 1.5) * 10), y = (k - 1.5) * (7 + d * 4) + Math.sin(d * 5 - u * 10 + k * 2.1) * 4 * d;
      p.beam(px, py, x, y, Math.max(1, 2.4 * (1 - d)), d < 0.3 ? C[3] : d < 0.6 ? C[2] : C[1], 0.6 - d * 0.55);
      px = x; py = y;
    }
  }
  p.glow(-10, 0, 26, C[3], 0.35);
  for (let i = WEDGE.length - 1; i >= 0; i--) {
    const [bx, by, s] = WEDGE[i], ph = u * Math.PI * 4 + i * 1.7, bob = Math.sin(ph * 0.5 + i) * 1.6;
    bat(p, bx + Math.sin(ph * 0.5) * 1.5, by + bob - Math.sin(ph) * 1.2, s, Math.sin(ph), 1, C, eye);
  }
  for (let i = 0; i < 9; i++) {
    const born = rand(o.seed, i) * 0.8, age = u - born;
    if (age < 0 || age > 0.35) continue;
    const x = -14 - rand(o.seed, i + 10) * 50 - age * 60, y = (rand(o.seed, i + 20) - 0.5) * 30 + age * 10;
    p.dot(x, y, age < 0.12 ? C[6] : C[4], 1 - age / 0.35);
    p.light(x, y, C[4], 0.5);
  }
}
