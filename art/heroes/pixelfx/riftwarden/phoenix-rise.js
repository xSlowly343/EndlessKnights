import { Particles, Lights, P, PF, Rng, fit, clamp, seg, ease, band, sparkle, WHITE } from "../../core/index.js";

const END = 0.86, FLOOR = 0, HIT = -8;

export const meta = {
  name: "Phoenix rise",
  desc: "The firebird is reborn from its own blast: a ring of flame runs out, it climbs with wings unfolding into a fan of burning feathers, then lets go into embers and smoke.",
  tags: ["fire", "impact", "creature", "one-shot"],
  size: [124, 80],
  origin: [66, 64],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#3a0a08", "#8a1f17", "#d8401c", "#ff8a2a", "#ffc23d", "#ffe98a", "#ffffff"],
    seed: 112,
  },
};

function feather(p, x0, y0, x1, y1, w, C, heat, a) {
  p.beam(x0, y0, x1, y1, w + 1.6, C[1], a);
  p.beam(x0, y0, x1, y1, w, (u, v) => {
    const h = clamp(heat - u * 0.55);
    return v < 0.35 ? (h > 0.6 ? C[6] : h > 0.3 ? C[5] : C[4]) : h > 0.7 ? C[5] : h > 0.4 ? C[4] : h > 0.15 ? C[3] : C[2];
  }, a);
  p.dot(x1, y1, heat > 0.5 ? WHITE : C[5], a);
}

function bird(p, cy, spread, heat, C, a) {
  for (const side of [-1, 1]) {
    const reach = 0.3 + 0.7 * spread, armA = lerp(-0.9, 0.62, spread), ax = Math.cos(armA) * 19 * reach, ay = -Math.sin(armA) * 19 * reach;
    for (let i = 0; i < 8; i++) {
      const f = i / 7, bx = side * (2 + ax * f), by = cy - 3 + ay * f - Math.sin(f * Math.PI) * 3 * spread;
      const dir = lerp(-1.45, 0.75, Math.pow(f, 1.6)) * (0.4 + 0.6 * spread) - (1 - spread) * 0.9, L = (7 + 8 * f) * reach;
      feather(p, bx, by, bx + side * Math.cos(dir) * L, by - Math.sin(dir) * L, 3.6 - f * 0.8, C, heat, a);
    }
    p.beam(side * 2, cy - 3, side * (2 + ax), cy - 3 + ay, 3, (u, v) => (v < 0.5 ? C[6] : C[5]), a);
  }
  for (let i = -1; i <= 1; i++) {
    let px = i * 1.5, py = cy + 4;
    for (let k = 1; k <= 8; k++) {
      const s = k / 8, x = i * (1.5 + s * 7 * spread) + Math.sin(s * 5 + i + heat * 6) * 2 * s, y = cy + 4 + s * (11 + (i === 0 ? 5 : 0)) * (0.5 + 0.5 * spread);
      p.beam(px, py, x, y, 2.6 * (1 - s) + 1, s < 0.4 ? C[5] : s < 0.75 ? C[4] : C[3], a);
      px = x; py = y;
    }
    p.circle(px, py, 2.2, C[3], a);
    p.dot(px, py, heat > 0.4 ? WHITE : C[5], a);
  }
  p.beam(0, cy - 5, 0, cy + 5, 6.4, C[1], a);
  p.beam(0, cy - 5, 0, cy + 5, 5, (u, v) => (v < 0.45 ? (heat > 0.4 ? C[6] : C[5]) : C[4]), a);
  p.circle(0, cy - 8.5, 3.6, C[1], a);
  p.circle(0, cy - 8.5, 2.8, (d) => (d < 0.5 ? C[6] : C[5]), a);
  p.tri(-1, cy - 7, 1, cy - 7, 0, cy - 4.6, C[3], a);
  for (let i = -1; i <= 1; i++) p.line(i, cy - 11, i * 3.5, cy - 15 - (i === 0 ? 2 : 0), i === 0 ? C[6] : C[4], a);
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [C[6], C[4], C[2]] }), lights = new Lights();
  const drift = [];
  lights.add(0, HIT, 40, C[4], 0.9, 0.3);
  for (let i = 0; i < 26; i++) {
    const a = R.range(0, Math.PI * 2), sp = R.range(70, 230);
    ps.spawn(P.SPARK, 0, HIT, Math.cos(a) * sp, Math.sin(a) * sp - 50, R.range(0.25, 0.6));
  }
  for (let i = 0; i < 16; i++) ps.spawn(P.FIRE, R.range(-12, 12), FLOOR - R.range(0, 6), R.range(-30, 30), R.range(-70, -20), R.range(0.25, 0.5), 0, R.range(2.4, 4.4), R.range(0, 0.12), PF.SMOKES);
  let t = 0, burnt = false;
  return {
    update(dt) {
      t += dt;
      if (t > 0.08 && t < 0.5 && R.chance(dt * 70)) {
        const y = HIT - 16 * ease.outCubic(seg(t, 0.05, 0.4));
        ps.spawn(P.FIRE, R.range(-20, 20), y + R.range(-6, 12), R.range(-20, 20), R.range(-40, 10), R.range(0.18, 0.34), 0, R.range(1.6, 3), 0, PF.BACK);
        ps.spawn(P.EMBER, R.range(-26, 26), y + R.range(-10, 14), R.range(-30, 30), R.range(-50, -10), R.range(0.3, 0.6), 0, R.range(1, 2));
      }
      if (!burnt && t > 0.52) {
        burnt = true;
        lights.add(0, -22, 36, C[4], 0.7, 0.3);
        for (let i = 0; i < 34; i++) {
          const a = R.range(0, Math.PI * 2), r = R.range(4, 26);
          ps.spawn(P.EMBER, Math.cos(a) * r, -26 + Math.sin(a) * r * 0.6, Math.cos(a) * R.range(20, 90), Math.sin(a) * R.range(20, 70) - 30, R.range(0.2, 0.34), 0, R.range(1, 2.2));
        }
        for (let i = 0; i < 7; i++) ps.spawn(P.SMOKE, R.range(-16, 16), -26 + R.range(-10, 10), R.range(-12, 12), R.range(-26, -8), R.range(0.22, 0.32), 0, R.range(3, 5));
        for (let i = 0; i < 6; i++) drift.push({ x: R.range(-26, 26), y: -30 + R.range(-8, 8), ph: R.range(0, 6), born: t, life: R.range(0.22, 0.32) });
      }
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const k = seg(t, 0, 0.3);
      if (k < 1) band(p, 0, FLOOR, 6 + 28 * ease.outQuart(k), 0.2, 3, 1 - k, [C[1], C[2], C[4], C[6]], (g) => Math.sin(g) < 0, t < 0.06);
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      const k = seg(t, 0, 0.3);
      if (k < 1) band(p, 0, FLOOR, 6 + 28 * ease.outQuart(k), 0.2, 3, 1 - k, [C[1], C[2], C[4], C[6]], (g) => Math.sin(g) >= 0, t < 0.06);
      if (t < 0.1) {
        sparkle(p, 0, HIT, 16 * (1 - t / 0.1), C[5], WHITE);
        p.circle(0, HIT, 9 * (1 - t / 0.1), WHITE, 1);
      }
      if (t > 0.04 && t < 0.56) {
        const rise = ease.outCubic(seg(t, 0.04, 0.4)), cy = HIT - 9 * rise, beat = Math.sin(seg(t, 0.3, 0.52) * Math.PI) * 0.12;
        const spread = ease.outBack(seg(t, 0.06, 0.32), 1.6) - beat, heat = 1 - seg(t, 0.12, 0.5) * 0.75;
        p.glow(0, cy, 30, C[3], 0.5);
        p.glow(0, cy, 14, C[5], 0.5 * heat);
        bird(p, cy, clamp(spread, 0.05, 1.15), heat, C, t > 0.5 ? 1 - seg(t, 0.5, 0.56) : 1);
      }
      for (const f of drift) {
        const s = t - f.born;
        if (s > f.life) continue;
        const x = f.x + Math.sin(s * 9 + f.ph) * 5, y = f.y + s * 60, tilt = Math.cos(s * 9 + f.ph) * 0.9, a = 1 - s / f.life;
        p.beam(x - Math.cos(tilt) * 3, y - Math.sin(tilt) * 3, x + Math.cos(tilt) * 3, y + Math.sin(tilt) * 3, 2, (u) => (u < 0.4 ? C[5] : C[3]), a);
        p.light(x, y, C[3], 0.5 * a);
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
