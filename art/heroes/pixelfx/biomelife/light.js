export default function light(fx) {
  const { TAU, rand, clamp, WHITE } = fx;
  const S = fx.scene;
  const { W, H, GROUND } = S;
  const ROSE = S.ramp(["#ffb3c7", "#ffd9b8", "#c9b8ff"]);
  const DUSK = S.ramp(["#ff9a4a", "#ff6f61", "#6a4a9a"]);
  const NIGHT = S.ramp(["#0b1440", "#1a2a6c", "#3b4fa0"]);
  const SUN = S.ramp(["#fffbe0", "#ffe98a", "#ffd24a"]);
  const LOW = S.ramp(["#fff0c2", "#ffc46b", "#ff8f4f"]);
  const MOON = S.ramp(["#fbfcff", "#dfe6ff", "#aab8e8"]);
  const STAR = S.ramp(["#ffffff", "#cfe0ff", "#8fa8e8", "#ffe9b0"]);
  const steps = (a, n = 8) => Math.round(a * n * 4) / (n * 4);
  const bell = (y, mid, wide) => Math.exp(-(((y - mid) / wide) ** 2));

  function morning(p) {
    S.veil(p, 0, H, ROSE[2], (v, y) => steps(0.2 * clamp(1 - y / 22)));
    S.veil(p, 0, H, ROSE[0], (v, y) => steps(0.4 * bell(y, 24, 11)));
    S.veil(p, 0, H, ROSE[1], (v, y) => steps(0.22 * bell(y, 31, 6) + (y > 36 ? 0.08 : 0)));
  }

  function evening(p) {
    S.veil(p, 0, H, DUSK[2], (v, y) => steps(0.4 * clamp(1 - y / 26)));
    S.veil(p, 0, H, DUSK[1], (v, y) => steps(0.3 * bell(y, 19, 8)));
    S.veil(p, 0, H, DUSK[0], (v, y) => steps(0.46 * bell(y, 27, 8) + (y > 34 ? 0.12 : 0)));
  }

  function night(p) {
    S.veil(p, 0, H, NIGHT[0], (v, y) => steps(y < GROUND ? 0.5 - 0.22 * clamp(y / 30) : 0.3));
    S.veil(p, 0, H, NIGHT[2], (v, y) => steps(0.16 * bell(y, 25, 6)));
  }

  function sun(p, u) {
    const turn = S.beat(u, 8);
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * TAU + (turn % 2) * (TAU / 16), long = (i + turn) % 2 ? 7.4 : 6.2;
      p.dot(Math.cos(a) * long, Math.sin(a) * long, SUN[1], 0.75);
      if ((i + turn) % 2) p.dot(Math.cos(a) * (long - 1), Math.sin(a) * (long - 1), SUN[1], 0.75);
    }
    p.circle(0, 0, 4.6, SUN[2]);
    p.circle(0, 0, 3.8, SUN[1]);
    p.circle(-0.6, -0.6, 2.5, SUN[0]);
    p.glow(0, 0, 10, SUN[1], 0.4 + 0.06 * S.loop(u));
  }

  function sunLow(p, u) {
    p.circle(0, 0, 5.6, LOW[2]);
    p.circle(0, -0.4, 4.7, LOW[1]);
    p.circle(-0.5, -1, 3, LOW[0]);
    for (let x = -9; x <= 9; x++) {
      const a = 0.55 * (1 - Math.abs(x) / 10) * (0.8 + 0.2 * S.loop(u, 1, x * 0.07));
      p.dot(x, 3, LOW[2], Math.abs(x) > 5 ? a : 0);
      p.dot(x, 6, LOW[1], Math.abs(x) > 4 ? a * 0.7 : 0);
    }
    p.glow(0, 0, 13, LOW[2], 0.45 + 0.06 * S.loop(u));
  }

  function moon(p, u) {
    for (let y = -5; y <= 5; y++) {
      for (let x = -5; x <= 5; x++) {
        const d = Math.hypot(x + 0.5 - 0.5, y + 0.5 - 0.5);
        if (d > 4.7) continue;
        const cut = Math.hypot(x - 2.6, y + 1.4) < 4.1;
        if (cut) {
          p.dot(x, y, MOON[2], 0.3);
          continue;
        }
        p.px(x, y, d > 3.9 ? MOON[1] : MOON[0]);
      }
    }
    p.px(-3, 1, MOON[1]);
    p.px(-2, 3, MOON[1]);
    p.glow(-0.5, 0.5, 11, MOON[2], 0.38 + 0.05 * S.loop(u));
  }

  function stars(p, u) {
    for (let i = 0; i < 44; i++) {
      const x = Math.floor(rand(i, 1, 201) * W), y = Math.floor(rand(i, 2, 201) ** 1.5 * 21);
      const big = i % 9 === 0, tick = S.beat(u, 16, Math.floor(rand(i, 3, 201) * 16));
      const dim = tick < 2 || (!big && i % 3 === 0 && tick < 5);
      if (dim && !big) {
        if (tick === 1) p.dot(x, y, STAR[2], 0.6);
        continue;
      }
      p.px(x, y, big ? STAR[0] : i % 5 === 0 ? STAR[3] : i % 2 ? STAR[1] : STAR[2]);
      if (!big) continue;
      const flare = tick >= 6 && tick <= 9;
      const arm = flare ? STAR[1] : STAR[2];
      p.dot(x - 1, y, arm, flare ? 1 : 0.5);
      p.dot(x + 1, y, arm, flare ? 1 : 0.5);
      p.dot(x, y - 1, arm, flare ? 1 : 0.5);
      p.dot(x, y + 1, arm, flare ? 1 : 0.5);
      if (flare) p.glow(x + 0.5, y + 0.5, 3, WHITE, 0.3);
    }
  }

  return { lightMorning: morning, lightEvening: evening, lightNight: night, sun, sunLow, moon, stars };
}
