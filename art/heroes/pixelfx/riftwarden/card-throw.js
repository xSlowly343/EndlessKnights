import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Card throw",
  desc: "Three cards flicked from the hand, drawn in place so the game moves them: they spin edge over edge in a loose fan, each dragging a thin cut of air.",
  tags: ["trick", "projectile", "cards", "one-shot"],
  size: [112, 56],
  origin: [92, 28],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#2a1c3a", "#7a7090", "#c8c0d8", "#f4f0fa", "#ffffff"],
    suit: ["#5a0a1c", "#c41e3a", "#ff6a7a"],
    seed: 71,
  },
};

export function card(p, x, y, hw, hh, rot, flip, C, S, a = 1) {
  const w = Math.max(0.6, hw * Math.abs(flip)), ca = Math.cos(rot), sa = Math.sin(rot);
  const q = (dx, dy) => [x + dx * ca - dy * sa, y + dx * sa + dy * ca];
  p.poly([q(-w - 1, -hh - 1), q(w + 1, -hh - 1), q(w + 1, hh + 1), q(-w - 1, hh + 1)], C[0], a);
  if (flip >= 0) {
    p.poly([q(-w, -hh), q(w, -hh), q(w, hh), q(-w, hh)], C[3], a);
    p.poly([q(0, -hh), q(w, -hh), q(w, hh), q(0, hh)], C[2], a);
    if (w > 1.6) {
      p.poly([q(0, -2.2), q(1.6, 0), q(0, 2.2), q(-1.6, 0)], S[1], a);
      p.dot(q(-w + 1, -hh + 1)[0], q(-w + 1, -hh + 1)[1], S[1], a);
    }
  } else {
    p.poly([q(-w, -hh), q(w, -hh), q(w, hh), q(-w, hh)], S[1], a);
    if (w > 1.6) p.poly([q(-w + 1, -hh + 1), q(w - 1, -hh + 1), q(w - 1, hh - 1), q(-w + 1, hh - 1)], S[0], a);
    p.dot(x, y, S[2], a);
  }
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 5), S = o.suit.map(hex), u = clamp(t / FLY), grow = clamp(u / 0.2);
  const cards = [[2, 0, 0], [-14, -9, 1.3], [-27, 8, 2.6]];
  for (let i = 2; i >= 0; i--) {
    const [bx, by, ph] = cards[i], x = bx * (0.4 + 0.6 * grow), y = by * (0.4 + 0.6 * grow), rot = u * 9 + ph;
    for (let k = 0; k < 2; k++) {
      const yy = y + (k ? 3 : -3), len = (16 + rand(o.seed, i, k) * 22) * grow;
      p.line(x - 6 - len, yy, x - 6, yy, (v) => (v > 0.7 ? C[4] : v > 0.35 ? C[2] : C[1]), (v) => v * 0.8);
    }
    p.glow(x, y, 9, C[2], 0.25);
    card(p, x, y, 3.5, 5.5, rot, Math.cos(u * 14 + ph), C, S);
    p.dot(x + Math.cos(rot - 1) * 6, y + Math.sin(rot - 1) * 6, WHITE, 1);
  }
}
