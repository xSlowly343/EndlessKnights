export default function artifact(fx) {
  const { seg, bump, ease, burst, cycle, TAU, WHITE, hex, mix, scale } = fx;
  const K = fx.kit;
  const GROUPS = ["#5b9bff", "#4fc277", "#ff9a3c", "#e0574a"];
  const EDGE = hex("#e8daff");
  const VOID = hex("#0a0614");
  const RIFT = hex("#8f5bff");
  const CY = -34;
  const HALF_W = 5;
  const HALF_H = 8;
  const RUNES = [["x.x", ".x.", "x.x"], [".x.", "xxx", ".x."], ["xx.", ".x.", ".xx"], ["x.x", "x.x", ".x."], ["xxx", "x..", "xxx"], [".x.", "x.x", "xxx"]];

  const tones = (group) => {
    const base = hex(GROUPS[group]);
    return { deep: scale(base, 0.35), dark: scale(base, 0.6), mid: base, light: mix(base, WHITE, 0.5), pale: mix(base, WHITE, 0.8) };
  };

  const exit = (p, u, from = 0.86) => (p.fade = 1 - 0.7 * seg(u, from, 1));

  function relic(p, cx, cy, grow, t, shine, squash = 0) {
    const hw = HALF_W * grow + squash, hh = HALF_H * grow - squash;
    if (hh < 1) return;
    for (let y = -Math.ceil(hh); y <= Math.ceil(hh); y++) {
      const half = hw * (1 - Math.abs(y) / (hh + 0.5));
      if (half < 0.3) {
        if (Math.abs(y) <= hh) p.px(cx, cy + y, t.deep);
        continue;
      }
      const left = Math.round(-half), right = Math.round(half);
      for (let x = left; x <= right; x++) {
        const rim = x === left || x === right || Math.abs(y) >= hh - 0.5;
        const upper = y < 0, west = x < 0;
        const face = upper ? (west ? t.pale : t.light) : west ? t.mid : t.dark;
        const glint = shine >= 0 && Math.abs(x + y * 0.6 - (shine * 22 - 11)) < 1.2;
        p.px(cx + x, cy + y, rim ? t.deep : glint ? WHITE : x === 0 ? (upper ? WHITE : t.light) : face);
      }
    }
  }

  function motes(p, time, t, seed, count, reach) {
    cycle(time, 0.5, count, seed, (g) => g.range(0.3, 0.5), (age, life, g) => {
      const a = g.range(0, TAU), k = age / life, r = reach * (0.5 + 0.5 * k);
      p.dot(Math.cos(a + k * 2) * r, CY + Math.sin(a + k * 2) * r * 0.7 - k * 4, k < 0.4 ? WHITE : k < 0.75 ? t.light : t.mid, 1 - k * 0.6);
    });
  }

  function open(group) {
    const t = tones(group);
    return (p, u, info) => {
      const time = u * info.seconds;
      exit(p, u);
      const crack = seg(u, 0, 0.22), split = seg(u, 0.2, 0.36), out = ease.outBack(seg(u, 0.26, 0.5)), settle = seg(u, 0.8, 1);
      const bob = out >= 1 ? Math.round(Math.sin(seg(u, 0.5, 1) * TAU) * 1.2) + Math.round(settle * 2) : 0;
      if (split < 1) {
        const reach = 2 + crack * 8, wide = split * 4;
        for (let y = -reach; y <= reach; y++) {
          const jag = Math.round(Math.sin(y * 1.9) * 1.3 + (crack < 1 ? Math.sin(y * 7 + time * 60) * 0.6 : 0));
          const gap = Math.round(wide * (1 - Math.abs(y) / (reach + 1)));
          for (let x = -gap; x <= gap; x++) p.px(jag + x, CY + y, VOID);
          p.px(jag - gap - 1, CY + y, Math.abs(y) > reach - 2 ? RIFT : EDGE);
          p.px(jag + gap + 1, CY + y, Math.abs(y) > reach - 2 ? RIFT : EDGE);
        }
        p.glow(0, CY, 6 + crack * 6, RIFT, 0.3 + 0.3 * crack);
      }
      const flash = bump(u, 0.22, 0.44);
      if (flash > 0) {
        for (let i = 0; i < 8; i++) {
          const a = (i / 8) * TAU + 0.2, r0 = 5, r1 = 7 + flash * 7;
          p.line(Math.cos(a) * r0, CY + Math.sin(a) * r0, Math.cos(a) * r1, CY + Math.sin(a) * r1, i % 2 ? EDGE : t.pale, flash);
        }
        p.glow(0, CY, 12 + flash * 8, t.light, 0.7 * flash);
      }
      if (out > 0) relic(p, 0, CY + bob, Math.min(out, 1.12), t, seg(u, 0.5, 0.78) < 1 ? seg(u, 0.5, 0.78) : -1);
      burst(11, 401 + group, (g, i) => {
        const a = g.range(0, TAU), speed = g.range(26, 52);
        K.spark(p, 0, CY, Math.cos(a) * speed, Math.sin(a) * speed - 12, time - 0.24 * info.seconds, g.range(0.28, 0.5), i % 3 ? [EDGE, RIFT, hex("#5a34b8")] : [WHITE, t.light, t.mid], 120, 1.2, 0.05);
      });
      if (out >= 1) {
        motes(p, time, t, 409 + group, 5, 10);
        p.glow(0, CY + bob, 10, t.mid, 0.3 * (1 - settle * 0.5));
        const wink = bump(u, 0.78, 0.98);
        if (wink > 0) fx.sparkle(p, 3, CY + bob - 5, 3 * wink, WHITE, t.pale, 0.8);
      }
    };
  }

  function awaken(group) {
    const t = tones(group);
    return (p, u, info) => {
      const time = u * info.seconds;
      exit(p, u);
      const brace = bump(u, 0, 0.18), lid = ease.outBack(seg(u, 0.16, 0.44)), rays = seg(u, 0.38, 0.86), blink = bump(u, 0.84, 0.98);
      relic(p, 0, CY, 1 + 0.12 * bump(u, 0.16, 0.5), t, -1, Math.round(brace * 1.5));
      const open = Math.max(0, Math.min(lid, 1.15) * 3 - blink * 3);
      if (lid > 0) {
        for (let x = -4; x <= 4; x++) {
          const half = Math.round(open * (1 - (x * x) / 20));
          for (let y = -half; y <= half; y++) p.px(x, CY + y, Math.abs(y) === half && half > 0 ? t.deep : WHITE);
          if (half === 0) p.px(x, CY, t.deep);
        }
        if (open >= 2) {
          const look = Math.round(Math.sin(seg(u, 0.44, 0.84) * TAU));
          p.px(look, CY, VOID);
          p.px(look, CY - 1, t.deep);
          p.px(look - 1, CY, t.dark);
        }
      }
      if (rays > 0 && rays < 1) {
        const grow = Math.sin(rays * Math.PI), spin = rays * 0.5;
        for (let i = 0; i < 10; i++) {
          const a = (i / 10) * TAU + spin, r0 = 8 + (i % 2), r1 = r0 + grow * (i % 2 ? 3 : 6);
          p.line(Math.cos(a) * r0, CY + Math.sin(a) * r0, Math.cos(a) * r1, CY + Math.sin(a) * r1, (k) => (k < 0.4 ? WHITE : k < 0.75 ? t.pale : t.light), (k) => grow * (1 - k * 0.5));
        }
        p.glow(0, CY, 12 + grow * 8, t.light, 0.55 * grow);
      }
      const ring = seg(u, 0.4, 0.8);
      if (ring > 0 && ring < 1) K.floorRing(p, 0, CY, 6 + ease.outCubic(ring) * 12, 2 + ring * 5, t.pale, t.dark, 1 - ring);
      motes(p, time, t, 419 + group, 6, 12);
    };
  }

  function glyph(p, x, y, rows, color, alpha) {
    rows.forEach((row, dy) => [...row].forEach((ch, dx) => ch === "x" && p.dot(x + dx - 1, y + dy - 1, color, alpha)));
  }

  function enchant(group) {
    const t = tones(group);
    return (p, u, info) => {
      const time = u * info.seconds;
      exit(p, u);
      const gather = ease.inCubic(seg(u, 0.1, 0.56)), strike = bump(u, 0.52, 0.76), after = seg(u, 0.56, 1);
      relic(p, 0, CY + Math.round(bump(u, 0, 0.14) * -2) + Math.round(bump(u, 0.56, 0.72) * 1.5), 1, t, after > 0 && after < 0.6 ? after / 0.6 : -1);
      if (gather < 1) {
        const reach = 15 - gather * 12, turn = seg(u, 0, 0.56) * 2.6 + gather * 2.2;
        RUNES.forEach((rows, i) => {
          const a = (i / RUNES.length) * TAU + turn, x = Math.cos(a) * reach, y = CY + Math.sin(a) * reach * 0.62;
          glyph(p, x, y, rows, Math.sin(a) > 0 ? EDGE : RIFT, seg(u, i * 0.015, 0.08 + i * 0.015));
          p.dot(x - Math.cos(a - 0.5) * 2, y - Math.sin(a - 0.5) * 1.4, t.mid, 0.7);
        });
      }
      if (strike > 0) {
        for (let y = -HALF_H - 1; y <= HALF_H + 1; y++) {
          const half = Math.round(HALF_W * (1 - Math.abs(y) / (HALF_H + 1.5))) + 1;
          p.dot(-half - 1, CY + y, WHITE, strike);
          p.dot(half + 1, CY + y, WHITE, strike);
        }
        p.glow(0, CY, 10 + strike * 8, EDGE, 0.7 * strike);
      }
      if (after > 0) {
        const mark = Math.min(1, after * 3);
        glyph(p, 0, CY, RUNES[group % RUNES.length], after < 0.35 ? WHITE : EDGE, mark);
        burst(10, 431 + group, (g) => {
          const a = g.range(0, TAU);
          K.spark(p, Math.cos(a) * 5, CY + Math.sin(a) * 7, Math.cos(a) * g.range(8, 20), -g.range(10, 30), time - 0.56 * info.seconds - g.range(0, 0.06), g.range(0.22, 0.34), [WHITE, EDGE, RIFT, t.mid], 60, 1, 0.04);
        });
        K.floorRing(p, 0, CY + HALF_H + 2, 3 + ease.outCubic(after) * 9, 1 + after * 2, EDGE, RIFT, 1 - after);
      }
    };
  }

  const parts = {};
  GROUPS.forEach((_, g) => {
    parts[`artifactOpen${g}`] = open(g);
    parts[`artifactAwaken${g}`] = awaken(g);
    parts[`artifactEnchant${g}`] = enchant(g);
  });
  return parts;
}
