import { fit, clamp, rand, seg, ease, TAU, WHITE } from "../../core/index.js";
import { cog } from "./cog-roll.js";

const END = 0.6;

export const meta = {
  name: "Cog break",
  desc: "The cog strikes and its clockwork lets go: a clock face flashes with hands spinning wild, then small cogs and a spring bounce off across the floor.",
  tags: ["metal", "impact", "ground", "one-shot"],
  size: [128, 72],
  origin: [64, 58],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#1e1206", "#4a2c08", "#86520c", "#c48412", "#f0b424", "#ffe066", "#fff6c4"],
    seed: 62,
  },
};

function hop(x0, y0, vx, vy, t) {
  let x = x0, y = y0, n = Math.round(t * 240);
  for (let i = 0; i < n; i++) {
    vy += 1900 / 240;
    x += vx / 240;
    y += vy / 240;
    if (y > -3 && vy > 0) { y = -3; vy *= -0.55; vx *= 0.75; }
  }
  return [x, y];
}

export function draw(p, t, o) {
  if (t >= END) return;
  const C = fit(o.palette, 7), CY = -12, face = clamp(1 - t / 0.26);
  if (face > 0) {
    const r = 9 + 6 * ease.outQuart(seg(t, 0, 0.1)), hot = t < 0.06;
    p.glow(0, CY, 24, C[4], 0.5 * face);
    p.ring(0, CY, r, 1.5, hot ? WHITE : face > 0.4 ? C[5] : C[3], face);
    p.ring(0, CY, r - 3, 1, C[2], 0.6 * face);
    for (let i = 0; i < 12; i++) {
      const g = (i / 12) * TAU, l = i % 3 === 0 ? 3 : 1.5;
      p.line(Math.cos(g) * (r - l - 1), CY + Math.sin(g) * (r - l - 1), Math.cos(g) * (r - 1), CY + Math.sin(g) * (r - 1), hot ? WHITE : C[5], face);
    }
    const h1 = -1.6 + t * 55, h2 = -1.6 + t * 13;
    p.line(0, CY, Math.cos(h1) * (r - 3), CY + Math.sin(h1) * (r - 3), WHITE, face);
    p.beam(0, CY, Math.cos(h2) * (r - 6), CY + Math.sin(h2) * (r - 6), 2, C[5], face);
    p.dot(0, CY, C[1], face);
    for (let i = 1; i <= 4; i++) {
      const g = h1 - i * 0.35;
      p.line(0, CY, Math.cos(g) * (r - 3), CY + Math.sin(g) * (r - 3), C[4], face * (0.5 - i * 0.1));
    }
  }
  const s = t - 0.04;
  if (s <= 0) return;
  for (let i = 0; i < 5; i++) {
    const dir = i % 2 ? -1 : 1, vx = dir * (40 + rand(o.seed, i) * 75), vy = -170 - rand(o.seed, i + 10) * 130;
    const life = 0.4 + rand(o.seed, i + 20) * 0.14;
    if (s > life) continue;
    const [x, y] = hop(0, CY, vx, vy, s), R = 2.5 + rand(o.seed, i + 30) * 2, k = clamp((life - s) / 0.12);
    cog(p, x, y, R, s * dir * 16 + i, C, R > 3.5 ? 6 : 5, k);
    p.light(x, y, C[4], 0.4 * k);
  }
  const sl = 0.44;
  if (s < sl) {
    const [x, y] = hop(0, CY, -30, -300, s), k = clamp((sl - s) / 0.12), tilt = s * 9;
    for (let j = 0; j < 7; j++) {
      const a0 = j - 3, a1 = j - 2, w = 2.5;
      const X = (a, side) => x + Math.cos(tilt) * a * 1.6 - Math.sin(tilt) * side * w;
      const Y = (a, side) => y + Math.sin(tilt) * a * 1.6 + Math.cos(tilt) * side * w;
      p.line(X(a0, j % 2 ? 1 : -1), Y(a0, j % 2 ? 1 : -1), X(a1, j % 2 ? -1 : 1), Y(a1, j % 2 ? -1 : 1), j % 2 ? C[5] : C[3], k);
    }
  }
  for (let i = 0; i < 10; i++) {
    const a = rand(o.seed, i + 50) * TAU, sp = 60 + rand(o.seed, i + 60) * 120, life = 0.14 + rand(o.seed, i + 70) * 0.16;
    if (s > life) continue;
    const x = Math.cos(a) * sp * s, y = CY + Math.sin(a) * sp * s + 300 * s * s;
    if (y > -1) continue;
    p.line(x, y, x - Math.cos(a) * 2.5, y - Math.sin(a) * 2.5, s < 0.08 ? WHITE : C[5], 1 - s / life);
  }
}
