import { fit, clamp, rand, hex } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Moth flock",
  desc: "A flight of pale moths, drawn in place so the game moves it: each one beats its wings out of step and weaves its own path, dusting the air behind.",
  tags: ["nature", "projectile", "swarm", "one-shot"],
  size: [120, 64],
  origin: [98, 32],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#241a3a", "#4a3a78", "#7d6cc0", "#b3a4ee", "#dcd2ff", "#f6f2ff"],
    dust: "#8ef0e0",
    seed: 51,
  },
};

export function moth(p, x, y, size, flap, C, a = 1) {
  const open = 0.2 + 0.8 * Math.abs(flap), w = size * open, h = size * (1.15 - 0.35 * open);
  for (const s of [-1, 1]) {
    p.poly([[x, y - 1], [x + s * (w + 1), y - h - 1], [x + s * (w + 2), y - h * 0.3], [x + s * (w * 0.6 + 1), y + h * 0.75 + 1], [x, y + 2]], C[1], a);
    p.poly([[x, y], [x + s * w, y - h], [x + s * (w + 1), y - h * 0.3], [x + s * w * 0.5, y + 1]], s < 0 ? C[4] : C[3], a);
    p.poly([[x, y + 1], [x + s * w * 0.8, y + 1], [x + s * w * 0.6, y + h * 0.75], [x, y + 1.5]], C[2], a);
    if (w > 2.4) {
      p.dot(x + s * w * 0.65, y - h * 0.55, C[5], a);
      p.dot(x + s * w * 0.55, y - h * 0.3, C[1], a);
    }
  }
  p.line(x, y - 2, x, y + 2, C[0], a);
  p.dot(x, y - 3, C[5], a);
  p.glow(x, y, size + 4, C[3], 0.35 * a);
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 6), D = hex(o.dust), u = clamp(t / FLY), grow = clamp(u / 0.2);
  p.glow(-18, 0, 22, C[2], 0.22);
  for (let i = 4; i >= 0; i--) {
    const lag = i === 0 ? 0 : (8 + i * 13 + rand(o.seed, i) * 5) * grow;
    const ph = rand(o.seed, i + 10) * 6.28, amp = i === 0 ? 2 : 3 + rand(o.seed, i + 20) * 5;
    const x = 2 - lag + Math.sin(u * 7 + ph) * 2, y = Math.sin(u * 8 + ph) * amp + (i % 2 ? -1 : 1) * (4 + rand(o.seed, i + 30) * 5) * (i ? 1 : 0);
    moth(p, x, y, i === 0 ? 6 : 3.5 + rand(o.seed, i + 40) * 1.5, Math.sin(t * 55 + ph), C);
    for (let k = 1; k <= 4; k++) {
      const age = k / 5, dx = x - k * 6 - rand(o.seed, i, k) * 4, dy = y + age * 7 + Math.sin(k + ph) * 2;
      if (dx < -88) continue;
      p.dot(dx, dy, k < 2 ? C[5] : k < 3 ? D : C[2], (1 - age) * grow);
    }
  }
}
