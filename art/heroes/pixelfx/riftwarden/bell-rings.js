import { fit, clamp, sparkle, WHITE } from "../../core/index.js";

const D = 0.6;

export const meta = {
  name: "Bell rings",
  desc: "A bell tolls: three rings of sound pulse out from it, bright then thinning, with a glint on the bell at each stroke.",
  tags: ["sound", "bell", "rings", "pulse", "one-shot"],
  size: [120, 120],
  origin: [60, 60],
  duration: D,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#1a1a40", "#2c3a80", "#4a6ac8", "#86b8ff", "#c4e2ff", "#eef6ff", "#ffffff"], seed: 1 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7);
  for (let i = 0; i < 3; i++) {
    const s = t - i * 0.12;
    if (s < 0 || s > 0.4) continue;
    const u = s / 0.4, r = 4 + u * 40;
    p.ring(0, 0, r, 1, u < 0.3 ? WHITE : C[4], 1 - u);
    p.glowRing(0, 0, r, 4, C[3], 0.3 * (1 - u));
  }
  if (t < 0.1) sparkle(p, 0, 0, 6 * (1 - t / 0.1), C[5], WHITE);
}
