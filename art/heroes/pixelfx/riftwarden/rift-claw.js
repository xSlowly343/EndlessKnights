import { fit, clamp, rand, seg, ease, hex, sparkle, WHITE } from "../../core/index.js";

const END = 0.6;

export const meta = {
  name: "Rift claw",
  desc: "The crack opens on its target into an eye of dark with stars in it; three talons rake out and back, then the tear snaps shut on a line of light.",
  tags: ["shadow", "impact", "rift", "one-shot"],
  size: [112, 96],
  origin: [52, 48],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#07030f", "#1c0b38", "#47148a", "#8a2be0", "#d24cf0", "#ff9af4", "#ffffff"],
    star: "#8ef0e0",
    seed: 92,
  },
};

function lens(w, h, grow) {
  const pts = [];
  for (let i = 0; i <= 10; i++) {
    const v = i / 10, y = -h + 2 * h * v;
    pts.push([-(w + grow) * Math.pow(Math.sin(Math.PI * v), 0.8) - (i % 2 ? 0.8 : 0), y]);
  }
  for (let i = 10; i >= 0; i--) {
    const v = i / 10, y = -h + 2 * h * v;
    pts.push([(w + grow) * Math.pow(Math.sin(Math.PI * v), 0.8) + (i % 2 ? 0 : 0.8), y]);
  }
  return pts;
}

export function draw(p, t, o) {
  if (t >= END) return;
  const C = fit(o.palette, 7), ST = hex(o.star);
  const open = ease.outBack(seg(t, 0, 0.12), 2), shut = ease.inCubic(seg(t, 0.36, 0.46)), w = 10 * open * (1 - shut), h = 14 + 12 * open;
  if (t < 0.46) {
    p.glow(0, 0, 22, C[3], 0.45 * (1 - shut * 0.5));
    for (let i = 0; i < 4; i++) {
      const k = (t * 1.6 + i / 4) % 1;
      p.disc(Math.sin(i * 2.4) * 8 + k * 6 * (i % 2 ? 1 : -1), 10 - k * 26, 3 + 4 * k, C[2], 0.4 * (1 - k) * open * (1 - shut));
    }
    if (w > 0.4) {
      p.poly(lens(w, h, 2.4), C[4], 1);
      p.poly(lens(w, h, 1.2), C[5], 1);
      p.poly(lens(w, h - 1, 0), C[0], 1);
      p.poly(lens(w * 0.6, h * 0.7, 0), C[1], 0.6);
      for (let i = 0; i < 6; i++) {
        const y = (rand(o.seed, i) - 0.5) * 1.5 * h, x = (rand(o.seed, i + 10) - 0.5) * 1.2 * w * Math.sin(Math.PI * (y / h / 2 + 0.5));
        if (Math.sin(t * 30 + i * 2.1) > -0.2) p.dot(x, y, i % 2 ? ST : WHITE, 1);
      }
    }
    p.dot(0, -h - 1, WHITE, 1);
    p.dot(0, h + 1, C[5], 1);
  }
  const reach = ease.outQuart(seg(t, 0.08, 0.17)) * (1 - ease.inQuad(seg(t, 0.26, 0.36)));
  if (reach > 0.02) {
    for (let i = 0; i < 3; i++) {
      const y0 = -9 + i * 8, L = (24 + (i === 1 ? 6 : 0)) * reach, drop = 7 + i * 2;
      const at = (s) => [s * L, y0 - Math.sin(s * Math.PI) * 5 * reach + s * s * drop];
      let a = at(0);
      for (let k = 1; k <= 10; k++) {
        const s = k / 10, b = at(s), wd = 4.2 * (1 - s) + 0.8;
        p.beam(a[0], a[1], b[0], b[1], wd + 1.6, C[4], 1);
        a = b;
      }
      a = at(0);
      for (let k = 1; k <= 10; k++) {
        const s = k / 10, b = at(s), wd = 4.2 * (1 - s) + 0.8;
        p.beam(a[0], a[1], b[0], b[1], wd, s > 0.75 ? C[2] : C[1], 1);
        p.dot(b[0], b[1] - wd / 2, s > 0.6 ? C[5] : C[3], 1);
        a = b;
      }
      p.dot(a[0] + 1, a[1], WHITE, 1);
      p.light(a[0], a[1], C[5], 0.8);
      if (t > 0.14 && t < 0.3) p.line(a[0] + 2, a[1] - 6, a[0] + 6, a[1] + 5, C[5], (v) => Math.sin(Math.PI * v) * (1 - seg(t, 0.2, 0.3)));
    }
  }
  if (t >= 0.42 && t < 0.56) {
    const k = seg(t, 0.42, 0.56), l = h * (1 - k * 0.6);
    p.glowLine(0, -l, 0, l, 5, C[4], 0.6 * (1 - k));
    p.line(0, -l, 0, l, k < 0.4 ? WHITE : C[5], (v) => Math.sin(Math.PI * v) * (1 - k));
    if (k < 0.5) sparkle(p, 0, 0, 9 * (1 - k * 2), C[5], WHITE);
  }
  for (let i = 0; i < 10; i++) {
    const born = 0.02 + rand(o.seed, i + 30) * 0.4, age = t - born;
    if (age < 0 || age > 0.16) continue;
    const a = rand(o.seed, i + 40) * 6.28, r = 8 + 90 * age, x = Math.cos(a) * r * 0.7, y = Math.sin(a) * r;
    p.tri(x, y - 2, x + 2, y + 1, x - 1, y + 1, i % 2 ? C[5] : C[3], 1 - age / 0.16);
  }
}
