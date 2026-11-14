export default function loot(fx) {
  const { clamp, seg, bump, TAU, cycle, burst, hex, mix, scale, WHITE } = fx;
  const K = fx.kit;
  const LOOP = 8 / 12;
  const RARITIES = ["#9aa3b5", "#e6e9f0", "#4fc277", "#5b9bff", "#b07cff", "#ff9a3c", "#ff5a5a", "#3fe0d0", null, "#5c66d6", "#fff4d6"];
  const RAINBOW = K.ramp(["#ff5a5a", "#ff9a3c", "#ffe08a", "#4fc277", "#3fe0d0", "#5b9bff", "#b07cff"]);
  const NIGHT = K.ramp(["#12154a", "#1e2370", "#5c66d6", "#b8c2ff"]);
  const GOLD = K.ramp(["#6a3f0e", "#b67c1c", "#f0b43c", "#ffd27a", "#fff3c4"]);
  const SETS = K.ramp(["#1f6a58", "#3fe0d0", "#c8fff4"]);
  const WOOD = K.ramp(["#3b2616", "#6a4326", "#96643a", "#c08a52"]);
  const PAPER = K.ramp(["#8a7a5c", "#d8c8a0", "#f4ead0", "#fffaf0"]);
  const WAX = K.ramp(["#7a1f1a", "#c8382e", "#ff7a6a"]);
  const RIFT = K.ramp(["#2a1458", "#5a34b8", "#8f5bff", "#e8daff"]);
  const STEEL = K.ramp(["#4a5578", "#9fb4d6", "#e6e9f0"]);

  const shape = (r) => ({ tall: 14 + Math.round(r * 2.7), half: r < 2 ? 0 : r < 5 ? 1 : r < 8 ? 2 : 3 });

  function tones(r, row, tall, phase) {
    if (r === 8) {
      const at = (i) => RAINBOW[((Math.floor(row / 3 - phase * RAINBOW.length) + i) % RAINBOW.length + RAINBOW.length) % RAINBOW.length];
      return [scale(at(0), 0.55), at(0), mix(at(0), WHITE, 0.55)];
    }
    if (r === 9) return [NIGHT[0], NIGHT[1], NIGHT[2]];
    const base = hex(RARITIES[r]);
    if (r === 10) return [GOLD[2], base, WHITE];
    return [scale(base, 0.5), base, mix(base, WHITE, 0.6)];
  }

  function column(p, r, phase) {
    const { tall, half } = shape(r);
    for (let row = 0; row < tall; row++) {
      const v = row / tall, [dark, mid, light] = tones(r, row, tall, phase);
      const band = (((row / 9 - phase) % 1) + 1) % 1 < 0.22;
      const thin = v > 0.8 ? 1 : 0;
      for (let x = -half - 1; x <= half; x++) {
        const edge = x === -half - 1 || x === half;
        const core = half === 0 ? true : Math.abs(x + 0.5) < Math.max(1, half - 0.5);
        if (v > 0.62 && (x + row) % 2 === (v > 0.84 ? 0 : 2)) continue;
        if (v > 0.84 && (x + row) % 2) continue;
        if (thin && edge && half > 0) continue;
        p.px(x, -row, edge && half > 0 ? dark : core ? (band ? WHITE : light) : band ? light : mid);
      }
    }
    return tall;
  }

  function beam(r) {
    return (p, phase) => {
      const { tall, half } = shape(r), [dark, mid, light] = tones(r, 4, tall, phase);
      if (r >= 2) K.floorRing(p, 0, 0, 3 + half + r * 0.45, 1 + (r > 5 ? 1 : 0), light, dark);
      if (r >= 5)
        for (const side of [-1, 1]) {
          const reach = tall * (0.45 + 0.1 * K.pulse(phase, 2, side * 0.25));
          for (let row = 0; row < reach; row++) if ((row + Math.floor(phase * 8)) % 3) p.px(side * (half + 3) - (side < 0 ? 1 : 0), -row, row > reach * 0.7 ? dark : mid);
        }
      column(p, r, phase);
      cycle(phase * LOOP, LOOP, 2 + r, 40 + r, (g) => g.range(0.35, 0.6), (age, life, g) => {
        const x = g.range(-half - 4, half + 4), k = age / life;
        p.px(x + Math.sin(k * 5 + g.next() * 6), -g.range(0, tall * 0.5) - k * tall * 0.5, k < 0.5 ? WHITE : k < 0.8 ? light : mid);
      });
      if (r >= 7)
        cycle(phase * LOOP, LOOP, r - 4, 60 + r, () => 0.33, (age, life, g) => {
          fx.sparkle(p, g.range(-7, 7), -g.range(4, tall), 2.4 * Math.sin((age / life) * Math.PI), WHITE, light, 0.9);
        });
      if (r === 9)
        burst(9, 71, (g, i) => {
          if (K.pulse(phase, 1 + (i % 2), g.next()) > 0.55) p.px(g.range(-9, 9), -g.range(2, tall), i % 3 ? NIGHT[3] : WHITE);
        });
      if (r === 10) {
        K.flame(p, 0, 0, 3.4, 9, phase, [GOLD[2], GOLD[3], GOLD[4], WHITE, WHITE], 3, 1);
        fx.sparkle(p, 0, -tall + 1, 2 + K.pulse(phase, 2), WHITE, GOLD[3], 0.9);
      }
      p.glow(0, -tall * 0.35, 5 + r * 0.9, r === 9 ? NIGHT[2] : mid, 0.1 + r * 0.02);
    };
  }

  function setMark(p, phase) {
    for (let strand = 0; strand < 2; strand++)
      for (let link = 0; link < 6; link++) {
        const rise = ((link / 6 + phase / 6) % 1) * 36, a = (rise / 12 + strand / 2 + phase) * TAU;
        const x = Math.cos(a) * 6, front = Math.sin(a) > 0;
        if (!front) {
          p.px(x, -4 - rise, SETS[0]);
          continue;
        }
        for (const [dx, dy] of [[0, -1], [1, 0], [0, 1], [-1, 0]]) p.px(x + dx, -4 - rise + dy, SETS[1]);
        p.px(x, -4 - rise, SETS[2]);
      }
    K.floorRing(p, 0, 0, 8 + K.pulse(phase), 2, SETS[2], SETS[0]);
  }

  function uniqueMark(p, phase) {
    for (let row = 2; row < 36; row++)
      for (let x = -5; x <= 4; x++) {
        const stripe = (((x + row) / 8 - phase) % 1 + 1) % 1;
        if (stripe < 0.16 && Math.abs(x + 0.5) > 2.5) p.px(x, -row, stripe < 0.08 ? GOLD[4] : GOLD[2]);
      }
    for (const side of [-1, 1]) {
      const y = -38 - K.pulse(phase, 1, side * 0.25);
      p.px(side * 4 - (side < 0 ? 1 : 0), y, GOLD[3]);
    }
    fx.sparkle(p, 0, -41, 3 + 1.5 * K.pulse(phase, 1), GOLD[4], GOLD[2], 0.95);
    p.px(0, -41, WHITE);
  }

  function bundle(p, phase) {
    const lift = Math.round(K.pulse(phase) * 1.4);
    const rows = [".....tt.....", "....tSSt....", "...tsSSst...", "..tssSSsst..", "..tsssssst..", "..tsshssst..", "...tssst...."];
    const ink = { t: WOOD[0], s: WOOD[2], S: WOOD[3], h: GOLD[3] };
    rows.forEach((row, y) => [...row].forEach((ch, x) => ch !== "." && p.px(x - 6, y - 8 - lift, ink[ch])));
    p.px(-1, -9 - lift, GOLD[2]);
    p.px(0, -9 - lift, GOLD[4]);
    for (let x = -3; x <= 2; x++) p.px(x, 0, lift ? WOOD[0] : WOOD[1]);
  }

  function coin(p, phase) {
    const half = [3, 2, 1, 0, 1, 2][Math.floor(phase * 6) % 6], facing = phase < 0.5;
    for (let y = -3; y <= 2; y++) {
      const wide = Math.abs(y + 0.5) > 2 ? Math.max(0, half - 1) : half;
      if (half === 0) {
        p.px(0, y, GOLD[3]);
        p.px(-1, y, GOLD[1]);
        continue;
      }
      for (let x = -wide; x < wide; x++) {
        const rim = x === -wide || x === wide - 1 || Math.abs(y + 0.5) > 2;
        p.px(x, y, rim ? GOLD[1] : facing && x < 0 && y < 0 ? GOLD[4] : GOLD[2 + ((x + y) & 1)]);
      }
    }
    if (half === 3) p.px(-2, -2, WHITE);
  }

  function gem(p, phase) {
    const turn = Math.floor(phase * 6) % 6, shine = turn % 3;
    const rows = ["..llll..", ".lwllmm.", "lwllmmdd", "llllmmdd", ".llmmdd.", ".lmmmdd.", "..mmdd..", "..mddd..", "...dd..."];
    const ink = { l: STEEL[2], m: STEEL[1], d: STEEL[0], w: WHITE };
    rows.forEach((row, y) => [...row].forEach((ch, x) => {
      if (ch === ".") return;
      const moved = (x + shine * 2) % 8;
      p.px(x - 4, y - 5, ch === "w" ? WHITE : moved < 2 && ch !== "d" ? WHITE : ink[ch]);
    }));
    if (turn === 0) fx.sparkle(p, -2, -4, 2, WHITE, STEEL[2], 0.9);
  }

  function spark(p, phase) {
    const beat = K.pulse(phase, 1), arm = 2 + Math.round(beat * 2), turned = Math.floor(phase * 6) % 2;
    for (let d = 1; d <= arm; d++)
      for (const [dx, dy] of turned ? [[1, 1], [-1, 1], [1, -1], [-1, -1]] : [[1, 0], [-1, 0], [0, 1], [0, -1]]) p.px(dx * d, dy * d, d === arm ? RIFT[1] : RIFT[2]);
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) p.px(dx, dy, RIFT[3]);
    p.px(0, 0, WHITE);
    p.glow(0, 0, 4, RIFT[2], 0.25 + 0.15 * beat);
  }

  function chestBox(p, shake, open, glow) {
    const x0 = -9 + shake;
    for (let y = -9; y <= 0; y++)
      for (let x = 0; x < 18; x++) {
        const edge = x === 0 || x === 17 || y === 0 || y === -9, band = x === 3 || x === 14;
        p.px(x0 + x, y, edge ? WOOD[0] : band ? GOLD[2] : y < -6 ? WOOD[3] : (x + y) % 5 === 0 ? WOOD[1] : WOOD[2]);
      }
    const hinge = -10, tilt = open;
    for (let row = 0; row < 6; row++) {
      const lift = row + Math.round(tilt * 5), lean = Math.round(tilt * row * 0.5);
      for (let x = 0; x < 18; x++) {
        const edge = x === 0 || x === 17 || row === 5 || row === 0, band = x === 3 || x === 14;
        p.px(x0 + x - lean, hinge - lift, edge ? WOOD[0] : band ? GOLD[3] : row > 3 ? WOOD[3] : WOOD[2]);
      }
    }
    if (open < 0.2) {
      p.px(x0 + 8, -9, GOLD[4]);
      p.px(x0 + 9, -9, GOLD[2]);
      p.px(x0 + 8, -8, GOLD[1]);
      p.px(x0 + 9, -8, GOLD[1]);
    }
    if (glow > 0) {
      for (let x = 2; x < 16; x++) p.px(x0 + x, -10, x % 3 ? GOLD[4] : WHITE);
      p.glow(0, -12, 12, GOLD[3], 0.45 * glow);
    }
  }

  function chestOpen(p, u, info) {
    const time = u * info.seconds;
    const shake = u < 0.25 ? Math.round(Math.sin(u * 60) * 1.2) : 0;
    const open = fx.ease.outBack ? clamp(fx.ease.outBack(seg(u, 0.25, 0.5)), 0, 1.15) : seg(u, 0.25, 0.5);
    chestBox(p, shake, Math.min(1, open), seg(u, 0.3, 0.45) * (1 - 0.4 * seg(u, 0.8, 1)));
    const pop = bump(u, 0.3, 0.62);
    if (pop > 0) {
      for (let i = 0; i < 7; i++) {
        const a = -Math.PI / 2 + (i - 3) * 0.24, reach = 8 + pop * 12;
        p.line(Math.cos(a) * 4, -11 + Math.sin(a) * 4, Math.cos(a) * reach, -11 + Math.sin(a) * reach, i % 2 ? GOLD[3] : GOLD[4], pop);
      }
      fx.sparkle(p, 0, -13, 6 * pop, WHITE, GOLD[3], 0.9);
    }
    burst(9, 131, (g) => {
      const a = g.range(-2.4, -0.75), speed = g.range(30, 58);
      K.spark(p, g.range(-5, 5), -11, Math.cos(a) * speed, Math.sin(a) * speed, time - 0.22 - g.range(0, 0.08), g.range(0.3, 0.46), [WHITE, GOLD[4], GOLD[3], GOLD[2], GOLD[1]], 150, 0.4, 0.04);
    });
  }

  function envelope(p, shake, flap, card) {
    const x0 = -10 + shake;
    if (card > 0) {
      const rise = Math.round(card * 9);
      for (let y = 0; y < 9; y++) for (let x = 3; x < 17; x++) p.px(x0 + x, -8 - rise + y - 4, x === 3 || x === 16 || y === 0 ? PAPER[0] : y % 3 === 2 && x > 5 && x < 14 ? PAPER[1] : PAPER[3]);
    }
    for (let y = -12; y <= 0; y++)
      for (let x = 0; x < 20; x++) {
        const edge = x === 0 || x === 19 || y === 0 || y === -12;
        const fold = Math.abs(x - 9.5) === Math.abs(Math.round((y + 12) * 0.8) - 9.5) || Math.round(Math.abs(x - 9.5)) === Math.round((-y) * 0.8);
        p.px(x0 + x, y, edge ? PAPER[0] : fold && y > -11 ? PAPER[1] : PAPER[2]);
      }
    const depth = Math.round(7 * (1 - flap * 2));
    if (depth >= 0) {
      for (let row = 0; row <= depth; row++) {
        const inset = Math.round((row / 7) * 9.5);
        for (let x = inset; x < 20 - inset; x++) p.px(x0 + x, -12 + row, x === inset || x === 19 - inset || row === depth ? PAPER[0] : PAPER[3]);
      }
    } else {
      for (let row = 0; row <= -depth; row++) {
        const inset = Math.round((row / 7) * 9.5);
        for (let x = inset; x < 20 - inset; x++) p.px(x0 + x, -13 - row, x === inset || x === 19 - inset ? PAPER[0] : PAPER[1]);
      }
    }
  }

  function letterOpen(p, u, info) {
    const time = u * info.seconds;
    const shake = u < 0.2 ? Math.round(Math.sin(u * 70) * 1.2) : 0;
    const flap = seg(u, 0.28, 0.55), card = fx.ease.outCubic ? fx.ease.outCubic(seg(u, 0.5, 0.85)) : seg(u, 0.5, 0.85);
    envelope(p, shake, flap, card);
    if (u < 0.3) {
      const crack = seg(u, 0.18, 0.3);
      for (let y = -2; y <= 1; y++) for (let x = -2; x <= 1; x++) if (Math.abs(x + 0.5) + Math.abs(y + 0.5) < 3) p.px(x + shake, -6 + y, x < 0 && y < 0 ? WAX[2] : (x + y) % 2 ? WAX[1] : WAX[0]);
      if (crack > 0) p.line(-2 + shake, -8, 1 + shake, -5, PAPER[3]);
    }
    burst(6, 151, (g) => {
      const a = g.range(-Math.PI, 0), speed = g.range(24, 46);
      K.spark(p, 0, -6, Math.cos(a) * speed, Math.sin(a) * speed, time - 0.2, g.range(0.2, 0.34), [WAX[2], WAX[1], WAX[0]], 170, 0.6);
    });
    const shine = bump(u, 0.55, 1);
    if (shine > 0) {
      fx.sparkle(p, 0, -20, 5 * shine, WHITE, GOLD[3], 0.9);
      p.glow(0, -18, 10, GOLD[3], 0.3 * shine);
      cycle(time, 0.3, 5, 157, () => 0.3, (age, life, g) => p.px(g.range(-8, 8), -14 - g.range(0, 10) - (age / life) * 4, age < 0.15 ? WHITE : GOLD[3]));
    }
  }

  const parts = { setMark, uniqueMark, bundle, coin, gem, spark, chestOpen, letterOpen };
  RARITIES.forEach((_, r) => (parts[`beam${r}`] = beam(r)));
  return parts;
}
