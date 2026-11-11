import { fit, clamp, rand, star, sparkle, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Star seed",
  desc: "A small gold star tossed in an arc, drawn in place so the game moves it: it tumbles point over point and sheds a curl of glitter.",
  tags: ["holy", "projectile", "star", "one-shot"],
  size: [96, 48],
  origin: [78, 24],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#4a2c08", "#86520c", "#c48412", "#f0b424", "#ffe066", "#fff6c4", "#ffffff"],
    seed: 41,
  },
};

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), u = clamp(t / FLY), rot = u * Math.PI * 2.5, grow = clamp(u / 0.25);

  p.glow(0, 0, 13, C[3], 0.4);
  for (let i = 1; i <= 12; i++) {
    const d = i / 12;
    if (d > grow) break;
    const x = -4 - d * 56, y = Math.sin(d * 5 + u * 9) * 3 * d + d * d * 9, tw = rand(o.seed, i, Math.floor(t * 30)) > 0.5;
    if (i % 3 === 0) sparkle(p, x, y, tw ? 3 : 2, C[4], WHITE, 0.4);
    else p.dot(x, y, d < 0.4 ? C[5] : d < 0.75 ? C[4] : C[2], 1 - d * 0.6);
    p.light(x, y, C[3], 0.3);
  }
  p.poly(star(0, 0, 3.4, 8, 4, rot), C[1], 1);
  p.poly(star(0, 0, 2.6, 7, 4, rot), C[4], 1);
  p.poly(star(0, 0, 1.6, 4.5, 4, rot), C[5], 1);
  p.poly(star(0, 0, 1, 2.4, 4, rot + Math.PI / 4), WHITE, 1);
  p.dot(Math.cos(rot) * 7, Math.sin(rot) * 7, WHITE, 1);
}
