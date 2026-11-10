import { fit, clamp, cycle } from "../../core/index.js";

const D = 0.8;

export const meta = {
  name: "Berserk aura",
  desc: "Battle fury on the body: a red outline throbs with each heartbeat, heat glows from the chest, steam and embers lift off the shoulders.",
  tags: ["aura", "rage", "fire", "buff", "body", "loop"],
  size: [96, 96],
  origin: [48, 84],
  duration: D,
  loop: true,
  fps: 30,
  bg: "ground",
  actor: 2,
  onBody: true,
  options: { palette: ["#2a0606", "#5c0e0e", "#a01c14", "#e04a1c", "#ff8a2a", "#ffc23d", "#fff0a0"], power: 1, seed: 2 },
};

export function draw(p, t, o) {
  const A = o.actor, C = fit(o.palette, 7), k = o.power;
  const beat = Math.max(0, Math.sin((t / D) * Math.PI * 2)) ** 3;
  p.outline(A.mask, A.x, A.y, beat > 0.5 ? C[5] : C[3], clamp(0.45 + 0.55 * beat) * k);
  p.glow(A.x + A.w / 2, A.y + A.h * 0.4, A.h * 0.6, C[3], (0.12 + 0.18 * beat) * k);
  cycle(t, D, 10, o.seed, 0.6, (age, L, r) => {
    const u = age / L, x = A.x + A.w * (0.2 + 0.6 * r.next()), y = A.y + A.h * 0.25 - u * 18;
    p.dot(x + Math.sin(u * 6 + r.next() * 6) * 2, y, u < 0.4 ? C[5] : C[3], (1 - u) * k);
  });
}
