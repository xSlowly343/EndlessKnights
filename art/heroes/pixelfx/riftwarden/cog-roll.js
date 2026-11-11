import { fit, clamp, rand, TAU, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Cog roll",
  desc: "A brass cog bowled along the ground, drawn in place so the game moves it: teeth biting the floor, sparks kicked up behind, a scored rut and ghosts of where it just was.",
  tags: ["metal", "projectile", "ground", "one-shot"],
  size: [112, 48],
  origin: [90, 38],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#1e1206", "#4a2c08", "#86520c", "#c48412", "#f0b424", "#ffe066", "#fff6c4"],
    radius: 8,
    seed: 61,
  },
};

export function cog(p, x, y, R, rot, C, teeth = 8, a = 1) {
  for (let i = 0; i < teeth; i++) {
    const g = rot + (i / teeth) * TAU, ca = Math.cos(g), sa = Math.sin(g), lit = ca + sa < -0.3;
    p.beam(x + ca * (R - 1), y + sa * (R - 1), x + ca * (R + 1.8), y + sa * (R + 1.8), Math.max(1.6, R * 0.38), lit ? C[5] : ca + sa > 0.6 ? C[2] : C[3], a);
  }
  p.circle(x, y, R, (d, g) => (d > 0.82 ? C[2] : Math.cos(g) + Math.sin(g) < -0.5 ? C[4] : C[3]), a);
  if (R >= 5) {
    p.arc(x, y, R, 1, Math.PI * 0.95, Math.PI * 1.6, C[5], a);
    p.circle(x, y, R * 0.55, C[1], a);
    for (let i = 0; i < 4; i++) {
      const g = rot + (i / 4) * TAU;
      p.line(x, y, x + Math.cos(g) * R * 0.6, y + Math.sin(g) * R * 0.6, C[3], a);
    }
    p.circle(x, y, R * 0.24, C[4], a);
    p.dot(x - 1, y - 1, C[6], a);
  } else {
    p.dot(x, y, C[0], a);
    p.dot(x - 1, y - 1, C[5], a);
  }
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), u = clamp(t / FLY), R = o.radius, cy = -R - 2 - Math.abs(Math.sin(u * TAU * 1.5)) * 1.5;
  const rot = u * TAU * 1.4, grow = clamp(u / 0.2);

  for (let x = -6; x > -6 - 70 * grow; x -= 1) if ((Math.floor(x) & 3) !== 0) p.dot(x, 0, C[1], 0.9 + x / 90);
  for (let k = 3; k >= 1; k--) {
    const gx = -k * 9 * grow;
    p.arc(gx, cy, R + 1, 1, 1.9, 4.4, C[k === 1 ? 3 : 2], 0.5 - k * 0.12);
  }
  p.glow(0, cy, R + 9, C[3], 0.3);
  cog(p, 0, cy, R, rot, C);
  p.glow(-1, 0, 6, C[5], 0.6);
  p.dot(0, -1, WHITE, 1);
  for (let i = 0; i < 12; i++) {
    const born = rand(o.seed, i) * 0.85, age = u - born;
    if (age < 0 || age > 0.35) continue;
    const a = Math.PI + 0.25 + rand(o.seed, i + 20) * 0.9, sp = 70 + rand(o.seed, i + 40) * 110, s = age * FLY;
    const x = -2 + Math.cos(a) * sp * s * 3, y = -1 + Math.sin(a) * sp * s * 3 + 500 * s * s * 4;
    if (y > 0) continue;
    p.line(x, y, x + 2.5, y + 1.2, age < 0.12 ? WHITE : age < 0.24 ? C[5] : C[3], 1 - age / 0.35);
    p.light(x, y, C[4], 0.5);
  }
}
