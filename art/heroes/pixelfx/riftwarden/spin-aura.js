import { fit, sparkle, clamp, cycle, WHITE } from "../../core/index.js";

const D = 1.2;

export const meta = {
  name: "Spin aura",
  desc: "Threads of light spiral up round the body and tighten, its outline glows hotter, motes rise, then everything flashes white on the release and unwinds.",
  tags: ["aura", "cast", "thread", "buff", "body", "one-shot"],
  size: [96, 96],
  origin: [48, 84],
  duration: D,
  loop: false,
  fps: 30,
  bg: "ground",
  actor: 2,
  options: {
    palette: ["#2a0f2e", "#5a1e5e", "#9a3a8e", "#d86ab8", "#f6a8dc", "#ffe0f4", "#ffffff"],
    release: 0.62,
    seed: 3,
  },
};

const power = (t, R) => (t < R ? clamp(t / R) ** 1.4 : clamp(1 - (t - R) / (D - R)) ** 0.8);

function helix(p, t, o, back) {
  const A = o.actor, C = fit(o.palette, 7), R = o.release, k = power(t, R);
  if (k <= 0.02) return;
  const top = A.y - 6, cx = A.x + A.w / 2, rad = A.w * 0.55 * (1.15 - 0.45 * clamp(t / R));
  for (let s = 0; s < 2; s++) {
    let prev = null;
    for (let i = 0; i <= 40; i++) {
      const u = i / 40, y = -u * (-top), ang = u * Math.PI * 5 + t * 9 + s * Math.PI;
      const z = Math.sin(ang), x = cx + Math.cos(ang) * rad * (0.6 + 0.4 * u);
      const front = z > 0;
      if (front !== !back) { prev = null; continue; }
      if (prev && u <= clamp(t / (R * 0.7))) p.line(prev[0], prev[1], x, y, s ? C[4] : C[5], k);
      prev = [x, y];
    }
  }
}

export function drawBack(p, t, o) { helix(p, t, o, true); }

export function draw(p, t, o) {
  const A = o.actor, C = fit(o.palette, 7), R = o.release, k = power(t, R);
  helix(p, t, o, false);
  if (k > 0.02) {
    p.outline(A.mask, A.x, A.y, t >= R && t < R + 0.034 ? WHITE : C[4], Math.min(1, k * 1.2));
    p.glow(A.x + A.w / 2, A.y + A.h * 0.45, A.h * 0.7, C[3], 0.25 * k);
  }
  if (t >= R && t < R + 0.034) p.mask(A.mask, A.x, A.y, WHITE, 0.55);
  if (k > 0.1) cycle(t, 0.6, 14, o.seed, 0.6, (age, L, r) => {
    const u = age / L, x = A.x + r.next() * A.w, y = -u * (A.h + 14);
    p.dot(x, y, u < 0.5 ? C[5] : C[4], k * (1 - u));
  });
  if (t >= R && t < R + 0.1) sparkle(p, A.x + A.w / 2, A.y + A.h * 0.4, 10 * (1 - (t - R) / 0.1), C[5], WHITE);
}
