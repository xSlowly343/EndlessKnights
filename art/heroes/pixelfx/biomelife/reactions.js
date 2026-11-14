export default function reactions(fx) {
  const { TAU, hex, rand, noise, clamp, wrap, ease, cycle, WHITE } = fx;
  const S = fx.scene;
  const { W, H, GROUND } = S;
  const VOID = S.ramp(["#2e1870", "#4a2a9a", "#8f5bff", "#b79cff", "#e8daff"]);
  const STORM = S.ramp(["#6f7b90", "#525f76", "#3a445a"]);
  const GUST = S.ramp(["#ffffff", "#dfe8ee"]);
  const LEAF = S.ramp(["#b5d95a", "#7fb648", "#e9e2c4"]);
  const GOLD = S.ramp(["#fffbe6", "#fff0b0", "#ffd86a", "#ffb84a"]);

  const crackX = (y) => noise(y * 0.22, 501) * 3.2 + noise(y * 0.7, 502) * 1.3;

  function enemyFlash(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    const reach = i === 0 ? 14 : i === 1 ? 28 : 36, fade = i < 4 ? 0 : (i - 3) / 6;
    for (let k = 0; k < reach; k++) {
      const y = -36 + k, x = crackX(y), ends = Math.min(k, 36 - k) / 6;
      const half = i >= 2 && i <= 3 ? (ends > 1 ? 1 : 0) : 0;
      if (fade > 0 && rand(k, i, 503) < fade * 0.8) continue;
      for (let d = -half - 1; d <= half + 1; d++) {
        const rim = Math.abs(d) === half + 1;
        if (rim && (i < 2 || fade > 0.4)) continue;
        p.px(x + d, y, rim ? VOID[2] : fade > 0.3 ? VOID[3] : i >= 2 ? WHITE : VOID[4]);
      }
    }
    if (i >= 1 && i <= 5) for (let k = 4; k < 36; k += 7) p.glow(crackX(-36 + k), -36 + k, i < 4 ? 8 : 6, VOID[2], i === 2 || i === 3 ? 0.6 : 0.3);
    if (i >= 2) {
      for (let s = 0; s < 6; s++) {
        const age = (i - 2) / 7, y0 = -32 + s * 5.5, side = s % 2 ? 1 : -1;
        p.dot(crackX(y0) + side * (2 + age * 7), y0 - age * 4 + age * age * 6, s % 3 ? VOID[3] : VOID[4], 1 - age);
      }
    }
  }

  function bossOut(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    const strength = Math.sin(clamp(u * 1.15) * Math.PI);
    for (let n = 0; n < 7; n++) {
      const start = rand(n, 1, 511) * 0.35, k = (u - start) / 0.6;
      if (k <= 0 || k >= 1) continue;
      const head = 128 - k * 170, y = 8 + n * 6 + rand(n, 2, 511) * 3, len = 22 + rand(n, 3, 511) * 16;
      for (let s = 0; s < len; s++) {
        const x = head + s, yy = y + Math.sin(s * 0.16 + n) * 1.8;
        if (x < 0 || x >= W || S.inZone(x, yy)) continue;
        p.dot(x, yy, s < 3 ? GUST[0] : GUST[1], Math.sin(k * Math.PI) * (1 - s / len));
      }
    }
    for (let n = 0; n < 16; n++) {
      const start = rand(n, 4, 511) * 0.3, k = (u - start) / 0.65;
      if (k <= 0 || k >= 1) continue;
      const x = 118 - k * (120 + rand(n, 5, 511) * 60), y = 46 - rand(n, 6, 511) * 30 - Math.sin(k * 3.2) * 6 + k * 8;
      if (x < 0 || S.inZone(x, y)) continue;
      const color = LEAF[n % 3], turn = (i + n) % 3;
      p.dot(x, y, color, 1 - k * 0.5);
      p.dot(x + (turn === 0 ? 1 : 0), y + (turn === 1 ? 1 : turn === 2 ? -1 : 0), color, 1 - k * 0.5);
    }
    for (let n = 0; n < 9; n++) {
      const k = clamp(u * 1.3 - n * 0.04), x = 116 - k * (60 + n * 9) - n * 3;
      if (k <= 0 || k >= 1 || x < 0) continue;
      fx.kit.puff(p, x, GROUND - 1 - k * 3, 1.5 + k * 2, LEAF[2], 0.42 * strength);
    }
  }

  function bossSky(p, u) {
    const t = S.tile(p);
    S.veil(p, 0, 28, STORM[2], (v) => 0.5 * (1 - v) ** 0.8);
    const row = [[12, 8, 36, 8], [52, 11, 42, 9], [96, 7, 34, 7], [134, 12, 44, 9]];
    row.forEach(([x, y, w, h], i) => S.cloud(t, x + Math.round(S.loop(u, 1, i * 0.25) * 1.5), y, w, h, 521 + i, [STORM[0], STORM[1], STORM[2]]));
    const low = [[30, 17, 30, 5], [78, 18, 28, 5], [118, 19, 32, 5], [156, 17, 22, 4]];
    low.forEach(([x, y, w, h], i) => S.cloud(t, x - Math.round(S.loop(u, 1, i * 0.3) * 2), y, w, h, 531 + i, [hex("#8894a8"), STORM[0], STORM[1]], 0.85));
    S.haze(p, u, 14, 28, STORM[1], 0.3, 541);
  }

  function shaft(p, x0, wide, slant, alpha, color) {
    for (let y = 0; y < GROUND; y++) {
      const left = x0 + y * slant, spread = wide + y * 0.12, fall = 1 - (y / GROUND) * 0.45;
      for (let x = Math.floor(left); x < left + spread; x++) {
        if (x < 0 || x >= W) continue;
        const edge = Math.min(x - left, left + spread - x) / spread;
        p.blend(x, y, color, alpha * fall * (edge < 0.18 ? 0.5 : 1));
      }
    }
  }

  function bossDown(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    const glow = Math.sin(clamp(u * 1.05) * Math.PI) ** 0.8;
    const rays = [[18, 9, 0.42], [46, 14, 0.4], [78, 7, 0.38], [96, 11, 0.36]];
    rays.forEach(([x, wide, slant], n) => {
      const late = clamp((u - n * 0.06) / 0.25);
      shaft(p, x, wide, slant, 0.34 * glow * late, n % 2 ? GOLD[1] : GOLD[0]);
    });
    for (let n = 0; n < 14; n++) {
      const x = 14 + rand(n, 1, 551) * 96, y = 40 + rand(n, 2, 551) * 12, tick = (i + Math.floor(rand(n, 3, 551) * 6)) % 6;
      if (tick > 2 || glow < 0.3 || S.inZone(x, y)) continue;
      p.px(x, y, tick === 1 ? WHITE : GOLD[1]);
      if (tick === 1) {
        p.dot(x - 1, y, GOLD[2], 0.8);
        p.dot(x + 1, y, GOLD[2], 0.8);
        p.dot(x, y - 1, GOLD[2], 0.8);
        p.dot(x, y + 1, GOLD[2], 0.8);
      }
    }
    for (let n = 0; n < 8; n++) {
      const k = wrap(u * 1.5 + rand(n, 4, 551), 1), x = 20 + rand(n, 5, 551) * 88 + k * 4, y = 48 - k * 20;
      p.dot(x, y, GOLD[1], glow * Math.sin(k * Math.PI));
    }
  }

  const skyCrack = (x) => 12 + noise(x * 0.07, 561) * 5 + noise(x * 0.33, 562) * 1.8;

  function guardianCrack(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    const open = clamp((i + 1) / 3), peak = i >= 2 && i <= 4, fade = i < 5 ? 0 : (i - 4) / 9;
    if (i >= 1 && i <= 7) S.veil(p, 0, 30, VOID[0], (v) => (peak ? 0.4 : 0.22) * (1 - fade) * (1 - v * 0.6));
    const reach = open * 76;
    for (let x = 4; x < W - 4; x++) {
      const from = Math.abs(x - 84);
      if (from > reach) continue;
      const y = skyCrack(x), tip = clamp((reach - from) / 10), half = peak && tip > 0.6 ? 1 : 0;
      if (fade > 0 && rand(x, i, 563) < fade * 0.85) continue;
      for (let d = -half - 1; d <= half + 1; d++) {
        const rim = Math.abs(d) === half + 1;
        if (rim && (!peak || tip < 0.3)) continue;
        p.px(x, y + d, rim ? VOID[2] : fade > 0.35 ? VOID[3] : peak ? WHITE : VOID[4]);
      }
      if (x % 12 === 0 && fade < 0.7) p.glow(x, y, peak ? 9 : 6, VOID[2], peak ? 0.55 : 0.3 * (1 - fade));
    }
    for (let n = 0; n < 6; n++) {
      const x = 22 + n * 24, y = skyCrack(x), dir = n % 2 ? 1 : -1, len = peak ? 4 + (n % 3) : 2;
      if (Math.abs(x - 84) > reach || fade > 0.5) continue;
      for (let j = 1; j <= len; j++) p.px(x + j * (n % 3 === 0 ? 1 : -1), y + dir * j, j < len ? VOID[3] : VOID[1]);
    }
    if (i >= 3) {
      for (let n = 0; n < 12; n++) {
        const age = (i - 3) / 10, x = 12 + rand(n, 1, 564) * 136;
        p.dot(x + (rand(n, 2, 564) - 0.5) * 6 * age, skyCrack(x) + age * (4 + rand(n, 3, 564) * 9), n % 2 ? VOID[4] : VOID[3], 1 - age);
      }
    }
  }

  function prestigeWave(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    const front = -2 + u * (W + 52);
    for (let y = 0; y < H; y++) {
      const lead = front - (H - y) * 0.3;
      for (let x = Math.max(0, Math.floor(lead - 46)); x <= Math.min(W - 1, lead + 3); x++) {
        const back = lead - x;
        const a = back < 0 ? 0.25 : back < 3 ? 0.85 : back < 9 ? 0.6 - (back - 3) * 0.04 : 0.34 * (1 - (back - 9) / 37) ** 1.5;
        p.blend(x, y, back < 3 ? GOLD[0] : back < 9 ? GOLD[1] : GOLD[2], a);
      }
      if (y % 6 === 0) p.glow(lead, y, 9, GOLD[2], 0.4);
    }
    cycle(u * 1.6, 1.6, 34, 571, (r) => r.range(0.25, 0.5), (age, life, r, n) => {
      const born = u * 1.6 - age, at = -2 + clamp(born / 1.6) * (W + 52), k = age / life;
      const y = r.range(2, H - 3), x = at - (H - y) * 0.3 - k * 6;
      if (x < 0 || x >= W || born < 0) return;
      p.dot(x, y - k * 3, n % 3 ? GOLD[1] : WHITE, 1 - k);
      if (n % 4 === 0 && k < 0.5) {
        p.dot(x - 1, y - k * 3, GOLD[3], 0.8);
        p.dot(x + 1, y - k * 3, GOLD[3], 0.8);
      }
    });
  }

  return { enemyFlash, bossOut, bossSky, bossDown, guardianCrack, prestigeWave };
}
