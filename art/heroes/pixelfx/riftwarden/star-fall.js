import { fit, clamp, rand, seg, ease, star, sparkle, WHITE } from "../../core/index.js";

const END = 0.6, HIT = 0.1;

export const meta = {
  name: "Star fall",
  desc: "A star drops out of the sky on a streak of light, stands a pillar of gold where it lands, throws a star-shaped shock and small stars that bounce away.",
  tags: ["holy", "impact", "star", "sky", "one-shot"],
  size: [120, 84],
  origin: [60, 68],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#4a2c08", "#86520c", "#c48412", "#f0b424", "#ffe066", "#fff6c4", "#ffffff"],
    seed: 42,
  },
};

export function draw(p, t, o) {
  if (t >= END) return;
  const C = fit(o.palette, 7), HY = -14;
  if (t < HIT) {
    const k = ease.inQuad(t / HIT), x = -24 + 24 * k, y = -50 + (HY + 50) * k;
    p.glowLine(x - 14, y - 22, x, y, 5, C[3], 0.5);
    p.beam(x - 16, y - 24, x, y, 3, (a, v) => (v < 0.4 ? WHITE : C[4]), (a) => a * a);
    p.poly(star(x, y, 3, 8, 5, t * 30), C[4], 1);
    p.poly(star(x, y, 1.8, 5, 5, t * 30), WHITE, 1);
    p.glow(x, y, 12, C[4], 0.5);
    return;
  }
  const s = t - HIT, pillar = clamp(1 - s / 0.22), shock = seg(s, 0, 0.3), fade = clamp(1 - s / 0.34);
  if (pillar > 0) {
    const w = 2 + 9 * pillar * pillar;
    p.glowLine(0, -62, 0, 0, w + 6, C[3], 0.5 * pillar);
    p.beam(0, -60, 0, -1, w, (a, v) => (v < 0.35 ? WHITE : v < 0.7 ? C[5] : C[4]), (a, v) => (0.35 + 0.65 * a) * (v < 0.7 ? 1 : pillar));
    p.ellipse(0, 0, 6 + 12 * (1 - pillar), 2, C[5], pillar);
  }
  if (fade > 0) {
    const r = 6 + ease.outQuart(shock) * 24, pts = star(0, HY, r * 0.5, r, 5, -Math.PI / 2 + s * 1.5);
    pts.push(pts[0]);
    p.polyline(pts, fade > 0.55 ? WHITE : fade > 0.25 ? C[4] : C[2], fade);
    for (let i = 0; i < 10; i += 2) p.light(pts[i][0], pts[i][1], C[4], 0.6 * fade);
    if (s < 0.1) sparkle(p, 0, HY, 13 * (1 - s / 0.1), C[5], WHITE);
  }
  for (let i = 0; i < 7; i++) {
    const a = -Math.PI / 2 + (i - 3) * 0.42 + (rand(o.seed, i) - 0.5) * 0.2, sp = 70 + rand(o.seed, i + 10) * 70;
    const life = 0.3 + rand(o.seed, i + 20) * 0.18;
    if (s > life) continue;
    let x = Math.cos(a) * sp * s, y = HY + Math.sin(a) * sp * s + 330 * s * s;
    if (y > -2) y = -2 - Math.abs(Math.sin((y + 2) * 0.35)) * 5 * (1 - s / life);
    const k = 1 - s / life, r = 1.5 + 2.5 * k;
    p.poly(star(x, y, r * 0.45, r, 4, s * 14 + i), i % 2 ? C[4] : C[5], 1);
    p.dot(x, y, WHITE, 1);
    p.light(x, y, C[3], 0.5 * k);
  }
  for (let i = 0; i < 8; i++) {
    const born = 0.05 + rand(o.seed, i + 40) * 0.25, age = s - born;
    if (age < 0 || age > 0.2) continue;
    const x = (rand(o.seed, i + 50) - 0.5) * 44, y = -4 - rand(o.seed, i + 60) * 40;
    sparkle(p, x, y, 2.5 * Math.sin((age / 0.2) * Math.PI), C[4], WHITE, 0.4);
  }
}
