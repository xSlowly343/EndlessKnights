import { fit, sparkle, WHITE } from "../../core/index.js";

const D = 1.6;

export const meta = {
  name: "Mystic glow",
  desc: "A staff crystal's living glow: a soft violet halo, a faint rune ring turning on a tilt and three motes orbiting through it, a glint every turn.",
  tags: ["aura", "staff", "crystal", "arcane", "loop"],
  size: [60, 60],
  origin: [30, 30],
  duration: D,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#1a0a2a", "#3a1a6a", "#6a3ac8", "#9a6aff", "#7ae0e0", "#d0fff8", "#ffffff"], seed: 1 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7), a = (t / D) * Math.PI * 2;
  p.glow(0, 0, 12 + 2 * Math.sin(a * 2), C[3], 0.35);
  for (let i = 0; i < 16; i++) {
    const b = a * 0.5 + (i / 16) * Math.PI * 2;
    if (i % 2) p.dot(Math.cos(b) * 9, Math.sin(b) * 3.5, C[4], 0.6);
  }
  for (let k = 0; k < 3; k++) {
    const b = a + (k * Math.PI * 2) / 3, x = Math.cos(b) * 8, y = Math.sin(b) * 3;
    p.px(x, y, Math.sin(b) > 0 ? C[5] : C[3]);
  }
  if ((t % (D / 2)) < 0.1) sparkle(p, 1, -2, 3, C[5], WHITE);
}
