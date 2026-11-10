import { fit, clamp, WHITE } from "../../core/index.js";

export const meta = {
  name: "Pellet",
  desc: "A single hot pellet streaking right: a white head and a gold-to-red streak that grows from the muzzle as it flies, never reaching back past where it was fired.",
  tags: ["projectile", "pellet", "tracer", "gun", "directional", "one-shot"],
  size: [80, 16],
  origin: [70, 8],
  duration: 0.3,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6"],
    length: 26,
    speed: 1200,
    seed: 1,
  },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7), tail = Math.min(o.length, t * o.speed);
  if (tail < 1) return;
  p.glowLine(-tail, 0, 0, 0, 4, C[4], 0.35);
  p.line(-tail, 0, 0, 0, (u) => (u > 0.85 ? WHITE : u > 0.55 ? C[6] : u > 0.25 ? C[4] : C[2]));
  p.px(0, 0, WHITE);
  p.glow(0, 0, 5, C[5], 0.5);
}
