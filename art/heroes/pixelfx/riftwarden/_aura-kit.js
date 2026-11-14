import { fit, TAU, clamp, seg, ease, rng, cycle, burst, WHITE } from "../../core/index.js";
import { NEUTRAL } from "./_skill-kit.js";

const RX = 14;
const RY = 4;
const SIGIL_SECONDS = 2;
const MOTE_SECONDS = 1.2;
const SURGE_SECONDS = 0.8;
const OFF_SECONDS = 0.3;

const frac = (v) => ((v % 1) + 1) % 1;
const near = (phase, slot) => {
  const d = frac(phase - slot);
  return Math.min(d, 1 - d);
};
const wave = (phase, turns = 1, shift = 0) => Math.sin((phase * turns + shift) * TAU);
const dot = (p, x, y, color) => p.px(Math.round(x), Math.round(y), color);
const floor = (u, v) => [u * RX, v * RY];
const onRing = (turn, scale = 1) => floor(Math.cos(turn * TAU) * scale, Math.sin(turn * TAU) * scale);

function ring(p, scale, color, keep) {
  const steps = Math.ceil(110 * scale);
  for (let i = 0; i < steps; i++) {
    const turn = i / steps;
    if (keep && !keep(turn, i)) continue;
    const [x, y] = onRing(turn, scale);
    dot(p, x, y, color);
  }
}

function chase(C, phase, slot, count, base = C[2]) {
  const d = near(phase, slot / count) * count;
  return d < 0.5 ? C[5] : d < 1.5 ? C[4] : base;
}

const SIGILS = {
  common(p, ph, C) {
    ring(p, 1, C[3]);
    ring(p, 0.58, wave(ph) > 0 ? C[3] : C[2]);
    for (let i = 0; i < 4; i++) {
      const [x, y] = onRing(i / 4 + ph / 4, 0.8);
      dot(p, x, y, C[5]);
      dot(p, x + 1, y, C[4]);
    }
    dot(p, 0, 0, wave(ph, 2) > 0 ? C[4] : C[2]);
  },
  runedancer(p, ph, C) {
    ring(p, 1, C[1], (turn, i) => i % 2 === 0);
    const shapes = [[[0, 0], [0, -1], [1, 0]], [[0, 0], [-1, 0], [1, -1]], [[0, 0], [0, -1], [-1, -1]], [[0, 0], [1, 0], [-1, -1]], [[0, 0], [0, -1], [1, -1]], [[0, 0], [-1, 0], [0, -1]]];
    for (let i = 0; i < 6; i++) {
      const [x, y] = onRing(i / 6 + 1 / 12, 0.86), tone = chase(C, ph, i, 6);
      for (const [dx, dy] of shapes[i]) dot(p, x + dx, y + dy, tone);
      if (tone === C[5]) p.glow(x, y, 4, C[4], 0.25);
    }
  },
  duelist(p, ph, C) {
    for (let x = -11; x <= 11; x++) dot(p, x, -3, x % 4 === 0 ? C[3] : C[2]);
    for (let x = -13; x <= 13; x++) dot(p, x, 3, x % 4 === 0 ? C[3] : C[2]);
    for (const side of [-1, 1]) {
      p.line(side * 11, -3, side * 13, 3, C[2]);
      dot(p, side * 6, 0, C[1]);
    }
    const swing = 0.5 - 0.5 * Math.cos(ph * TAU), head = -10 + 20 * swing, dir = wave(ph) >= 0 ? 1 : -1;
    for (let i = 0; i < 6; i++) dot(p, head - dir * i, 0, i === 0 ? WHITE : i < 2 ? C[5] : i < 4 ? C[3] : C[1]);
    p.glow(head, 0, 4, C[4], 0.25);
  },
  windshot(p, ph, C) {
    ring(p, 1, C[1], (turn, i) => i % 3 === 0);
    for (let k = 0; k < 3; k++) {
      for (let i = 9; i >= 0; i--) {
        const turn = ph / 3 + k / 3 - i * 0.022, [x, y] = onRing(turn, 0.82 - i * 0.03);
        dot(p, x, y, i === 0 ? C[5] : i < 3 ? C[4] : i < 6 ? C[3] : C[2]);
        if (i === 0) dot(p, x, y - 1, C[4]);
      }
    }
  },
  shardcaller(p, ph, C) {
    ring(p, 0.9, C[1], (turn, i) => i % 3 === 0);
    for (let i = 0; i < 8; i++) {
      const turn = i / 8 + 1 / 16, [x, y] = onRing(turn, 0.9), front = Math.sin(turn * TAU) > 0, tone = chase(C, ph, i, 8, C[3]);
      dot(p, x, y, C[2]);
      dot(p, x, y - 1, tone);
      dot(p, x, y - 2, tone === C[3] ? C[4] : WHITE);
      if (front) {
        dot(p, x - 1, y - 1, C[2]);
        dot(p, x + 1, y - 1, tone === C[3] ? C[3] : C[4]);
      }
      if (tone === C[5]) p.glow(x, y - 1, 4, C[4], 0.25);
    }
  },
  alchemist(p, ph, C) {
    ring(p, 1, C[2]);
    const tips = [0.75, 0.75 + 1 / 3, 0.75 + 2 / 3].map((turn) => onRing(turn, 0.95));
    for (let i = 0; i < 3; i++) p.line(Math.round(tips[i][0]), Math.round(tips[i][1]), Math.round(tips[(i + 1) % 3][0]), Math.round(tips[(i + 1) % 3][1]), C[3]);
    for (const [x, y] of tips) dot(p, x, y, C[4]);
    cycle(ph * SIGIL_SECONDS, SIGIL_SECONDS, 4, 3, 0.9, (age, life, r) => {
      const k = age / life, x = r.range(-6, 6) + Math.sin(k * 5) * 0.8, y = 2 - k * 6;
      dot(p, x, y, k > 0.85 ? WHITE : k > 0.4 ? C[5] : C[4]);
      if (k > 0.85) { dot(p, x - 1, y, C[3]); dot(p, x + 1, y, C[3]); }
    });
  },
  minstrel(p, ph, C) {
    ring(p, 1, C[1], (turn, i) => turn >= 0.5 && i % 2 === 0);
    for (const scale of [1, 0.55]) ring(p, scale, scale === 1 ? C[3] : C[2], (turn) => turn < 0.5);
    for (let i = 0; i < 4; i++) {
      const beat = near(ph, i / 4) * 4, hop = beat < 0.5 ? Math.round(2 * Math.cos(beat * Math.PI)) : 0;
      const [x, y] = onRing(0.07 + i * 0.12, [0.55, 1, 0.55, 1][i]);
      const tone = hop > 0 ? C[5] : C[4];
      dot(p, x, y - hop, tone);
      dot(p, x + 1, y - hop, tone);
      dot(p, x + 1, y - hop - 1, C[3]);
      dot(p, x + 1, y - hop - 2, C[3]);
    }
  },
  lanternbearer(p, ph, C) {
    const breath = 0.5 + 0.5 * wave(ph);
    p.ellipse(0, 0, 10, 2.9, C[1]);
    p.ellipse(0, 0, 6 + breath, 1.9, C[2]);
    p.ellipse(0, 0, 2.5 + breath, 0.9, C[3]);
    p.glow(0, 0, 9, C[3], 0.2 + 0.1 * breath);
    for (let i = 0; i < 2; i++) {
      const turn = ph + i / 2, [x, y] = onRing(turn, 0.95), blink = wave(ph, 3, i * 0.3) > -0.3;
      dot(p, x, Math.max(-5, y - 1 + Math.round(wave(ph, 2, i * 0.5))), blink ? WHITE : C[3]);
      const [tx, ty] = onRing(turn - 0.03, 0.95);
      dot(p, tx, ty - 1, C[2]);
      if (blink) p.glow(x, y - 1, 3, C[5], 0.3);
    }
  },
  whisperer(p, ph, C) {
    ring(p, 1, C[2]);
    for (let i = 0; i < 3; i++) {
      const [x, y] = onRing(i / 3 + ph / 3, 1);
      for (const [dx, dy] of [[0, 0], [1, 0], [0, -1], [1, -1]]) dot(p, x + dx, y + dy, dx + dy === 0 ? C[5] : C[4]);
    }
    cycle(ph * SIGIL_SECONDS, SIGIL_SECONDS, 3, 5, 1.1, (age, life, r) => {
      const k = age / life, x0 = r.range(-7, 7), tone = k < 0.2 || k > 0.8 ? C[2] : C[4];
      for (let i = 0; i < 4; i++) dot(p, x0 + Math.sin((k * 2 + i * 0.25) * TAU) * 1.5, 2 - k * 4 - i * 0.6, i === 0 ? tone : C[3]);
    });
  },
  cartomancer(p, ph, C) {
    ring(p, 1, C[1], (turn, i) => i % 2 === 0);
    for (let i = 0; i < 4; i++) {
      const x = -10 + i * 6.6, y = [0, 1, 1, 0][i], beat = near(ph, i / 4 + 0.125) * 4;
      const turnK = beat < 0.5 ? Math.abs(Math.cos(beat * Math.PI * 2)) : 1, shown = beat < 0.25;
      const half = Math.max(0, Math.round(2 * turnK));
      for (let dx = -half; dx <= half; dx++) {
        for (let dy = -1; dy <= 1; dy++) {
          const edge = Math.abs(dx) === half || dy !== 0;
          dot(p, x + dx, y + dy, shown ? (edge ? C[4] : WHITE) : edge ? C[3] : dx === 0 ? C[4] : C[2]);
        }
      }
    }
  },
  herbwitch(p, ph, C) {
    for (let i = 0; i < 20; i++) {
      const turn = i / 20, [x, y] = onRing(turn, 0.96), sway = Math.round(wave(ph, 1, turn * 2) * 0.6);
      dot(p, x, y, C[2]);
      dot(p, x + sway, y - 1, i % 2 ? C[3] : C[2]);
    }
    for (let i = 0; i < 4; i++) {
      const [x, y] = onRing(i / 4 + 0.07, 0.6), bloom = wave(ph, 1, i / 4) > 0.1;
      dot(p, x, y, bloom ? WHITE : C[4]);
      if (bloom) for (const [dx, dy] of [[1, 0], [-1, 0], [0, -1], [0, 1]]) dot(p, x + dx, y + dy, C[4]);
    }
  },
  mothcaller(p, ph, C) {
    ring(p, 1, C[3]);
    for (let i = 0; i < 60; i++) {
      const turn = 0.2 + (i / 60) * 0.6, [x, y] = floor(Math.cos(turn * TAU) * 0.78 + 0.18, Math.sin(turn * TAU) * 0.78);
      dot(p, x, y, C[2]);
    }
    p.ellipse(-5, 0, 3.4, 1.4, wave(ph) > 0 ? C[3] : C[2]);
    cycle(ph * SIGIL_SECONDS, SIGIL_SECONDS, 6, 7, 1.2, (age, life, r) => {
      const k = age / life, x = r.range(-10, 10) + Math.sin(k * 6 + r.range(0, 6)) * 1.2, y = -5 + k * r.range(4, 7);
      dot(p, x, y, k < 0.5 ? C[5] : k < 0.8 ? C[4] : C[2]);
    });
  },
  soulbinder(p, ph, C) {
    for (let i = 0; i < 4; i++) {
      const x = (frac(ph + i / 4) - 0.5) * 20, edge = 1 - Math.abs(frac(ph + i / 4) - 0.5) * 2;
      p.disc(x, [1, -1, 1, 0][i], 2.2 + (i % 2) * 0.6, i % 2 ? C[2] : C[1], 0.55 * Math.min(1, edge * 2.5));
    }
    ring(p, 1, C[2], (turn, i) => i % 4 < 2);
    for (let i = 0; i < 2; i++) {
      const a = (ph + i / 2) * TAU, x = Math.sin(a) * 9, y = Math.sin(a * 2) * 2 - 1;
      dot(p, x, y, WHITE);
      dot(p, x, y - 1, C[4]);
      dot(p, x - Math.sign(Math.cos(a)) || 0, y, C[3]);
      p.glow(x, y, 4, C[5], 0.3);
    }
  },
  ghostcaptain(p, ph, C) {
    ring(p, 1, C[1], (turn, i) => i % 3 !== 0);
    for (let i = 1; i <= 12; i++) {
      dot(p, i, 0, i > 9 ? C[2] : C[3]);
      dot(p, -i, 0, i > 9 ? C[2] : C[3]);
    }
    for (let i = 1; i <= 3; i++) {
      dot(p, 0, i, C[3]);
      dot(p, 0, -i, C[3]);
    }
    for (const [sx, sy] of [[1, 1], [-1, 1], [1, -1], [-1, -1]]) {
      dot(p, sx * 3, sy, C[2]);
      dot(p, sx * 6, sy * 2, C[1]);
    }
    const turn = 0.92 + 0.07 * wave(ph) + 0.02 * wave(ph, 3), [x, y] = onRing(turn, 0.62);
    p.line(0, 0, Math.round(x), Math.round(y), C[5]);
    p.line(0, 0, Math.round(-x * 0.4), Math.round(-y * 0.4), C[2]);
    dot(p, x, y, WHITE);
    dot(p, 0, 0, C[4]);
  },
  golemancer(p, ph, C) {
    ring(p, 1, C[1], (turn, i) => i % 3 === 0);
    const slabs = [[-10, -1, 7], [-3, 1, 7], [4, -1, 7], [-6, -3, 5], [2, -3, 5]];
    slabs.forEach(([x, y, w], i) => {
      for (let dx = 0; dx < w; dx++) {
        dot(p, x + dx, y, C[2]);
        dot(p, x + dx, y + 1, C[1]);
        if (i < 3) dot(p, x + dx, y + 2, C[0]);
      }
      const heat = 0.5 + 0.5 * wave(ph, 1, i / 5), mid = x + (w >> 1);
      dot(p, mid, y, heat > 0.8 ? WHITE : heat > 0.5 ? C[5] : heat > 0.25 ? C[4] : C[3]);
      if (i < 3) dot(p, mid, y + 1, heat > 0.5 ? C[4] : C[2]);
      if (heat > 0.8) p.glow(mid, y, 4, C[4], 0.28);
    });
  },
  clockwright(p, ph, C) {
    ring(p, 1, C[2]);
    for (let i = 0; i < 12; i++) {
      const [x, y] = onRing(i / 12, 0.84);
      dot(p, x, y, i % 3 === 0 ? C[4] : C[2]);
    }
    const [hx, hy] = onRing(0.62, 0.42);
    p.line(0, 0, Math.round(hx), Math.round(hy), C[3]);
    const [x, y] = onRing(ph - 0.25, 0.9);
    p.line(0, 0, Math.round(x), Math.round(y), C[5]);
    dot(p, x, y, WHITE);
    dot(p, 0, 0, WHITE);
  },
  sandwalker(p, ph, C) {
    for (let row = -3; row <= 3; row += 2) {
      const half = Math.round(RX * Math.sqrt(1 - (row / 4.2) ** 2));
      for (let x = -half; x <= half; x++) {
        const crest = Math.sin(x * 0.55 + row * 1.3 - ph * TAU);
        if (crest > -0.2) dot(p, x, row + (crest > 0.75 ? -1 : 0), crest > 0.75 ? C[4] : crest > 0.3 ? C[3] : C[2]);
      }
    }
    for (let i = 0; i < 5; i++) {
      const x = -8 + i * 4 + Math.round(wave(ph, 2, i * 0.21) * 1.2);
      if (wave(ph, 1, i * 0.37) > -0.2) {
        dot(p, x, -5, C[4]);
        dot(p, x + 1, -5, C[3]);
      }
    }
  },
  dowser(p, ph, C) {
    for (let i = 0; i < 6; i++) {
      const g = rng(41, i), turn = i / 6 + g.range(-0.04, 0.04);
      let px = 0, py = 0;
      for (let s = 1; s <= 5; s++) {
        const [x, y] = onRing(turn + g.range(-0.03, 0.03), s / 5);
        p.line(Math.round(px), Math.round(py), Math.round(x), Math.round(y), C[2]);
        px = x; py = y;
      }
      const run = frac(ph * 2 + i / 6), [sx, sy] = onRing(turn, run);
      dot(p, sx, sy, run > 0.8 ? C[4] : WHITE);
      dot(p, px, py, C[4]);
    }
    const twitch = near(ph, 0.3) < 0.04 || near(ph, 0.8) < 0.04 ? 1 : 0;
    dot(p, twitch, 0, C[5]);
    dot(p, twitch, -1, C[5]);
    dot(p, -1 + twitch, -2, C[4]);
    dot(p, 1 + twitch, -2, C[4]);
    dot(p, -2 + twitch, -3, C[4]);
    dot(p, 2 + twitch, -3, C[4]);
  },
  dreamcatcher(p, ph, C) {
    ring(p, 1, C[3]);
    ring(p, 0.6, C[2]);
    ring(p, 0.26, wave(ph) > 0 ? C[4] : C[3]);
    for (let i = 0; i < 8; i++) {
      const [x0, y0] = onRing(i / 8 + ph / 8, 0.26), [x1, y1] = onRing(i / 8 + ph / 8 + 0.04, 1);
      p.line(Math.round(x0), Math.round(y0), Math.round(x1), Math.round(y1), C[1]);
      const [kx, ky] = onRing(i / 8 + ph / 8 + 0.02, 0.6);
      dot(p, kx, ky, C[4]);
    }
    for (const side of [-1, 1]) {
      const swing = Math.round(wave(ph, 1, side * 0.2) * 1.4);
      dot(p, side * 12, 2, C[3]);
      dot(p, side * 12 + (swing > 0 ? 1 : swing < 0 ? -1 : 0), 3, C[5]);
      dot(p, side * 12 + swing, 4, C[4]);
    }
  },
};

const sprite = (p, rows, tones) => rows.forEach((row, y) => [...row].forEach((ch, x) => {
  if (tones[ch]) p.px(x - 2, y - 2, tones[ch]);
}));

const MOTES = {
  might(p, ph, C) {
    const lean = Math.round(wave(ph, 2));
    const tones = { a: C[3], b: C[4], c: WHITE };
    sprite(p, lean > 0 ? ["...a.", "..ab.", ".bcb.", "abcba", ".bbb."] : lean < 0 ? [".a...", ".ba..", ".bcb.", "abcba", ".bbb."] : ["..a..", ".aba.", ".bcb.", "abcba", ".bbb."], tones);
    const rise = frac(ph * 2);
    if (rise < 0.6) p.px(lean, -3 - Math.floor(rise * 2), rise < 0.3 ? C[5] : C[3]);
  },
  edge(p, ph, C) {
    const sweep = Math.floor(frac(ph) * 12) - 3;
    for (let y = -2; y <= 2; y++)
      for (let x = -2; x <= 2; x++) {
        const d = Math.abs(x) + Math.abs(y);
        if (d > 2) continue;
        p.px(x, y, x + y === sweep || x + y === sweep - 1 ? WHITE : d === 2 ? C[3] : x < 0 || y < 0 ? C[5] : C[4]);
      }
    if (sweep === 3) { p.px(3, -3, WHITE); p.px(2, -3, C[4]); p.px(3, -2, C[4]); }
  },
  hunt(p, ph, C) {
    const order = { a: 0, b: 1, c: 2, d: 3, e: 4, f: 3, g: 2, h: 1, i: 0 };
    const head = Math.floor(frac(ph) * 9) - 1;
    ["a...i", "b...h", "bc.gh", ".def.", "..e.."].forEach((row, y) => [...row].forEach((ch, x) => {
      if (ch === ".") return;
      const along = "abcdefghi".indexOf(ch) * 0.6;
      p.px(x - 2, y - 2, Math.abs(along - head * 0.6) < 0.7 ? WHITE : order[ch] === 0 ? C[5] : order[ch] < 3 ? C[4] : C[3]);
    }));
  },
  flow(p, ph, C) {
    sprite(p, ["..a..", ".aba.", "abcba", "abbba", ".aaa."], { a: C[3], b: C[4], c: WHITE });
    const turn = ph * TAU - Math.PI / 2, x = Math.round(Math.cos(turn) * 3.6), y = Math.round(Math.sin(turn) * 3.6);
    p.px(x, y, WHITE);
    const back = turn - 0.5;
    p.px(Math.round(Math.cos(back) * 3.6), Math.round(Math.sin(back) * 3.6), C[3]);
  },
  fortune(p, ph, C) {
    const face = Math.cos(ph * TAU), half = Math.round(Math.abs(face) * 2);
    for (let y = -2; y <= 2; y++) {
      const reach = Math.abs(y) === 2 ? Math.max(0, half - 1) : half;
      for (let x = -reach; x <= reach; x++) {
        const rim = Math.abs(x) === reach || Math.abs(y) === 2;
        p.px(x, y, half === 0 ? C[5] : rim ? C[3] : face > 0 ? (x === 0 ? WHITE : C[5]) : C[4]);
      }
    }
    if (half === 2 && face > 0.9) { p.px(-3, -3, WHITE); p.px(3, 3, C[4]); }
  },
  hex(p, ph, C) {
    const blink = near(ph, 0.5) < 0.04;
    if (blink) {
      for (let x = -2; x <= 2; x++) p.px(x, 0, C[4]);
      return;
    }
    sprite(p, [".aaa.", "a...a", "a...a", "a...a", ".aaa."], { a: C[4] });
    const turn = Math.floor(frac(ph * 2) * 8), spots = [[0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1]];
    p.px(0, 0, WHITE);
    p.px(spots[turn][0], spots[turn][1], C[5]);
    p.px(spots[(turn + 7) % 8][0], spots[(turn + 7) % 8][1], C[3]);
  },
  kin(p, ph, C) {
    const ends = [[-2, -2], [2, -2], [0, 2]];
    p.px(-1, -1, C[3]); p.px(1, -1, C[3]); p.px(0, 0, C[4]); p.px(0, 1, C[3]);
    ends.forEach(([x, y], i) => {
      const lit = near(ph, i / 3) * 3 < 0.5;
      p.px(x, y, lit ? WHITE : C[5]);
      if (lit) {
        p.px(x + (x < 0 ? -1 : x > 0 ? 1 : 0), y, C[4]);
        p.px(x, y + (y < 0 ? -1 : 1), C[4]);
      }
    });
  },
  calm(p, ph, C) {
    sprite(p, [".aab.", "ab...", "ab...", "ab...", ".aab."], { a: C[5], b: C[3] });
    const rise = frac(ph), y = 0 - Math.floor(rise * 4);
    if (rise < 0.8) {
      p.px(2, y, rise < 0.4 ? WHITE : C[4]);
      if (rise > 0.2 && rise < 0.6) p.px(3, y - 1, C[3]);
    }
  },
};

function looped(name, desc, tags, size, origin, seconds, paint) {
  return {
    meta: { name, desc, tags: ["skill", "aura", "loop", ...tags], size, origin, duration: seconds, loop: true, fps: 30, bg: "dark", options: { palette: NEUTRAL, seed: 1 } },
    draw(p, t, o) { paint(p, frac(t / seconds), fit(o.palette, 6)); },
  };
}

export function sigil(id) {
  return looped(`Aura sigil: ${id}`, `A flat class sign on the ground under the hero while an aura is on, neutral for tinting: ${id}.`, ["sigil", "ground"], [32, 10], [16, 5], SIGIL_SECONDS, (p, ph, C) => SIGILS[id](p, ph, [C[1], C[2], C[3], C[4], C[5], WHITE]));
}

export function mote(id) {
  return looped(`Aura mote: ${id}`, `A five pixel orbiting badge for one running aura, neutral for tinting: theme ${id}.`, ["mote", "icon"], [9, 9], [4, 4], MOTE_SECONDS, MOTES[id]);
}

export function surge() {
  return looped("Aura surge", "Glowing streams rising behind the hero while a timed aura runs, neutral for tinting.", ["surge", "timed"], [24, 28], [12, 27], SURGE_SECONDS, (p, ph, C) => {
    cycle(ph * SURGE_SECONDS, SURGE_SECONDS, 7, 17, (r) => r.range(0.5, 0.75), (age, life, r, i) => {
      const k = age / life, x0 = -9 + i * 3 + r.range(-1, 1), top = -1 - ease.outQuad(k) * r.range(17, 25), length = 4 + Math.round(5 * Math.sin(k * Math.PI));
      for (let j = 0; j < length; j++) {
        const y = top + j, x = x0 + Math.sin((y * 0.32) + i * 1.7) * 1.3;
        if (y > 0) break;
        p.px(Math.round(x), Math.round(y), j === 0 ? (k < 0.7 ? WHITE : C[4]) : j < 3 ? C[4] : j < 6 ? C[3] : C[2]);
        if (j > 0 && j < 3 && k < 0.7) p.px(Math.round(x) + 1, Math.round(y), C[3]);
      }
      if (k < 0.15) p.px(Math.round(x0), 0, C[5]);
    });
    for (let x = -10; x <= 9; x++) if ((x + Math.floor(ph * 8)) % 4 === 0) p.px(x, 0, C[2]);
    p.glow(0, -3, 10, C[3], 0.22);
  });
}

export function off() {
  return {
    meta: { name: "Aura off", desc: "An aura mote going out, neutral for tinting: a blink, a ring of specks flying apart and a wisp rising.", tags: ["skill", "aura", "one-shot"], size: [15, 15], origin: [7, 7], duration: OFF_SECONDS, loop: false, fps: 30, bg: "dark", options: { palette: NEUTRAL, seed: 1 } },
    draw(p, t, o) {
      const C = fit(o.palette, 6), u = clamp(t / OFF_SECONDS);
      if (u >= 1) return;
      if (u < 0.3) {
        const arm = u < 0.15 ? 2 : 1;
        p.px(0, 0, WHITE);
        for (let d = 1; d <= arm; d++) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) p.px(dx * d, dy * d, d === arm ? C[4] : C[5]);
        p.glow(0, 0, 5, C[4], 0.3 * (1 - u / 0.3));
      }
      const spread = ease.outCubic(seg(u, 0.1, 0.9));
      burst(8, o.seed + 60, (r, i) => {
        const a = (i / 8) * TAU + r.range(-0.2, 0.2), d = 1.5 + spread * r.range(3.5, 5.2);
        if (u > 0.1 && u < r.range(0.7, 0.98)) p.px(Math.round(Math.cos(a) * d), Math.round(Math.sin(a) * d), u < 0.4 ? C[5] : u < 0.65 ? C[4] : C[2]);
      });
      const wisp = seg(u, 0.3, 1);
      if (wisp > 0 && wisp < 0.95) {
        const y = -1 - Math.round(wisp * 4);
        p.px(Math.round(Math.sin(wisp * 6)), y, wisp < 0.5 ? C[3] : C[2]);
        if (wisp < 0.6) p.px(Math.round(Math.sin(wisp * 6 - 1)), y + 1, C[2]);
      }
    },
  };
}

export const SIGIL_IDS = Object.keys(SIGILS);
export const MOTE_IDS = Object.keys(MOTES);
