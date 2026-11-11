import { fit, sparkle, clamp, rand, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Void crescent",
  desc: "A crescent torn off a blade in flight, drawn in place so the game moves it: it thins as it goes, drags echo arcs, speed lines and motes behind it.",
  tags: ["melee", "slash", "projectile", "wave", "directional", "one-shot"],
  size: [112, 64],
  origin: [84, 32],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#06281c", "#0c6a48", "#20ca7b", "#6ffd92", "#d0f7d6", "#f2fef4", "#ffffff"],
    radius: 15,
    seed: 3,
  },
};

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), u = clamp(t / FLY), RAD = o.radius;
  const r = RAD * (1 - 0.3 * u), sw = 0.5 + 0.12 * u;
  const a0 = -Math.PI * sw, a1 = Math.PI * sw;
  const grow = clamp(u / 0.2), wake = 18 + 46 * grow;

  p.glow(-r * 0.4, 0, r + 9, C[2], 0.3);
  p.glow(0, 0, 6, C[3], 0.3);

  for (let i = 0; i < 9; i++) {
    const y = (rand(o.seed, i) - 0.5) * 2 * r * 0.9;
    const len = (0.35 + 0.65 * rand(o.seed, i + 20)) * wake;
    const lag = 6 + rand(o.seed, i + 40) * 10;
    const edge = r - Math.sqrt(Math.max(0, r * r - y * y));
    const x1 = -edge - lag, x0 = x1 - len * (0.6 + 0.4 * Math.sin((u + rand(o.seed, i + 60)) * Math.PI));
    p.line(x0, y, x1, y, (v) => (v > 0.75 ? C[4] : v > 0.4 ? C[3] : C[2]), (v) => 0.15 + 0.7 * v);
  }

  for (let k = 3; k >= 1; k--) {
    p.arc(-r - k * 6 * grow - 2, 0, r - k * 1.5, 1, a0 * (0.85 - k * 0.12), a1 * (0.85 - k * 0.12), C[k === 1 ? 3 : 2], 0.75 - k * 0.2);
  }

  p.arc(-r - 2, 0, r, (v) => 1 + 3 * Math.sin(Math.PI * v), a0 * 0.92, a1 * 0.92, C[2], 1);
  p.arc(-r, 0, r, (v) => 1 + 5 * Math.sin(Math.PI * v), a0, a1, C[3], 1);
  p.arc(-r + 1, 0, r, (v) => 3 * Math.sin(Math.PI * v), a0 * 0.8, a1 * 0.8, (v) => (Math.abs(v - 0.5) < 0.22 ? WHITE : C[5]), 1);
  p.arc(-r - 3, 0, r, 1, a0 * 0.95, a1 * 0.95, C[1], 0.9);
  p.glowLine(-r * 0.2, -r * 0.8, -r * 0.2, r * 0.8, C[3], 0.35);

  for (let i = 0; i < 6; i++) {
    const born = rand(o.seed, i + 80) * 0.7, age = u - born;
    if (age < 0 || age > 0.45) continue;
    const y = (rand(o.seed, i + 100) - 0.5) * 2 * r, x = -6 - age * 150;
    p.dot(x, y + age * 8, age < 0.2 ? C[5] : C[3], 1 - age / 0.45);
    p.light(x, y, C[3], 0.3);
  }

  if (u < 0.3) sparkle(p, 1, 0, 5 * (1 - u / 0.3), C[5], WHITE);
}
