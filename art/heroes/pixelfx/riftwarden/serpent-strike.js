import { Particles, Lights, P, PF, Rng, fit, clamp, lerp, seg, ease, hex, band, sparkle, WHITE } from "../../core/index.js";
import { serpentHead, coil } from "./serpent-wave.js";

const END = 0.9, FLOOR = 0, UP = 0.3, BITE = 0.38, HOLD = 0.52, GONE = 0.76, PX = -22;

export const meta = {
  name: "Serpent strike",
  desc: "A ring of sea opens on the ground and a serpent rears out of it, fins flaring; it arches over, bites down on the target in a burst of spray, and pours back into the water.",
  tags: ["water", "impact", "creature", "summon", "one-shot"],
  size: [116, 68],
  origin: [74, 56],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#04201e", "#0a4a40", "#12806a", "#2ac8a0", "#7cf0c8", "#d0fff0", "#ffffff"],
    fin: ["#7a1a2a", "#e8504a", "#ffa878"],
    water: ["#0e2e5a", "#1c76bc", "#78ccf4", "#e8faff"],
    eye: "#ffe066",
    seed: 178,
  },
};

const REAR = [[PX, 2], [PX - 9, -21], [PX + 2, -42], [PX + 12, -34]];
const BIT = [[PX, 2], [PX - 7, -31], [PX + 12, -44], [-1, -17]];

function bez(c, u) {
  const v = 1 - u, a = v * v * v, b = 3 * v * v * u, d = 3 * v * u * u, e = u * u * u;
  return [a * c[0][0] + b * c[1][0] + d * c[2][0] + e * c[3][0], a * c[0][1] + b * c[1][1] + d * c[2][1] + e * c[3][1]];
}

export function create(o) {
  const C = fit(o.palette, 7), F = fit(o.fin, 3), W = fit(o.water, 4), eye = hex(o.eye), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, W[2], W[1]], haze: W[1] }), lights = new Lights();
  let t = 0, bit = false, sunk = false;
  lights.add(PX, 0, 28, W[1], 0.6, 0.3);
  const splash = (x, n, power, life) => {
    for (let i = 0; i < n; i++) ps.spawn(P.SPARK, x + R.range(-8, 8), -1, R.range(-70, 70) * power, -R.range(80, 220) * power, R.range(0.5, 1) * life);
    for (let i = 0; i < n / 3; i++) ps.spawn(P.VAPOUR, x + R.range(-10, 10), -2, R.range(-30, 30), R.range(-40, -10), R.range(0.5, 0.8) * life, W[2], R.range(3, 5));
  };
  splash(PX, 12, 0.8, 0.5);
  return {
    update(dt) {
      t += dt;
      if (t > 0.06 && t < HOLD + 0.04 && R.chance(dt * 50)) ps.spawn(P.SPARK, PX + R.range(-9, 9), -1, R.range(-50, 50), -R.range(60, 150), R.range(0.2, 0.36));
      if (!bit && t >= BITE) {
        bit = true;
        lights.add(0, -15, 26, C[3], 0.45, 0.2);
        for (let i = 0; i < 26; i++) {
          const a = R.range(0, Math.PI * 2), sp = R.range(70, 230);
          ps.spawn(P.SPARK, 0, -15, Math.cos(a) * sp * (Math.cos(a) > 0 ? 0.5 : 1), Math.sin(a) * sp - 40, R.range(0.2, 0.42));
        }
        for (let i = 0; i < 7; i++) ps.spawn(P.VAPOUR, R.range(-8, 8), -15 + R.range(-8, 8), R.range(-50, 30), R.range(-40, 10), R.range(0.2, 0.3), W[2], R.range(3, 5), 0, PF.BACK);
      }
      if (!sunk && t >= GONE - 0.04) {
        sunk = true;
        splash(PX, 16, 0.8, 0.17);
        lights.add(PX, -2, 26, W[2], 0.6, 0.2);
      }
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const k = ease.outCubic(seg(t, 0, 0.12)) * (1 - seg(t, GONE, END - 0.04));
      if (k > 0.02) band(p, PX, 1, 15 * k, 0.24, 2.4, k, [W[0], W[1], W[2], W[3]], (g) => Math.sin(g) < 0);
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      const k = ease.outCubic(seg(t, 0, 0.12)) * (1 - seg(t, GONE, END - 0.04));
      if (k > 0.02) {
        p.ellipse(PX, 1, 14 * k, 3.2 * k, (d, g) => (d > 0.8 ? W[1] : Math.sin(d * 9 - t * 20) > 0.3 ? W[1] : W[0]), 1);
        p.glow(PX, 0, 20, W[1], 0.4 * k);
      }
      for (let i = 0; i < 3; i++) {
        const s = (t * 1.7 + i / 3) % 1, a = (1 - s) * clamp(t / 0.1) * (1 - seg(t, GONE, END));
        band(p, PX, 1, 10 + 22 * s, 0.24, 1, a * 0.9, [W[0], W[1], W[2], W[3]], (g) => Math.cos(g) < 0.55 || s < 0.45);
      }
      if (t > 0.07 && t < GONE) {
        const strike = ease.inCubic(seg(t, UP + 0.02, BITE)), sway = Math.sin(t * 16) * 2 * (1 - strike);
        const ctrl = REAR.map((q, i) => [lerp(q[0], BIT[i][0], strike) + (i > 1 ? sway : 0), lerp(q[1], BIT[i][1], strike)]);
        const out = t < HOLD ? ease.outCubic(seg(t, 0.07, UP)) : 1 - ease.inCubic(seg(t, HOLD, GONE));
        const shake = t >= BITE && t < HOLD ? Math.sin(t * 140) * 1.2 : 0, n = 22, pts = [];
        for (let i = 0; i <= n; i++) {
          const q = bez(ctrl, (i / n) * out);
          pts.push([q[0] + (i === n ? shake : 0), Math.min(q[1], FLOOR + 1)]);
        }
        coil(p, pts, 8, 5.4, C, F, 1, t * 14);
        const head = pts[n], prev = pts[n - 2], ang = Math.atan2(head[1] - prev[1], head[0] - prev[0]);
        const open = t < UP ? 0.2 : t < BITE - 0.02 ? 0.2 + 0.6 * seg(t, UP - 0.04, BITE - 0.03) : t < HOLD ? 0.12 + 0.08 * Math.sin(t * 60) : 0.1;
        serpentHead(p, head[0] + Math.cos(ang) * 4, head[1] + Math.sin(ang) * 4, ang, 1.15, open, C, F, eye);
        p.ellipse(PX, 1.5, 9, 2, W[2], 0.9);
        for (let i = -1; i <= 1; i++) p.line(PX + i * 6, 0, PX + i * 7.5, -3 - Math.abs(Math.sin(t * 30 + i)) * 3, W[3], 1);
      }
      if (t >= BITE && t < BITE + 0.14) {
        const f = 1 - (t - BITE) / 0.14;
        sparkle(p, 0, -15, 9 * f, C[4], WHITE, 0.25);
        p.arc(0, -15, 8 + 18 * (1 - f * f), 2, Math.PI * 0.2, Math.PI * 1.1, f > 0.5 ? WHITE : W[2], f);
        p.arc(0, -15, 6 + 14 * (1 - f * f), 1.5, -Math.PI * 0.75, -Math.PI * 0.1, f > 0.5 ? WHITE : C[4], f);
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
