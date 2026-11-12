import { fit, clamp, rand, hex, sparkle, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Glass shard",
  desc: "A long shard of stained glass, drawn in place so the game moves it: four leaded panes taking the light in turn, a ribbon of the whole spectrum behind, chips tumbling off.",
  tags: ["glass", "projectile", "one-shot"],
  size: [124, 44],
  origin: [102, 22],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#e8324a", "#f0a020", "#f4e04a", "#3ac86a", "#32b4e8", "#5a5ae8", "#b44ae0", "#f05aa8"],
    lead: "#141020",
    shine: "#fff8e0",
    seed: 171,
  },
};

export function draw(p, t, o) {
  if (t >= FLY) return;
  const J = o.palette.map(hex), LD = hex(o.lead), LT = hex(o.shine), u = clamp(t / FLY), grow = clamp(u / 0.2), n = J.length;
  p.glow(0, 0, 18, LT, 0.4);
  for (let k = 0; k < 7; k++) {
    const off = (k - 3) * 1.6, L = (74 - Math.abs(k - 3) * 6) * grow, c = J[(k + 1) % n];
    for (let d = 0; d < L; d += 1) {
      const f = d / L, x = -16 - d, y = off * (1 + f * 1.6) + Math.sin(f * 5 - u * 9) * 3 * f;
      p.dot(x, y, f < 0.12 ? LT : c, 1 - f * 0.9);
      if (k === 3 && d % 9 === 0) p.light(x, y, c, 0.5 * (1 - f));
    }
  }
  const roll = u * 7, tip = [16, 0], tail = [-18, 0], top = [1, -5.5], bot = [1, 5.5], mid = [-2, 0];
  const pane = (i) => (Math.sin(roll + i * 1.6) > 0.55 ? LT : J[(i * 2 + Math.floor(u * 3)) % n]);
  p.poly([[18.5, 0], [1, -7.2], [-21, 0], [1, 7.2]], LD, 1);
  p.poly([tip, top, mid], pane(0), 1);
  p.poly([top, tail, mid], pane(1), 1);
  p.poly([tip, bot, mid], pane(2), 1);
  p.poly([bot, tail, mid], pane(3), 1);
  p.line(tail[0], 0, tip[0], 0, LD, 1);
  p.line(top[0], top[1], bot[0], bot[1], LD, 1);
  p.line(4, -3, 11, -1, WHITE, 0.9);
  sparkle(p, 16, 0, 4 + Math.sin(u * 28) * 1.5, LT, WHITE, 0.6);
  for (let i = 0; i < 9; i++) {
    const born = rand(o.seed, i) * 0.75, age = u - born;
    if (age < 0 || age > 0.4) continue;
    const x = -12 - rand(o.seed, i + 10) * 30 - age * 70, y = (rand(o.seed, i + 20) - 0.5) * 22 + age * age * 60, a = age * 22 + i, c = J[i % n];
    p.poly([[x + Math.cos(a) * 3, y + Math.sin(a) * 3], [x - Math.sin(a) * 1.6, y + Math.cos(a) * 1.6], [x - Math.cos(a) * 3, y - Math.sin(a) * 3], [x + Math.sin(a) * 1.6, y - Math.cos(a) * 1.6]], Math.sin(a * 2) > 0.5 ? LT : c, 1 - age / 0.4);
    p.light(x, y, c, 0.6);
  }
}
