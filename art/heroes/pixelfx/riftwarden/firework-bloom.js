import { Particles, Lights, P, Rng, fit, clamp, seg, hex, mix, sparkle, TAU, WHITE } from "../../core/index.js";

const END = 0.95, FLOOR = 16;

export const meta = {
  name: "Firework bloom",
  desc: "Three bursts in one: a wide rose chrysanthemum whose stars turn colour as they slow, a gold heart, a small blue shell; tips crackle white and a gold willow weeps down.",
  tags: ["fire", "impact", "festival", "one-shot"],
  size: [112, 84],
  origin: [62, 46],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#7a4a0c", "#c48412", "#f0b424", "#ffe066", "#fff6c4", "#ffffff"],
    rose: ["#5a0c3c", "#c81e78", "#ff4ab4", "#ffa0dc"],
    blue: ["#0c2c6a", "#1e78d8", "#4ae0ff", "#c8f8ff"],
    seed: 182,
  },
};

export function create(o) {
  const G = fit(o.palette, 6), RO = o.rose.map(hex), BL = o.blue.map(hex), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, G[3], G[1]] }), lights = new Lights();
  const shells = [
    { at: 0, x: -3, y: -5, n: 26, sp: 205, drag: 6.2, g: 30, life: 0.54, ramp: [WHITE, RO[3], RO[2], RO[1], RO[0]], tail: 7, crackle: true },
    { at: 0.1, x: -3, y: -5, n: 14, sp: 112, drag: 6.6, g: 24, life: 0.44, ramp: [WHITE, G[4], G[3], G[2], G[1]], tail: 5 },
    { at: 0.2, x: 12, y: -16, n: 12, sp: 98, drag: 7, g: 24, life: 0.42, ramp: [WHITE, BL[3], BL[2], BL[1], BL[0]], tail: 5, crackle: true },
    { at: 0.14, x: -3, y: -5, n: 11, sp: 120, drag: 4.2, g: 150, life: 0.74, ramp: [G[4], G[3], G[2], G[1], G[0]], tail: 12, willow: true },
  ];
  for (const s of shells) s.stars = Array.from({ length: s.n }, (_, i) => {
    const a = (i / s.n) * TAU + R.range(-0.09, 0.09) + (s.willow ? Math.PI : 0), k = s.willow ? R.range(0.5, 1) : R.range(0.86, 1.06);
    return { dx: Math.cos(a) * k * (s.willow ? 0.75 : 1), dy: Math.sin(a) * k * (s.willow ? 0.5 : 0.92) - (s.willow ? 0.45 : 0), life: s.life * R.range(0.82, 1), tw: R.range(0, 6) };
  });
  const pos = (s, st, age) => {
    const e = (1 - Math.exp(-s.drag * age)) / s.drag;
    return [s.x + st.dx * s.sp * e, s.y + st.dy * s.sp * e + s.g * age * age];
  };
  let t = 0, fired = 0;
  const order = [0, 1, 3, 2];
  return {
    update(dt) {
      t += dt;
      while (fired < order.length && t >= shells[order[fired]].at) {
        const s = shells[order[fired]];
        if (!s.willow) {
          lights.add(s.x, s.y, fired === 0 ? 44 : 28, s.ramp[2], fired === 0 ? 1 : 0.7, 0.24);
          for (let i = 0; i < (fired === 0 ? 14 : 6); i++) ps.spawn(P.STAR, s.x + R.range(-20, 18), s.y + R.range(-18, 16), 0, 6, R.range(0.2, 0.5), mix(s.ramp[2], WHITE, 0.4), R.range(1.4, 2.8), R.range(0.1, 0.4));
        }
        fired++;
      }
      ps.update(dt);
      lights.update(dt);
    },
    draw(p) {
      if (t >= END) return;
      for (const s of shells) {
        const age = t - s.at;
        if (age < 0) continue;
        if (!s.willow && age < 0.07) {
          const f = 1 - age / 0.07;
          p.circle(s.x, s.y, (s.n > 20 ? 8 : 5) * f, WHITE, 1);
          sparkle(p, s.x, s.y, (s.n > 20 ? 15 : 9) * f, s.ramp[1], WHITE);
          p.ring(s.x, s.y, 4 + (s.n > 20 ? 20 : 12) * (1 - f), 1.5, s.ramp[1], f);
        }
        for (const st of s.stars) {
          if (age > st.life) continue;
          const k = age / st.life;
          let [px, py] = pos(s, st, age);
          if (py > FLOOR) continue;
          for (let i = 1; i <= s.tail; i++) {
            const back = Math.max(0, age - i * 0.014), [x, y] = pos(s, st, back), f = i / s.tail;
            p.line(px, py, x, y, s.ramp[Math.min(4, Math.floor(k * 3.2 + f * 2.4))], (1 - f) * (1 - k * k) * (s.willow ? 0.9 : 1));
            px = x; py = y;
          }
          const [hx, hy] = pos(s, st, age), blink = Math.sin(t * 46 + st.tw) > 0;
          if (s.crackle && k > 0.55) {
            if (blink) sparkle(p, hx, hy, 2.6 * (1 - k) + 1.2, s.ramp[1], WHITE, 0.5);
            else p.dot(hx, hy, s.ramp[2], 1);
          } else {
            p.dot(hx, hy, k < 0.5 ? WHITE : s.ramp[1], 1);
            if (k < 0.4 && !s.willow) { p.dot(hx + 1, hy, s.ramp[1], 1); p.dot(hx, hy + 1, s.ramp[1], 1); }
            p.light(hx, hy, s.ramp[2], 0.7 * (1 - k));
          }
          if (s.willow && k > 0.3 && blink) p.dot(hx, hy + 1, WHITE, 1 - k);
        }
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
