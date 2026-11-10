import { Particles, Lights, P, Rng, fit, sparkle, flare, band, clamp, ease, WHITE, READY } from "../../core/index.js";

const WIND = 0.16, CUT = 0.0, RING = 0.07, END = 1.1;

export const meta = {
  name: "Royal cleave",
  desc: "A crown-gold crescent sweeps down from high overhead with a crimson edge, slams into the floor in a ring of gold and throws up crown-shaped glints and sparks.",
  tags: ["melee", "slash", "greatsword", "gold", "directional", "one-shot"],
  size: [210, 150],
  origin: [60, 128],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  actor: 2,
  options: {
    palette: ["#2a0a10", "#6e1420", "#b02a2a", "#e0702a", "#ffb43a", "#ffe07a", "#fff6d0", "#ffffff"],
    radius: 40,
    seed: 5,
  },
};

const GUARD = { ...READY, weapon: "greatsword", two: true, fx: 12, fy: -21, sa: -0.9 };
const LIFT = { ...GUARD, x: -6, crouch: 1, lean: -6, fx: -2, fy: -40, sa: -2.4, y: -2 };
const LEAP = { ...LIFT, x: 4, y: -6, lean: -2 };
const SLAM = { ...GUARD, x: 14, crouch: 7, lean: 7, fx: 22, fy: -12, sa: 0.9, toeB: true, ghost: 1 };
export const act = {
  target: { x: 60 },
  poses: [
    [-0.5, GUARD],
    [-0.28, LIFT],
    [-0.1, LEAP, "out"],
    [0, SLAM, "snap"],
    [0.32, { ...SLAM, crouch: 5 }],
    [0.6, { ...GUARD, x: 6 }],
    [0.85, { ...GUARD, x: 0 }],
  ],
  hits: [{ t: 0.02, stop: 0.16, shake: 0.26, amp: 3, push: 3.2, straw: 22, skin: "char" }],
  dust: [{ t: 0.02, x: 30, n: 9, dir: 1 }, { t: 0.02, x: 4, n: 5, dir: -1 }],
};

export function create(o) {
  const C = fit(o.palette, 8), R = new Rng(o.seed), RAD = o.radius;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[6], C[5], C[3]] }), lights = new Lights();
  const CX = 26, CY = -30;
  let t = 0, hit = false;
  return {
    update(dt) {
      t += dt;
      if (!hit && t >= RING) {
        hit = true;
        lights.add(44, -4, 56, C[4], 1, 0.5);
        for (let i = 0; i < 30; i++) {
          const a = -Math.PI * R.range(0.08, 0.92), sp = R.range(80, 240);
          ps.spawn(P.SPARK, 44 + R.range(-6, 6), -1, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.3, 0.7));
        }
        for (let i = 0; i < 7; i++) ps.spawn(P.DUST, 44 + R.range(-16, 16), -2, R.range(-40, 40), R.range(-24, -8), R.range(0.6, 1.0));
        for (let i = 0; i < 5; i++) ps.spawn(P.STAR, 44 + R.range(-20, 20), R.range(-34, -8), 0, R.range(-14, -4), R.range(0.4, 0.8), C[6]);
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      const u = clamp(t / 0.12), sweep = ease.out ? ease.out(u) : u;
      const a0 = -Math.PI * 0.85, a1 = a0 + (Math.PI * 1.15) * sweep, fade = clamp(1 - (t - 0.1) / 0.28);
      if (fade > 0) {
        const W = (v) => 1 + 6 * Math.sin(Math.PI * v) * fade;
        p.arc(CX, CY, RAD, W, a0, a1, (v) => (v > 0.8 ? WHITE : v > 0.55 ? C[6] : v > 0.3 ? C[5] : C[4]), 1);
        p.arc(CX, CY, RAD - 7, 1, a0, a1, C[2], 0.9 * fade);
        p.arc(CX, CY, RAD + 1.5, 1, a0 + (a1 - a0) * 0.4, a1, C[7], fade);
        p.glow(CX + Math.cos(a1) * RAD, CY + Math.sin(a1) * RAD, 14, C[4], 0.5 * fade);
        sparkle(p, CX + Math.cos(a1) * RAD, CY + Math.sin(a1) * RAD, 5 * fade, C[6], WHITE);
      }
      const s = t - RING;
      if (s >= 0) {
        const f = clamp(1 - s / 0.6);
        band(p, 44, 0, 4 + s * 70, 0.16, 1.4, f, [C[3], C[4], C[5], C[6]]);
        if (s < 0.08) flare(p, 44, -3, 12, 1, [C[4], C[6], WHITE]);
        p.glow(44, -2, 22, C[4], 0.4 * f);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
