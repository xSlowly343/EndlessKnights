import { fit, clamp, rand, seg, ease, hex, WHITE } from "../../core/index.js";

const END = 0.6;

export const meta = {
  name: "Bramble bloom",
  desc: "The bramble comes up under its target: thorny canes whip out of the ground, a rose opens at the top, then it all lets go in a fall of petals.",
  tags: ["nature", "impact", "ground", "one-shot"],
  size: [112, 76],
  origin: [56, 62],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0c1a0e", "#163418", "#225224", "#357530", "#4f9a3a", "#7cc048", "#b4e070"],
    rose: ["#4a0a1e", "#a01840", "#e0386a", "#ff86a8", "#ffd2e0"],
    soil: ["#33281c", "#6e5a40", "#b89f78"],
    seed: 82,
  },
};

const CANES = [[-12, 26, -16, 0], [-6, 36, -6, 0.02], [0, 46, 1, 0], [7, 34, 9, 0.03], [13, 24, 17, 0.01]];

export function draw(p, t, o) {
  if (t >= END) return;
  const C = fit(o.palette, 7), R = o.rose.map(hex), S = o.soil.map(hex);
  const wilt = seg(t, 0.34, 0.56), alive = 1 - wilt;
  for (let i = 0; i < 10; i++) {
    const s = t / 0.22;
    if (s > 1) break;
    const a = -Math.PI / 2 + (rand(o.seed, i) - 0.5) * 2.2, sp = 20 + rand(o.seed, i + 10) * 22;
    const x = (i - 4.5) * 3 + Math.cos(a) * sp * s, y = Math.sin(a) * sp * s + 40 * s * s;
    if (y < 0) p.dot(x, y, S[i % 3], 1);
  }
  let top = [0, 0];
  CANES.forEach(([bx, H, lean, delay], i) => {
    const g = ease.outBack(seg(t, delay, delay + 0.14), 1.2) * (1 - 0.25 * wilt);
    if (g <= 0) return;
    const at = (s) => [bx + lean * s * s + Math.sin(s * 5 + i) * 2.2, -H * s * g - (wilt * wilt * -8) * s];
    let prev = at(0);
    for (let k = 1; k <= 14; k++) {
      const s = k / 14, q = at(s);
      p.line(prev[0] + 1, prev[1], q[0] + 1, q[1], C[1], alive);
      p.line(prev[0], prev[1], q[0], q[1], s > 0.6 ? C[4] : C[3], alive);
      if (k % 3 === 0 && k < 14) {
        const side = (k / 3 + i) % 2 ? 1 : -1;
        p.tri(q[0], q[1] - 1, q[0], q[1] + 2, q[0] + side * (4 + 2 * g), q[1] - 2, C[0], alive);
        p.tri(q[0], q[1], q[0], q[1] + 1, q[0] + side * (3 + 2 * g), q[1] - 2, C[5], alive);
      }
      if (k === 7 || k === 11) p.ellipse(q[0] - (i % 2 ? 3 : -3), q[1], 2.5, 1.4, C[5], alive);
      prev = q;
    }
    if (i === 2) top = prev;
  });
  const open = ease.outBack(seg(t, 0.12, 0.24), 2.2), burst = seg(t, 0.3, 0.36);
  if (open > 0 && burst < 1) {
    const r = 8 * open * (1 + 0.25 * burst), [x, y] = top, a = 1 - burst;
    p.glow(x, y, r + 9, R[2], 0.45 * a);
    p.circle(x, y, r + 1, R[0], a);
    p.circle(x, y, r, R[1], a);
    p.arc(x, y, r, 2.5, 2.4, 5.6, R[3], a);
    p.arc(x, y, r, 2, -0.6, 1.6, R[2], a);
    p.arc(x, y, r * 0.62, 2, 0.4, 3.4, R[3], a);
    p.arc(x, y, r * 0.62, 1.5, 3.8, 6, R[4], a);
    p.circle(x, y, r * 0.22, R[0], a);
    p.dot(x - r * 0.4, y - r * 0.55, WHITE, a);
  }
  if (t > 0.3) {
    const s = t - 0.3, [x0, y0] = top;
    for (let i = 0; i < 14; i++) {
      const life = 0.18 + rand(o.seed, i + 30) * 0.1;
      if (s > life) continue;
      const a = rand(o.seed, i + 40) * 6.28, r = (10 + rand(o.seed, i + 50) * 16) * ease.outCubic(clamp(s / 0.12));
      const x = x0 + Math.cos(a) * r + Math.sin(s * 22 + i) * 2, y = y0 + Math.sin(a) * r * 0.7 + 130 * s * s, f = Math.sin(s * 30 + i) > 0;
      p.tri(x - 2, y, x + 2, y - (f ? 1 : 2), x, y + (f ? 1 : 2), i % 3 ? R[3] : R[2], 1 - s / life);
      p.dot(x, y, R[4], 1 - s / life);
    }
  }
}
