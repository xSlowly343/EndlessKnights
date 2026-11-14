export default function snow(fx) {
  const { TAU, hex, rand, wrap, clamp, cycle, ease, WHITE } = fx;
  const S = fx.scene;
  const K = fx.kit;
  const { W, H, GROUND } = S;
  const { rabbitKind, fliesKind, blit, pal } = fx.draws.critterKinds;
  const { gustKind } = fx.draws.reactionKinds;
  const ICE = S.ramp(["#ffffff", "#e4eefc", "#b8cdee", "#8aa6d8", "#5f7fc0"]);
  const OVERCAST = S.ramp(["#dfe6f0", "#c3cede", "#a3b1c8"]);
  const GLOW = S.ramp(["#eafff4", "#7dffc0", "#5fbf9a"]);
  const AURORA = S.ramp(["#6dffb0", "#4fe0c8", "#62a8ff", "#9a7bff"]);

  function flake(p, x, y, size, a = 1) {
    if (S.inZone(x, y)) return;
    const at = wrap(Math.floor(x), W);
    p.dot(at, y, ICE[0], a);
    if (size > 1) p.dot(at + 1, y + 1, ICE[3], a * 0.55);
    if (size < 3) return;
    p.dot(at - 1, y, ICE[1], a);
    p.dot(at + 1, y, ICE[1], a);
    p.dot(at, y - 1, ICE[1], a);
    p.dot(at, y + 1, ICE[1], a);
  }

  function clearNear(p, u, info) {
    cycle(u * info.seconds, info.seconds, 12, 601, (r) => r.range(0.5, 0.9), (age, life, r, i) => {
      const k = age / life, x = r.range(3, W - 3), y = r.range(5, 47) + k * 2;
      if (S.inZone(x, y)) return;
      const peak = k > 0.3 && k < 0.6;
      p.px(x, y, peak ? WHITE : ICE[1]);
      if (!peak) return;
      p.dot(x - 1, y, ICE[2], 0.9);
      p.dot(x + 1, y, ICE[2], 0.9);
      p.dot(x, y - 1, ICE[2], 0.9);
      p.dot(x, y + 1, ICE[2], 0.9);
      if (i % 3 === 0) p.glow(Math.floor(x) + 0.5, Math.floor(y) + 0.5, 3, ICE[1], 0.35);
    });
  }

  const clearMist = (p, u) => S.haze(p, u, 45, 56, ICE[0], 0.24, 611);

  function fallFar(p, u) {
    S.veil(p, 0, GROUND, OVERCAST[1], (v) => 0.3 - v * 0.18);
    const t = S.tile(p);
    for (let i = 0; i < 64; i++) {
      const k = wrap(u + rand(i, 1, 621), 1), y = k * 54;
      const x = rand(i, 2, 621) * W + Math.sin((u * 2 + rand(i, 3, 621)) * TAU) * 1.5;
      if (!S.inZone(x, y)) t.blend(x, y, ICE[0], 0.85);
    }
  }

  function fallNear(p, u) {
    for (let i = 0; i < 28; i++) {
      const k = wrap(u + rand(i, 1, 631), 1), y = -3 + k * (GROUND + 4);
      const x = (i / 28) * W + rand(i, 2, 631) * 5 + Math.sin((u * 2 + rand(i, 3, 631)) * TAU) * 2.2;
      flake(p, x, y, 1 + (i % 3), y > GROUND - 4 ? 0.5 : 1);
    }
  }

  const fallMist = (p, u) => S.haze(p, u, 44, 57, ICE[0], 0.3, 641);

  function blizzardFar(p, u) {
    S.veil(p, 0, H, OVERCAST[2], (v) => 0.46 - v * 0.2);
    const t = S.tile(p);
    for (let i = 0; i < 80; i++) {
      const x = rand(i, 1, 651) * W + u * W * 2, y = wrap(rand(i, 2, 651) * H + u * H, H);
      if (y > GROUND || S.inZone(x, y)) continue;
      t.blend(x, y, ICE[0], 0.7);
      t.blend(x - 1, y - 0.5, ICE[1], 0.45);
      t.blend(x - 2, y - 1, ICE[1], 0.25);
    }
  }

  function blizzardNear(p, u) {
    const t = S.tile(p);
    for (let i = 0; i < 46; i++) {
      const x = rand(i, 1, 661) * W + u * W * 3, y = wrap(rand(i, 2, 661) * H + u * H * 2, H);
      if (y > GROUND + 2) continue;
      for (let s = 0; s < 4; s++) {
        const xx = x - s, yy = y - s * 0.6;
        if (S.inZone(xx, yy)) continue;
        t.dot(xx, yy, s < 2 ? ICE[0] : ICE[1], 1 - s * 0.2);
      }
    }
    for (let i = 0; i < 3; i++) {
      const k = wrap(u + i / 3, 1), head = -20 + k * (W + 50), y = 12 + i * 13, a = Math.sin(k * Math.PI);
      for (let s = 0; s < 26; s++) {
        const xx = head - s, yy = y + Math.sin(s * 0.22 + i) * 2.4;
        if (xx < 0 || xx >= W || S.inZone(xx, yy)) continue;
        p.dot(xx, yy, ICE[0], a * (1.6 - s / 18));
      }
    }
  }

  function blizzardMist(p, u) {
    S.haze(p, u, 38, 57, ICE[0], 0.42, 671, 3, 0.08);
    S.haze(p, u, 46, 58, ICE[1], 0.3, 672, 2, 0.12);
  }

  function frostFar(p, u) {
    S.veil(p, 0, 34, ICE[2], (v) => 0.26 * (1 - v));
    for (let i = 0; i < 34; i++) {
      const x = Math.floor(rand(i, 1, 681) * W), y = Math.floor(rand(i, 2, 681) * 30), tick = S.beat(u, 16, Math.floor(rand(i, 3, 681) * 16));
      if (tick < 5) p.dot(x, y, tick === 2 ? WHITE : ICE[1], tick === 2 ? 1 : 0.7);
    }
  }

  function frostNear(p, u, info) {
    cycle(u * info.seconds, info.seconds, 11, 691, (r) => r.range(1.4, 2.2), (age, life, r, i) => {
      const k = age / life, x = r.range(4, W - 4) + Math.sin(k * 3 + i) * 1.5, y = r.range(8, 40) + k * 7;
      const a = Math.sin(k * Math.PI), tick = Math.floor(age * 8) % 4;
      flake(p, x, y, tick === 0 ? 3 : 1, a);
      if (tick === 0 && a > 0.6 && !S.inZone(x, y)) p.glow(Math.floor(x) + 0.5, y + 0.5, 3.5, ICE[2], 0.35);
    });
  }

  const frostMist = (p, u) => S.haze(p, u, 41, 57, ICE[2], 0.34, 701, 1, 0.06);

  function aurora(p, u) {
    for (let band = 0; band < 3; band++) {
      const turn = band === 1 ? -1 : 1;
      for (let x = 0; x < W; x++) {
        const v = x / W;
        const base = 8 + band * 4 + 3 * Math.sin((v + u * turn + band * 0.3) * TAU) + 1.5 * Math.sin((v * 3 - u * turn + band * 0.17) * TAU);
        const tall = 6 + 3 * Math.sin((v * 2 + u + band * 0.4) * TAU), streak = 0.65 + 0.35 * rand(x, band, 711);
        const bright = (0.5 + 0.5 * Math.sin((v * 2 - u * 2 + band * 0.21) * TAU)) * streak * (band === 2 ? 0.7 : 1);
        for (let k = 0; k < tall; k++) {
          const t = k / tall, y = Math.round(base - k);
          if (y < 0) break;
          p.blend(x, y, AURORA[Math.min(3, Math.floor(t * 4))], 0.85 * bright * (1 - t) ** 1.1);
        }
        if (x % 9 === band * 3 && bright > 0.5) p.glow(x, base - 1, 7, AURORA[band === 2 ? 2 : 0], 0.32 * bright);
      }
    }
  }

  const HARE = pal({ b: "#eef1f9", s: "#9fb0d2", w: "#ffffff", k: "#1f1c2a", e: "#3a3648", i: "#f0b8bc", n: "#d98a94" });

  function mound(p, lean, a = 1) {
    for (let x = -6; x <= 6; x++) {
      const tall = Math.round(3.2 * Math.sqrt(Math.max(0, 1 - (x / 6.5) ** 2)));
      for (let k = 0; k < tall; k++) p.dot(x + (k === tall - 1 ? Math.sign(lean) * 0 : 0), -k, k === tall - 1 ? ICE[0] : x < -2 && k === 0 ? ICE[2] : ICE[1], a);
    }
    if (lean) {
      p.dot(-7 * Math.sign(lean), -3, ICE[0], a);
      p.dot(-6 * Math.sign(lean), -4, ICE[1], a * 0.8);
    }
  }

  const hare = rabbitKind(HARE, mound, ICE[0]);

  const FINCH = pal({ k: "#22212c", r: "#e8554e", g: "#8f98aa", t: "#3a3c4c", w: "#f4f6fb" });
  const FINCH_SIT = [".kk.", "krrg", ".rgt"];
  const FINCH_PUFF = [".kk.", "krrg", "rrgt"];
  const FINCH_UP = [".kkg", "krrg", ".r.t"];
  const FINCH_DOWN = [".kk.", "krrg", ".rgg"];

  function finchAt(i, u) {
    const turn = i === 1 ? -1 : 1;
    return [
      13 * Math.sin((u * turn + i * 0.33) * TAU) + 2 * Math.sin((u * 3 + i * 0.2) * TAU),
      5 * Math.sin((u * 2 + i * 0.21) * TAU) + 1.5 * Math.sin((u * 6 + i * 0.4) * TAU) + (i - 1) * 3,
    ];
  }

  function finchFly(p, x, y, tick, flip, a = 1) {
    blit(p, tick % 2 ? FINCH_UP : FINCH_DOWN, FINCH, Math.round(x) - 2, Math.round(y) - 1 + (tick % 2 ? 0 : 1), { flip, a });
  }

  function bullfinchesCalm(p, u, info) {
    const tick = Math.round(u * info.frames);
    for (let i = 0; i < 3; i++) {
      const [x, y] = finchAt(i, u), [next] = finchAt(i, u + 0.01);
      finchFly(p, x, y, tick + i, next > x);
    }
  }

  const TWIGS = [[-7, -8], [-1, -11], [6, -7]];

  function bullfinchesFriendly(p, u, info) {
    const tick = Math.round(u * info.frames) % 16;
    TWIGS.forEach(([x, y], i) => {
      const beat = (tick + i * 5) % 16, hop = beat === 6 ? -1 : 0;
      blit(p, beat === 10 || beat === 11 ? FINCH_PUFF : FINCH_SIT, FINCH, x - 2, y - 2 + hop, { flip: beat >= 12 ? i !== 1 : i === 1 });
    });
  }

  function bullfinchesAlarmed(p, u, info) {
    const tick = Math.round(u * (info.frames - 1));
    if (tick >= info.frames - 1) return;
    for (let i = 0; i < 3; i++) {
      const [x0, y0] = finchAt(i, 0), k = ease.outQuad(u);
      finchFly(p, x0 + (i - 1) * 10 * k - k * 6, y0 - 17 * k, tick + i, false, u > 0.6 ? 1 - (u - 0.6) / 0.4 : 1);
    }
  }

  function bullfinchesHidden(p, u, info) {
    const tick = Math.round(u * (info.frames - 1));
    if (tick >= info.frames - 1) return;
    for (let i = 0; i < 3; i++) {
      const [x0, y0] = finchAt(i, 0), k = ease.inQuad(u);
      finchFly(p, x0 - k * 10, y0 + (9 - y0) * k, tick + i, false, u > 0.55 ? 1 - (u - 0.55) / 0.45 : 1);
    }
  }

  const BUSH = pal({ d: "#5a4a48", l: "#7a6660", w: "#ffffff", s: "#c5d0e6" });
  const BUSH_ROWS = [
    ".........w.........",
    "........wdw........",
    "..ww....sd....ww...",
    ".wddw....d...wddw..",
    "..s.dd...d..dd.s...",
    ".....dd..d.dd......",
    "......dd.ddd.......",
    ".......ddld........",
    "........dld........",
    "........dld........",
    ".......wdldw.......",
    ".....wwwwwwwww.....",
  ];

  const bullfinchesPerch = (p) => blit(p, BUSH_ROWS, BUSH, -9, -11);

  function print(p, x, y, a) {
    p.dot(x, y, ICE[4], a);
    p.dot(x + 1, y, ICE[3], a);
    p.dot(x, y - 1, ICE[0], a * 0.7);
  }

  function trail(p, i, frames, dir) {
    for (let n = 0; n < 9; n++) {
      const age = wrap(i - n * 3, frames);
      if (age > 16) continue;
      print(p, dir * (-16 + n * 4), n % 2 ? -1 : -3, age < 10 ? 1 : 1 - (age - 10) / 7);
    }
  }

  const tracksCalm = (p, u, info) => trail(p, Math.round(u * info.frames), info.frames, 1);
  const tracksFriendly = (p, u, info) => trail(p, Math.round(u * info.frames), info.frames, -1);

  function tracksAlarmed(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    for (let n = 0; n < 9; n++) {
      print(p, -16 + n * 4, n % 2 ? -1 : -3, 1 - u);
      if (n % 3 === 0) K.puff(p, -16 + n * 4, -2 - u * 3, 1 + u * 2, ICE[0], 0.5 * (1 - u));
    }
  }

  function tracksHidden(p, u, info) {
    if (Math.round(u * (info.frames - 1)) >= info.frames - 1) return;
    for (let n = 0; n < 9; n++) print(p, -16 + n * 4, n % 2 ? -1 : -3, 1 - u);
  }

  const glints = fliesKind(GLOW);

  const FOX = pal({ w: "#f6f8fd", s: "#9fb0d2", t: "#dbe4f5", k: "#1f1c2a", n: "#2a2634", e: "#8497c0" });
  const FOX_SIT = [
    "..e.e........",
    "..www........",
    ".wkwww.......",
    "nwwwwww......",
    ".swwwwww.....",
    "..wwwwwww....",
    "..wwwwwwwtt..",
    "..ww.wwsttttw",
    "..ss.ss..ttt.",
  ];
  const FOX_WAG = [...FOX_SIT.slice(0, 6), "..wwwwwww.tt.", "..ww.wwsttttt", "..ss.ss.tttw."];
  const FOX_TROT = [
    ".e.e..........",
    ".www..........",
    "kwwwwwwwww.ttt",
    "n.wwwwwwwwtttw",
    "..swwwwwws.tt.",
    "..s..s.s..s...",
  ];
  const FOX_TROT_B = [...FOX_TROT.slice(0, 5), ".s....ss...s.."];
  const FOX_DIVE = [
    "......ttw",
    ".....ttt.",
    "....www..",
    "...wwww..",
    "..wwww...",
    ".ewws....",
    ".kw......",
    ".n.......",
  ];

  function shadow(p) {
    for (let x = -5; x <= 7; x++) p.px(x, 1, x < -3 || x > 5 ? ICE[2] : ICE[3]);
  }

  function foxCalm(p, u, info) {
    const i = Math.round(u * info.frames), swap = {};
    if (i === 12 || i === 13) swap.k = "w";
    const look = i >= 20 && i < 26 ? 1 : 0;
    const rows = i % 16 >= 12 ? FOX_WAG : FOX_SIT;
    shadow(p);
    blit(p, rows.slice(4), FOX, -6, -4);
    blit(p, (i === 5 || i === 6 ? ["...ee........", ...rows.slice(1, 4)] : rows.slice(0, 4)), FOX, -6 + look, -8, { swap });
  }

  function foxFriendly(p, u, info) {
    const i = Math.round(u * info.frames) % 16, tilt = i >= 8 && i < 12 ? 1 : 0;
    const rows = i % 4 < 2 ? FOX_WAG : FOX_SIT;
    shadow(p);
    blit(p, rows.slice(4), FOX, -6, -4);
    blit(p, rows.slice(0, 4), FOX, -6 - tilt, -8 + tilt, { swap: i === 5 ? { k: "w" } : {} });
  }

  function foxAlarmed(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    if (i < 2) return blit(p, FOX_TROT, FOX, -6, -5 - i);
    const step = i - 2, x = -6 - step * 3.2;
    blit(p, step % 2 ? FOX_TROT_B : FOX_TROT, FOX, x, -5 - (step % 2), { a: step > 6 ? 1 - (step - 6) / 5 : 1 });
    for (let n = 0; n < 3; n++) {
      const age = step - n * 2;
      if (age >= 0 && age <= 4) K.puff(p, 5 - n * 9 + age * 0.6, -1 - age * 0.5, 1.2 + age * 0.4, ICE[0], 0.55 * (1 - age / 5));
    }
  }

  function foxHidden(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    if (i < 2) return blit(p, FOX_TROT, FOX, -6, -5 + i);
    if (i < 4) return blit(p, FOX_TROT, FOX, -6, -5 - (i - 1) * 4);
    const fall = i - 4, y = -15 + fall * 3.5;
    if (fall < 5) blit(p, FOX_DIVE, FOX, -5, y, { clip: 1 });
    if (fall >= 2) for (let n = -1; n <= 1; n++) K.puff(p, n * 4 - 1, -1 - (fall - 2) * 0.8, 1.5 + (fall - 2) * 0.5, ICE[0], 0.6 * (1 - (fall - 2) / 5));
  }

  const bossOut = gustKind([ICE[0], ICE[1], ICE[0]]);

  function slab(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    const landed = 36;
    if (i < 8) {
      const y = i < 2 ? i : 1 + (i - 1) ** 2 * 0.72, tip = i > 1 ? Math.min(3, i - 1) : 0;
      for (let x = -4; x <= 4; x++) {
        const lift = Math.round((x / 4) * tip * 0.6);
        p.px(x, y + lift, ICE[0]);
        p.px(x, y + lift + 1, Math.abs(x) < 4 ? ICE[1] : ICE[2]);
        if (Math.abs(x) < 3) p.px(x, y + lift + 2, ICE[2]);
      }
      for (let n = 0; n < 4; n++) p.dot(-3 + n * 2 + ((i + n) % 2), y - 2 - n * 1.5 - i * 0.4, ICE[1], 0.8 - n * 0.15);
      return;
    }
    const k = (i - 8) / 5;
    for (let n = 0; n < 6; n++) {
      const side = n - 2.5;
      K.puff(p, side * (2 + k * 3.4), landed - Math.sin(k * Math.PI * 0.7) * (5 + Math.abs(side)) + 2, 2.4 + k * 2, n % 2 ? ICE[0] : ICE[1], 0.75 * (1 - k));
    }
    for (let n = 0; n < 7; n++) p.dot((n - 3) * (1.5 + k * 3), landed - 4 - k * (6 + (n % 3) * 3) + k * k * 8, WHITE, 1 - k);
  }

  function bossGround(p, u) {
    const t = S.tile(p);
    for (let i = 0; i < 26; i++) {
      const x = rand(i, 1, 721) * W - u * W * (1 + (i % 2)), y = -1 - Math.floor(rand(i, 2, 721) * 7), len = 5 + (i % 5);
      for (let s = 0; s < len; s++) t.blend(x + s, y - Math.round(s * 0.15), s < 2 ? ICE[0] : ICE[1], 0.85 - (s / len) * 0.6);
    }
  }

  function kick(p, u, info, tier) {
    cycle(u * info.seconds, info.seconds, 3 + tier * 3, 731 + tier, (r) => r.range(0.3, 0.55), (age, life, r, i) => {
      const k = age / life, side = i % 2 ? 1 : -1;
      const x = side * (3 + k * r.range(5, 7 + tier * 3)), y = -1 - Math.sin(k * Math.PI) * r.range(1.5, 2 + tier * 1.6);
      K.puff(p, x, y, 1 + k * (0.8 + tier * 0.35), i % 3 ? ICE[0] : ICE[1], 0.7 * (1 - k));
      if (tier === 3 && i % 3 === 0) p.dot(x + side * 2, y - 3 - k * 4, WHITE, 1 - k);
    });
  }

  const sway1 = (p, u, info) => kick(p, u, info, 1);
  const sway2 = (p, u, info) => kick(p, u, info, 2);
  const sway3 = (p, u, info) => kick(p, u, info, 3);

  function sparkle(p, u) {
    for (let i = 0; i < 30; i++) {
      const x = Math.floor(rand(i, 1, 741) * W), y = Math.floor(i % 3 === 0 ? 49 + rand(i, 2, 741) * 8 : 12 + rand(i, 2, 741) * 36);
      const tick = S.beat(u, 16, Math.floor(rand(i, 3, 741) * 16));
      if (tick > 2 || S.inZone(x, y)) continue;
      p.px(x, y, WHITE);
      if (tick !== 1) continue;
      p.dot(x - 1, y, ICE[1], 0.9);
      p.dot(x + 1, y, ICE[1], 0.9);
      p.dot(x, y - 1, ICE[1], 0.9);
      p.dot(x, y + 1, ICE[1], 0.9);
      p.glow(x + 0.5, y + 0.5, 3, ICE[1], 0.3);
    }
  }

  function slide(p, u, info) {
    const i = Math.round(u * info.frames);
    if (i < 14) {
      for (let x = -2; x <= 2; x++) p.px(x, i >= 11 ? (i - 11) % 2 : 0, x === 2 ? ICE[2] : ICE[0]);
      return p.px(0, 1, ICE[1]);
    }
    if (i < 24) {
      const k = (i - 14) / 9, x = k * 6, y = k * k * 14;
      for (let d = -2; d <= 2; d++) p.px(x + d, y + Math.round(d * 0.5), Math.abs(d) === 2 ? ICE[1] : ICE[0]);
      for (let n = 1; n < 4; n++) p.dot(x - n * 1.4, y - n * 2.2, ICE[1], 0.8 - n * 0.2);
      return;
    }
    const k = (i - 24) / 6;
    if (k < 1) for (let n = -1; n <= 1; n++) K.puff(p, 6 + n * (2 + k * 3), 14 - k * 2, 1.6 + k * 1.6, ICE[0], 0.7 * (1 - k));
  }

  function iceGlow(p, u) {
    for (let x = 0; x < W; x++) {
      const wave = 0.5 + 0.5 * Math.sin((x / W * 3 - u) * TAU), seam = rand(x, 751) < 0.3;
      p.blend(x, 0, ICE[4], 0.3 + 0.4 * wave);
      p.blend(x, 1, ICE[3], 0.2 + 0.3 * wave);
      if (seam && wave > 0.6) p.px(x, 0, hex("#9fd4ff"));
      if (x % 14 === 0) p.glow(x + 0.5, 0.5, 6, hex("#5fa8ff"), 0.18 + 0.3 * wave);
    }
  }

  function bigFlakes(p, u) {
    for (let i = 0; i < 16; i++) {
      const k = wrap(u + rand(i, 1, 761), 1), y = -3 + k * (GROUND + 5);
      flake(p, (i / 16) * W + rand(i, 2, 761) * 8 + Math.sin((u + rand(i, 3, 761)) * TAU) * 3, y, i % 4 === 0 ? 2 : 3, y > GROUND - 5 ? 0.5 : 1);
    }
  }

  return {
    snowClearNear: clearNear, snowClearMist: clearMist, snowFallFar: fallFar, snowFallNear: fallNear, snowFallMist: fallMist,
    snowBlizzardFar: blizzardFar, snowBlizzardNear: blizzardNear, snowBlizzardMist: blizzardMist,
    snowFrostFar: frostFar, snowFrostNear: frostNear, snowFrostMist: frostMist, snowAurora: aurora,
    hareCalm: hare.calm, hareFriendly: hare.friendly, hareAlarmed: hare.alarmed, hareHidden: hare.hidden, hareHop: hare.hop,
    bullfinchesCalm, bullfinchesFriendly, bullfinchesAlarmed, bullfinchesHidden, bullfinchesPerch,
    tracksCalm, tracksFriendly, tracksAlarmed, tracksHidden,
    auroraGlintsCalm: glints.calm, auroraGlintsFriendly: glints.friendly, auroraGlintsAlarmed: glints.alarmed, auroraGlintsHidden: glints.hidden,
    arcticFoxCalm: foxCalm, arcticFoxFriendly: foxFriendly, arcticFoxAlarmed: foxAlarmed, arcticFoxHidden: foxHidden,
    snowBossOut: bossOut, snowSlab: slab, snowBossGround: bossGround, snowSway1: sway1, snowSway2: sway2, snowSway3: sway3,
    snowSparkle: sparkle, snowSlide: slide, snowIceGlow: iceGlow, snowBigFlakes: bigFlakes,
  };
}
