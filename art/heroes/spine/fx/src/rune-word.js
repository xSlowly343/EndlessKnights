import { PAL, ramp, at, burst, fly, seg, sparkle, WHITE, TAU } from "../../core/index.js";

export const meta = {
  name: "Rune word",
  desc: "Five small runes snap in on a ring, a large rune stamps white in the middle, the ring closes, then the seal cools down the ramp and crumbles into rising motes.",
  tags: ["runes", "seal", "mark", "impact", "one-shot"],
  size: [96, 96],
  origin: [48, 52],
  duration: 0.9,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: PAL.fire, rune: "flame", seed: 3 },
};

const GLYPHS = {
  flame: [[0, -9, 0, 9], [0, -9, 6, -2], [6, -2, 0, 3], [0, 3, -5, 9]],
  frost: [[0, -9, 0, 9], [-6, -5, 6, 5], [6, -5, -6, 5], [-3, -9, 0, -6], [3, -9, 0, -6]],
  thunder: [[4, -9, -4, -1], [-4, -1, 4, -1], [4, -1, -4, 9], [-4, 9, -1, 5]],
  shadow: [[-5, -9, -5, 9], [-5, -9, 5, -3], [5, -3, -5, 2], [5, 9, 5, 3]],
  light: [[0, -9, 0, 9], [-6, 0, 6, 0], [-5, -6, 5, 6], [5, -6, -5, 6]],
};
const ORDER = ["flame", "frost", "thunder", "shadow", "light"];
const RING = 26;

function glyph(p, name, cx, cy, s, c, a, thick) {
  for (const [x0, y0, x1, y1] of GLYPHS[name]) {
    p.line(cx + x0 * s, cy + y0 * s, cx + x1 * s, cy + y1 * s, c, a);
    if (thick) p.line(cx + x0 * s + 1, cy + y0 * s, cx + x1 * s + 1, cy + y1 * s, c, a);
  }
}

export function draw(p, t, o) {
  const C = ramp(o.palette), n = C.length, cy = -8;
  const stamp = 0.2, cool = seg(t, stamp + 0.08, 0.62), fade = seg(t, 0.55, 0.88);
  const hot = t < stamp + 0.08;
  ORDER.forEach((name, i) => {
    const born = i * 0.035;
    if (t < born) return;
    const an = -TAU / 4 + (i * TAU) / 5 + seg(t, 0, 0.5) * 0.5;
    const pull = 1 + 0.5 * (1 - seg(t, born, born + 0.09));
    const x = Math.cos(an) * RING * pull, y = cy + Math.sin(an) * RING * pull;
    const c = t < born + 0.05 || hot ? WHITE : at(C.slice(2), 1 - cool * 0.7);
    glyph(p, name, x, y, 0.5, c, 1 - fade, false);
    p.light(x, y, C[n - 3], 0.5 * (1 - fade));
  });
  if (t >= stamp) {
    const pop = 1.9 - 0.4 * seg(t, stamp, stamp + 0.07);
    const c = hot ? WHITE : at(C.slice(2), 1 - cool * 0.6);
    glyph(p, o.rune in GLYPHS ? o.rune : "flame", 0, cy, pop, C[1], (1 - fade) * 0.9, true);
    glyph(p, o.rune in GLYPHS ? o.rune : "flame", -1, cy - 1, pop, c, 1 - fade, true);
    p.ring(0, cy, RING - 7, 1, hot ? WHITE : C[n - 3], (1 - fade) * (hot ? 1 : 0.55));
    p.glow(0, cy, 34, C[n - 3], 0.75 * (1 - seg(t, stamp, 0.7)));
    if (t < stamp + 0.1) {
      sparkle(p, 0, cy, 22 * (1 - seg(t, stamp, stamp + 0.1)) + 6, WHITE, C[n - 2]);
      p.glow(0, cy, 46, WHITE, 0.5);
    }
    p.glowRing?.(0, cy, RING - 7, C[n - 3], 0.4 * (1 - fade));
  }
  burst(26, o.seed, (r) => {
    const a = r.range(0, TAU), sp = r.range(30, 95), life = r.range(0.25, 0.65), born = stamp + r.range(0, 0.06);
    const age = t - born;
    if (age < 0 || age > life) return;
    const [x, y] = fly(Math.cos(a) * 6, cy + Math.sin(a) * 6, Math.cos(a) * sp, Math.sin(a) * sp - 30, age, -60, 3);
    const u = age / life;
    p.px(x, y, u < 0.25 ? WHITE : at(C.slice(2), 1 - u));
    if (u < 0.5) p.px(x - Math.cos(a), y - Math.sin(a), at(C.slice(2), 0.8 - u));
    p.light(x, y, C[n - 3], 0.45 * (1 - u));
  });
}
