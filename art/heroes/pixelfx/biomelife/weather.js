export default function weather(fx) {
  const { TAU, rand, wrap, clamp, cycle, WHITE } = fx;
  const S = fx.scene;
  const { W, GROUND, MEADOW, HILLS } = S;
  const FAIR = S.ramp(["#ffffff", "#e3ecf2", "#bcd2e2"]);
  const PUFF = S.ramp(["#eef3f4", "#dce4e6", "#c2dbe4"]);
  const GREY = S.ramp(["#c9d3dc", "#a3b1bf", "#7d8b9c"]);
  const STORM = S.ramp(["#8794a6", "#66748a", "#4a566c"]);
  const RAIN = S.ramp(["#eaf4ff", "#b9d6f2", "#86aede"]);
  const LEAF = S.ramp(["#b5d95a", "#7fb648", "#f2f7c8", "#f6b8d0"]);
  const FLUFF = S.ramp(["#ffffff", "#e8f0d8"]);
  const BOW = S.ramp(["#ff6b6b", "#ffa94d", "#ffe066", "#8ce99a", "#74c0fc", "#b197fc"]);

  function clearFar(p) {
    const t = S.tile(p);
    S.cloud(t, 22, 10, 16, 4, 11, PUFF);
    S.cloud(t, 74, 6, 10, 3, 12, PUFF);
    S.cloud(t, 121, 12, 20, 5, 13, PUFF);
  }

  function clearNear(p, u, info) {
    cycle(u * info.seconds, info.seconds, 9, 21, (r) => r.range(1.6, 2.6), (age, life, r, i) => {
      const k = age / life, x = r.range(4, W - 4) + Math.sin(k * TAU + i) * 2.5, y = r.range(26, 49) - k * 5;
      if (S.inZone(x, y)) return;
      const a = Math.sin(k * Math.PI);
      p.dot(x, y, i % 3 ? FLUFF[1] : WHITE, a * 0.9);
      if (i % 4 === 0 && a > 0.85) p.glow(x, y, 2.5, FLUFF[1], 0.25);
    });
  }

  function clearMist(p, u, info) {
    cycle(u * info.seconds, info.seconds, 4, 23, () => info.seconds, (age, life, r, i) => {
      const k = age / life, x = wrap(r.range(0, W) + k * W, W), y = 44 - r.range(0, 5) + Math.sin(k * TAU * 2 + i) * 1.6;
      if (S.inZone(x, y)) return;
      p.px(x, y, FLUFF[0]);
      p.dot(x - 1, y, FLUFF[1], 0.7);
      p.dot(x, y - 1, FLUFF[1], 0.7);
      p.dot(x, y + 1, FLUFF[1], 0.5);
      p.dot(x, y + 2, LEAF[1], 0.45);
    });
  }

  function cloudyFar(p) {
    const t = S.tile(p);
    S.veil(p, 0, HILLS + 4, GREY[1], (v) => 0.36 * (1 - v * 0.7));
    const back = [[14, 9, 30, 8], [52, 12, 34, 10], [93, 8, 28, 7], [128, 13, 36, 10], [158, 9, 22, 7]];
    back.forEach(([x, y, w, h], i) => S.cloud(t, x, y, w, h, 31 + i, GREY));
    const front = [[30, 18, 24, 6], [74, 20, 28, 7], [112, 19, 20, 5], [148, 21, 24, 6]];
    front.forEach(([x, y, w, h], i) => S.cloud(t, x, y, w, h, 41 + i, FAIR));
  }

  function cloudyNear(p) {
    const t = S.tile(p);
    const wisps = [[20, 21, 20, 4], [80, 19, 26, 5], [133, 22, 18, 4]];
    wisps.forEach(([x, y, w, h], i) => S.cloud(t, x, y, w, h, 51 + i, GREY, 0.6, true));
  }

  function cloudyMist(p, u) {
    S.haze(p, u, 30, 42, FAIR[1], 0.3, 61);
  }

  function rainFar(p, u) {
    const t = S.tile(p);
    S.veil(p, 0, GROUND, STORM[1], (v) => 0.38 - v * 0.2);
    const row = [[12, 9, 32, 9], [50, 13, 36, 11], [92, 9, 30, 8], [130, 14, 38, 11], [159, 10, 20, 8]];
    row.forEach(([x, y, w, h], i) => S.cloud(t, x, y, w, h, 71 + i, STORM));
    const low = [[30, 19, 26, 6], [74, 21, 24, 6], [112, 20, 26, 6], [150, 21, 22, 5]];
    low.forEach(([x, y, w, h], i) => S.cloud(t, x, y, w, h, 81 + i, GREY));
    for (let i = 0; i < 46; i++) {
      const x0 = rand(i, 1, 91) * W, fall = wrap(u * 2 + rand(i, 2, 91), 1), y = 14 + fall * 26;
      if (y > MEADOW + 2) continue;
      t.blend(x0 - y * 0.25, y, RAIN[2], 0.5);
      t.blend(x0 - (y + 1) * 0.25, y + 1, RAIN[2], 0.32);
    }
  }

  function rainNear(p, u) {
    const t = S.tile(p);
    for (let i = 0; i < 44; i++) {
      const x0 = (i / 44) * W + rand(i, 1, 101) * 4, k = wrap(u + rand(i, 2, 101), 1), land = GROUND - 1 + Math.floor(rand(i, 3, 101) * 4);
      const hit = x0 - land * 0.35;
      if (k < 0.72) {
        const y = -6 + (k / 0.72) * (land + 6);
        for (let s = 0; s < 5; s++) {
          const yy = y - s, xx = x0 - yy * 0.35;
          if (yy < 0 || yy > land || S.inZone(xx, yy)) continue;
          t.dot(xx, yy, s < 2 ? RAIN[0] : s < 4 ? RAIN[1] : RAIN[2], s < 4 ? 1 : 0.6);
        }
      } else if (!S.inZone(hit, land)) {
        const s = (k - 0.72) / 0.28, rise = s < 0.5 ? 1 : 2;
        t.dot(hit - rise, land - rise + 1, RAIN[1], 1 - s);
        t.dot(hit + rise, land - rise + 1, RAIN[1], 1 - s);
        if (s < 0.4) t.px(hit, land, RAIN[0]);
      }
    }
  }

  function rainMist(p, u) {
    const t = S.tile(p);
    S.haze(p, u, 41, 55, RAIN[1], 0.3, 111);
    for (let i = 0; i < 7; i++) {
      const x = 9 + i * 16 + Math.floor(rand(i, 1, 121) * 6), y = 54 + (i % 3), wide = 3 + (i % 3);
      if (S.inZone(x, y - 6)) continue;
      const ring = wrap(u * 2 + rand(i, 2, 121), 1);
      for (let k = -wide; k <= wide; k++) t.blend(x + k, y, RAIN[2], 0.4);
      const reach = Math.round(ring * wide);
      t.dot(x - reach, y, RAIN[0], 1 - ring);
      t.dot(x + reach, y, RAIN[0], 1 - ring);
    }
  }

  function windFar(p) {
    const t = S.tile(p);
    for (let i = 0; i < 6; i++) {
      const x = i * 27 + rand(i, 1, 131) * 8, y = 4 + Math.floor(rand(i, 2, 131) * 14), len = 16 + Math.floor(rand(i, 3, 131) * 12);
      for (let k = 0; k < len; k++) {
        const v = k / len, lift = Math.round(Math.sin(v * 2.6) * 1.5);
        t.dot(x + k, y - lift, FAIR[0], v < 0.3 ? 0.3 + v * 2 : 1);
        if (v > 0.5) t.px(x + k, y - lift + 1, FAIR[1]);
        if (v > 0.75) t.px(x + k, y - lift - 1, FAIR[0]);
      }
      S.cloud(t, x + len + 3, y + 1, 7, 3, 141 + i, FAIR);
    }
  }

  function gust(t, head, y, len, curl, alpha) {
    if (alpha < 0.15) return;
    for (let k = 0; k < len; k++) {
      const v = k / len, yy = y + Math.sin(v * 3.2 + curl) * 2.2;
      if (!S.inZone(head - k, yy)) t.dot(head - k, yy, k < 3 ? WHITE : FAIR[1], v < 0.45 ? alpha * 2 : alpha * 2 * (1 - v) * 1.4);
    }
    const hook = y + Math.sin(curl) * 2.2;
    [[1, 0], [2, -1], [3, -2], [3, -3], [2, -4], [1, -4], [0, -3]].forEach(([dx, dy], n) => S.inZone(head + dx, hook + dy) || t.dot(head + dx, hook + dy, WHITE, alpha * 2 * (1 - n / 9)));
  }

  function windNear(p, u) {
    const t = S.tile(p);
    for (let i = 0; i < 4; i++) {
      const k = wrap(u + i * 0.27, 1), head = -30 + k * (W + 60), y = 14 + ((i * 11) % 30);
      gust(t, head, y, 20 + i * 3, i * 1.7 + k * 3, Math.sin(k * Math.PI));
    }
    for (let i = 0; i < 9; i++) {
      const k = wrap(u * (1 + (i % 2)) + rand(i, 1, 151), 1), x = k * W + rand(i, 2, 151) * W;
      const y = 18 + rand(i, 3, 151) * 30 + Math.sin((k * 3 + rand(i, 4, 151)) * TAU) * 3.5;
      if (S.inZone(x, y)) continue;
      const color = LEAF[i % 4], flip = S.beat(u, 12, i) % 4;
      t.px(x, y, color);
      if (flip < 2) t.px(x + 1, y + (flip ? 1 : -1), color);
      else if (flip === 2) t.px(x + 1, y, color);
    }
  }

  function windMist(p, u) {
    const t = S.tile(p);
    for (let i = 0; i < 12; i++) {
      const k = wrap(u * 2 + rand(i, 1, 161), 1), x = k * W + rand(i, 2, 161) * W, y = MEADOW + 2 + Math.floor(rand(i, 3, 161) * 12);
      if (S.inZone(x, y)) continue;
      const len = 4 + (i % 4), a = Math.sin(k * Math.PI);
      for (let s = 0; s < len; s++) t.blend(x - s, y, s < 2 ? LEAF[2] : LEAF[0], a * (0.75 - (s / len) * 0.5));
    }
    S.haze(p, u, 44, 54, S.ramp(["#e9e2c4"])[0], 0.2, 171, 2, 0.1);
  }

  function rainbow(p, u) {
    const cx = 62, cy = 47, outer = 37;
    for (let y = 2; y < 31; y++) {
      for (let x = cx - outer - 1; x <= cx + outer + 1; x++) {
        const d = Math.hypot(x + 0.5 - cx, (y + 0.5 - cy) * 1.12), band = Math.floor(outer - d);
        if (band < 0 || band >= BOW.length) continue;
        const fade = clamp((30 - y) / 9) * (0.42 + 0.1 * Math.sin((u * 2 + x * 0.02) * TAU));
        p.blend(x, y, BOW[band], fade);
      }
    }
    for (let i = 0; i < 6; i++) {
      const a = Math.PI * (1.12 + 0.76 * rand(i, 1, 181)), r = outer - 0.5 - Math.floor(rand(i, 2, 181) * 6);
      const on = S.beat(u, 8, i * 3);
      if (on < 2) p.dot(cx + Math.cos(a) * r, cy + (Math.sin(a) * r) / 1.12, WHITE, on ? 0.6 : 1);
    }
  }

  return { clearFar, clearNear, clearMist, cloudyFar, cloudyNear, cloudyMist, rainFar, rainNear, rainMist, windFar, windNear, windMist, rainbow };
}
