export default function critters(fx) {
  const { TAU, hex, rand, clamp, ease } = fx;
  const S = fx.scene;
  const K = fx.kit;
  const pal = (map) => Object.fromEntries(Object.entries(map).map(([k, v]) => [k, hex(v)]));
  const GRASS = S.ramp(["#3f8f4a", "#5aa85a", "#8ccf5a"]);
  const HILL_DUST = hex("#e9e2c4");

  function blit(p, rows, colors, x, y, o = {}) {
    const wide = rows[0].length;
    for (let r = 0; r < rows.length; r++) {
      const yy = y + r;
      if (o.clip !== undefined && yy >= o.clip) break;
      for (let q = 0; q < wide; q++) {
        const ch = rows[r][q];
        if (ch === ".") continue;
        const color = colors[(o.swap && o.swap[ch]) || ch];
        if (color) p.dot(x + (o.flip ? wide - 1 - q : q), yy, color, o.a ?? 1);
      }
    }
  }

  function tuft(p, cx, wide, seed, lean = 0, a = 1) {
    for (let i = 0; i < wide; i++) {
      const x = cx - wide / 2 + i, edge = 1 - Math.abs(i / (wide - 1) - 0.5) * 1.3;
      const tall = Math.max(1, Math.round((1.5 + rand(seed, i) * 3) * edge));
      for (let k = 0; k < tall; k++) {
        const bend = k >= tall - 1 ? Math.round(lean) : 0;
        p.dot(x + bend, -k, k === tall - 1 ? GRASS[2] : (i + k) % 3 ? GRASS[1] : GRASS[0], a);
      }
    }
  }

  const HILL_RABBIT = pal({ b: "#c8a37a", s: "#9a7a56", w: "#f4ead6", k: "#2a1c1a", e: "#b8926a", i: "#e8a59c", n: "#e07a80" });
  const SIT = [
    "...e.e....",
    "...i.e....",
    "..bbbb....",
    ".bkbbb....",
    ".nbbbbbb..",
    "..wbbbbbb.",
    "..wbbbsbbw",
    "..bb.sssb.",
  ];
  const SIT_FLICK = ["..e..e....", "...i.e....", ...SIT.slice(2)];
  const CROUCH = [
    "..........",
    "..........",
    "...e.e....",
    "..bbbb....",
    ".kbbbbbbb.",
    "nbbbbbbbbw",
    ".wbbsbbsb.",
    "..bb..ss..",
  ];
  const LEAP = [
    "............",
    "..ee........",
    ".bbbb.......",
    "kbbbbbbbbb..",
    "n.wbbbbbbbbw",
    "bb.....sbs..",
    "............",
    "............",
  ];
  const LAND = [
    "......bbbw..",
    "..e..bbbbbs.",
    ".ebbbbbb.ss.",
    "kbbbbb...s..",
    "nbbb........",
    ".bb.........",
    ".b..........",
    "............",
  ];
  const HOPS = [CROUCH, LEAP, LEAP, LAND, CROUCH, CROUCH];
  const HOP_LIFT = [0, 2, 3, 1, 0, 0];

  function rabbitKind(RABBIT, cover, DUST) {
    function rabbitFace(i) {
      const swap = {};
      if (i % 4 === 1) swap.n = "b";
      return swap;
    }

    function rabbitCalm(p, u, info) {
      const i = Math.round(u * info.frames);
      const sink = i < 5 ? 9 : i < 8 ? [6, 4, 2][i - 5] : i < 23 ? 1 : i < 26 ? [3, 6, 8][i - 23] : 9;
      if (sink < 9) {
        const swap = rabbitFace(i);
        if (i === 17 || i === 18) swap.k = "s";
        blit(p, i === 13 || i === 14 ? SIT_FLICK : SIT, RABBIT, -4, -7 + sink, { clip: 1, swap });
      }
      cover(p, i >= 5 && i < 8 ? 1 : i >= 23 && i < 26 ? -1 : 0, 1);
    }

    function rabbitFriendly(p, u, info) {
      const i = Math.round(u * info.frames), swap = rabbitFace(i);
      if (i === 10) swap.k = "s";
      if (i >= 12) return blit(p, CROUCH, RABBIT, -4, -7, { swap: i % 2 ? swap : { n: "b" } });
      blit(p, i === 5 || i === 6 ? SIT_FLICK : SIT, RABBIT, -4, -7, { swap });
    }

    function rabbitHop(p, u, info) {
      const i = Math.round(u * info.frames) % 6;
      blit(p, HOPS[i], RABBIT, HOPS[i][0].length === 12 ? -5 : -4, -7 - HOP_LIFT[i]);
    }

    function rabbitAlarmed(p, u, info) {
      const i = Math.round(u * (info.frames - 1));
      if (i >= info.frames - 1) return;
      if (i < 3) {
        blit(p, SIT, RABBIT, -4, -7 - (i === 1 ? 2 : i === 2 ? 1 : 0), { swap: { n: "b" } });
        if (i > 0) {
          p.px(-1, -11 - (i === 1 ? 2 : 1), RABBIT.w);
          p.px(3, -10 - (i === 1 ? 2 : 1), RABBIT.w);
        }
        return;
      }
      const step = i - 3, x = -4 - step * 3.1, pose = (step + 1) % 6, a = step > 6 ? 1 - (step - 6) / 4 : 1;
      blit(p, HOPS[pose], RABBIT, x - 1, -7 - HOP_LIFT[pose], { a });
      for (let k = 0; k < 3; k++) {
        const age = step - k * 2;
        if (age < 0 || age > 4) continue;
        K.puff(p, 4 - k * 9 + age * 0.6, -1 - age * 0.5, 1.2 + age * 0.4, DUST, 0.5 * (1 - age / 5));
      }
    }

    function rabbitHidden(p, u, info) {
      const i = Math.round(u * (info.frames - 1));
      if (i >= info.frames - 1) return;
      const sink = [0, 2, 5, 8, 9][i];
      if (sink < 9) blit(p, SIT, RABBIT, -4, -7 + sink, { clip: 1 });
      cover(p, i < 3 ? -1 : 0, i < 4 ? 1 : 0.5);
    }

    return { calm: rabbitCalm, friendly: rabbitFriendly, hop: rabbitHop, alarmed: rabbitAlarmed, hidden: rabbitHidden };
  }

  const hillRabbit = rabbitKind(HILL_RABBIT, (p, lean, a) => tuft(p, 0.5, 13, 311, lean, a), HILL_DUST);

  const WINGS = [
    S.ramp(["#ffe066", "#fff3b0", "#3a2a1a"]),
    S.ramp(["#ff9ecb", "#ffd0e6", "#3a2030"]),
    S.ramp(["#8fd3ff", "#d6f0ff", "#1e2a40"]),
  ];

  function flutter(p, x, y, state, tones, a = 1) {
    if (state === 0) {
      p.dot(x - 1, y - 1, tones[1], a);
      p.dot(x + 1, y - 1, tones[1], a);
      p.dot(x - 1, y, tones[0], a);
      p.dot(x + 1, y, tones[0], a);
    } else if (state === 1) {
      p.dot(x - 1, y - 1, tones[0], a);
      p.dot(x + 1, y - 1, tones[0], a);
    } else p.dot(x, y - 1, tones[0], a);
    p.dot(x, y, tones[2], a);
  }

  const FLAP = [0, 1, 2, 1];

  function butterflyAt(i, u) {
    const turn = i === 1 ? -1 : 1;
    const x = (i === 2 ? 6 : 10) * Math.sin((u * turn + i * 0.31) * TAU) + 1.5 * Math.sin((u * 3 + i * 0.2) * TAU);
    const y = (i === 2 ? 4 : 6) * Math.sin((u * 2 + i * 0.17) * TAU) + 1.2 * Math.sin((u * 5 + i * 0.4) * TAU) + (i - 1) * 2;
    return [x, y];
  }

  function butterfliesCalm(p, u, info) {
    const tick = Math.round(u * info.frames);
    for (let i = 0; i < 3; i++) {
      const [x, y] = butterflyAt(i, u);
      flutter(p, x, y, FLAP[(tick + i) % 4], WINGS[i]);
    }
  }

  const PERCH = [
    ["y...y", "yy.yy", ".yky.", "..k.."],
    [".....", ".y.y.", ".yky.", "..k.."],
    ["..y..", "..y..", "..k..", "..k.."],
  ];
  const PERCH_ORDER = [0, 0, 0, 0, 0, 1, 2, 2, 1, 0, 0, 1, 2, 1, 0, 0];

  function butterfliesFriendly(p, u, info) {
    const i = Math.round(u * info.frames) % 16;
    blit(p, PERCH[PERCH_ORDER[i]], { y: WINGS[0][0], k: WINGS[0][2] }, -2, -3);
    if (PERCH_ORDER[i] === 0) {
      p.px(-2, -3, WINGS[0][1]);
      p.px(2, -3, WINGS[0][1]);
    }
  }

  function butterfliesAlarmed(p, u, info) {
    const tick = Math.round(u * (info.frames - 1));
    if (tick >= info.frames - 1) return;
    for (let i = 0; i < 3; i++) {
      const [x0, y0] = butterflyAt(i, 0), k = ease.outQuad(u);
      const x = x0 + (i - 1) * 9 * k + Math.sin(u * 9 + i * 2) * 2.5, y = y0 - 17 * k;
      flutter(p, x, y, FLAP[(tick * 2 + i) % 4], WINGS[i], u > 0.6 ? 1 - (u - 0.6) / 0.4 : 1);
    }
  }

  function butterfliesHidden(p, u, info) {
    const tick = Math.round(u * (info.frames - 1));
    if (tick >= info.frames - 1) return;
    for (let i = 0; i < 3; i++) {
      const [x0, y0] = butterflyAt(i, 0), k = ease.inQuad(u);
      const y = y0 + (9 - y0) * k;
      flutter(p, x0 + Math.sin(u * 7 + i) * 1.5, y, u > 0.7 ? 2 : FLAP[(tick + i) % 4], WINGS[i], u > 0.6 ? 1 - (u - 0.6) / 0.4 : 1);
    }
  }

  const BIRD = S.ramp(["#262a36", "#434a5e"]);

  function wing(p, x, y, state, a = 1) {
    p.dot(x, y, BIRD[0], a);
    if (state === 0) p.dot(x + 1, y - 1, BIRD[0], a);
    else if (state === 2) p.dot(x + 1, y + 1, BIRD[1], a);
    else p.dot(x + 1, y, BIRD[1], a);
  }

  function flockAt(i, u) {
    const breathe = 1 + 0.32 * Math.sin((u * 2 + 0.1) * TAU), spin = (i % 2 ? 1 : -1) * u + rand(i, 1, 331);
    const r = (2 + rand(i, 2, 331) * 7) * breathe;
    const cx = 11 * Math.sin(u * TAU), cy = 3.5 * Math.sin((u * 2 + 0.2) * TAU);
    return [cx + Math.cos(spin * TAU) * r * 1.7, cy + Math.sin(spin * TAU) * r * 0.55 + (rand(i, 3, 331) - 0.5) * 4];
  }

  function starlingsCalm(p, u, info) {
    const tick = Math.round(u * info.frames);
    for (let i = 0; i < 22; i++) {
      const [x, y] = flockAt(i, u);
      wing(p, x, y, FLAP[(tick + i * 3) % 4]);
    }
  }

  const PECKER = pal({ k: "#2b2f3c", g: "#3f5e62", y: "#f0c040", l: "#8a6a50" });
  const STAND = ["..ky", "kgk.", ".l.."];
  const PECK = ["....", "kgk.", ".lky"];
  const SPOTS = [[-13, 0], [-7, 1], [-1, 0], [5, 1], [11, 0]];

  function starlingsFriendly(p, u, info) {
    const i = Math.round(u * info.frames);
    SPOTS.forEach(([x, flip], n) => {
      const beat = (i + n * 5) % 16, peck = beat === 3 || beat === 4 || beat === 7 || beat === 8;
      const hop = beat === 12 ? -1 : 0;
      blit(p, peck ? PECK : STAND, PECKER, x - 2, -2 + hop, { flip: !!flip });
    });
  }

  function starlingsAlarmed(p, u, info) {
    const tick = Math.round(u * (info.frames - 1));
    if (tick >= info.frames - 1) return;
    for (let i = 0; i < 16; i++) {
      const start = rand(i, 1, 341) * 0.3, k = clamp((u - start) / (1 - start));
      if (k <= 0) {
        p.px(-22 + i * 3, -1, BIRD[0]);
        continue;
      }
      const fan = (i / 15 - 0.5) * 1.9 - 0.25 + (rand(i, 2, 341) - 0.5) * 0.4, reach = ease.outQuad(k) * (22 + rand(i, 3, 341) * 20);
      const x = -22 + i * 3 + Math.sin(fan) * reach * 1.2 + Math.sin(k * 8 + i) * 1.5, y = -1 - Math.cos(fan) * reach;
      if (k > 0.72 && i % 2) p.dot(x, y, BIRD[0], 1 - (k - 0.72) / 0.28);
      else wing(p, x, y, FLAP[(tick * 2 + i) % 4], k > 0.75 ? 1 - (k - 0.75) / 0.25 : 1);
    }
  }

  function starlingsHidden(p, u, info) {
    const tick = Math.round(u * (info.frames - 1));
    if (tick >= info.frames - 1) return;
    const k = ease.inQuad(u);
    for (let i = 0; i < 22; i++) {
      const [x0, y0] = flockAt(i, 0);
      const x = x0 * (1 - k * 0.6) - k * 14, y = y0 * (1 - k * 0.6) + k * 14;
      wing(p, x, y, FLAP[(tick + i * 3) % 4], u > 0.55 ? 1 - (u - 0.55) / 0.45 : 1);
    }
  }

  const HILL_FLY = S.ramp(["#fbffb8", "#d8f76a", "#8fb347"]);

  function fliesKind(FLY) {
    function firefly(p, x, y, bright) {
      if (bright <= 0.08) return;
      if (bright > 0.45) p.px(x, y, bright > 0.8 ? FLY[0] : FLY[1]);
      else p.dot(x, y, FLY[2], 0.35 + bright);
      p.glow(Math.floor(x) + 0.5, Math.floor(y) + 0.5, 4, FLY[1], bright * 0.6);
    }

    const blink = (u, i, seed) => Math.max(0, Math.sin((u * (1 + (i % 3)) + rand(i, 1, seed)) * TAU)) ** 1.5;

    function flyAt(i, u, wide, tall, seed) {
      const turn = i % 2 ? 1 : -1;
      const x = (rand(i, 2, seed) - 0.5) * wide * 1.5 + wide * 0.22 * Math.sin((u * turn + rand(i, 3, seed)) * TAU);
      const y = (rand(i, 4, seed) - 0.5) * tall * 1.4 + tall * 0.25 * Math.sin((u * 2 * turn + rand(i, 5, seed)) * TAU);
      return [x, y];
    }

    function firefliesCalm(p, u) {
      for (let i = 0; i < 7; i++) {
        const [x, y] = flyAt(i, u, 20, 10, 351);
        firefly(p, x, y, blink(u, i, 351));
      }
    }

    function firefliesFriendly(p, u) {
      for (let i = 0; i < 6; i++) {
        const a = (u * (i % 2 ? 1 : -1) + i / 6) * TAU, r = 6 + (i % 3) * 2;
        firefly(p, Math.cos(a) * r, Math.sin(a) * r * 0.6 + Math.sin((u * 2 + i * 0.3) * TAU) * 2, 0.55 + 0.45 * blink(u, i, 361));
      }
    }

    function firefliesAlarmed(p, u, info) {
      if (Math.round(u * (info.frames - 1)) >= info.frames - 1) return;
      for (let i = 0; i < 7; i++) {
        const [x, y] = flyAt(i, 0, 20, 10, 351), k = ease.outQuad(u);
        firefly(p, x * (1 + k * 0.7) + Math.sin(u * 20 + i) * 1.2, y * (1 + k * 0.5) - k * 3, (1 - u) * (0.5 + 0.5 * ((Math.round(u * 9) + i) % 2)));
      }
    }

    function firefliesHidden(p, u, info) {
      if (Math.round(u * (info.frames - 1)) >= info.frames - 1) return;
      for (let i = 0; i < 7; i++) {
        const [x, y] = flyAt(i, 0, 20, 10, 351);
        firefly(p, x, y + u * 6, (1 - u) * Math.max(0.3, blink(0, i, 351)));
      }
    }

    return { calm: firefliesCalm, friendly: firefliesFriendly, alarmed: firefliesAlarmed, hidden: firefliesHidden };
  }

  const hillFlies = fliesKind(HILL_FLY);

  const OWL = pal({ b: "#8a6b4e", t: "#5e4632", m: "#c9ab82", y: "#ffd84a", k: "#1c1410", n: "#f09a3a", f: "#f0b050", s: "#5e4632" });
  const OWL_BODY = [
    "t.....t",
    "tbbbbbt",
    "byybyyb",
    "bykbykb",
    "bbbnbbb",
    "bmbmbmb",
    "bbmbmbb",
    ".bbbbb.",
    "..f.f..",
  ];
  const eyes = (top, low) => [OWL_BODY[0], OWL_BODY[1], top, low, ...OWL_BODY.slice(4)];
  const OWL_LEFT = eyes("byybyyb", "bkybkyb");
  const OWL_RIGHT = eyes("byybyyb", "bykbykb".split("").reverse().join(""));
  const OWL_BLINK = eyes("bbbbbbb", "bssbssb");
  const OWL_WIDE = eyes("bkybkyb", "byybyyb");
  const OWL_UP = [
    "b.........b",
    "bb..t.t..bb",
    ".bbbbbbbbb.",
    "...bykyb...",
    "...bbnbb...",
    "....bmb....",
  ];
  const OWL_DOWN = [
    "....t.t....",
    "...bbbbb...",
    ".bbbykybbb.",
    "bbbbbnbbbbb",
    "b...bmb...b",
    "....bbb....",
  ];
  const STUMP = pal({ b: "#6b5038", d: "#4a3626", l: "#8a6a4a", k: "#241a14" });
  const STUMP_ROWS = [
    "..lllll..",
    ".lkkkkkl.",
    ".bkkkkkb.",
    ".blbbbdb.",
    ".bbbdbbb.",
    ".blbbbdb.",
    ".bbbdbbb.",
    ".blbbbdb.",
    ".bbbbdbb.",
    "bblbbbdbb",
    "bbbdbbbbd",
  ];

  function owlEyes(p, x, y, k) {
    p.glow(x + 2, y + 3, 2.5, OWL.y, k);
    p.glow(x + 5, y + 3, 2.5, OWL.y, k);
  }

  function owlCalm(p, u, info) {
    const i = Math.round(u * info.frames);
    const rows = i === 4 || i === 5 || i === 16 || i === 17 ? OWL_BLINK : i >= 8 && i < 12 ? OWL_LEFT : i >= 20 && i < 24 ? OWL_RIGHT : OWL_BODY;
    blit(p, rows, OWL, -3, -8);
    if (i === 28 || i === 29) {
      p.px(-4, -3, OWL.b);
      p.px(4, -3, OWL.b);
      p.px(-4, -2, OWL.t);
      p.px(4, -2, OWL.t);
    }
    if (rows !== OWL_BLINK) owlEyes(p, -3, -8, 0.25);
  }

  function owlFriendly(p, u, info) {
    const i = Math.round(u * info.frames) % 16, lean = i >= 4 && i < 8 ? 1 : i >= 12 ? -1 : 0, bob = i % 8 < 2 ? -1 : 0;
    blit(p, OWL_BODY.slice(4), OWL, -3, -4);
    blit(p, (i === 9 ? OWL_BLINK : OWL_WIDE).slice(0, 4), OWL, -3 + lean, -8 + bob);
    if (i !== 9) owlEyes(p, -3 + lean, -8 + bob, 0.35);
  }

  function owlAlarmed(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    if (i < 2) {
      blit(p, OWL_WIDE, OWL, -3, -8 - i);
      p.px(-4, -5 - i, OWL.b);
      p.px(4, -5 - i, OWL.b);
      p.px(-5, -6 - i, OWL.t);
      p.px(5, -6 - i, OWL.t);
      return owlEyes(p, -3, -8 - i, 0.4);
    }
    const step = i - 2, x = -5 - step * 2.9, y = -9 - step * 2 - Math.sin(step * 1.3) * 1.5;
    blit(p, Math.floor(step / 2) % 2 ? OWL_DOWN : OWL_UP, OWL, x, y, { a: step > 7 ? 1 - (step - 7) / 4 : 1 });
  }

  function owlHidden(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    if (i >= info.frames - 1) return;
    const sink = [0, 0, 2, 4, 6, 8, 9][i];
    blit(p, i < 2 ? OWL_BLINK : OWL_BODY, OWL, -3, -8 + sink, { clip: 1 });
  }

  function owlPerch(p) {
    blit(p, STUMP_ROWS, STUMP, -4, -10);
    tuft(p, -4, 5, 371);
    tuft(p, 4.5, 4, 372);
  }

  return {
    rabbitCalm: hillRabbit.calm, rabbitFriendly: hillRabbit.friendly, rabbitAlarmed: hillRabbit.alarmed, rabbitHidden: hillRabbit.hidden, rabbitHop: hillRabbit.hop,
    butterfliesCalm, butterfliesFriendly, butterfliesAlarmed, butterfliesHidden,
    starlingsCalm, starlingsFriendly, starlingsAlarmed, starlingsHidden,
    firefliesCalm: hillFlies.calm, firefliesFriendly: hillFlies.friendly, firefliesAlarmed: hillFlies.alarmed, firefliesHidden: hillFlies.hidden,
    owlCalm, owlFriendly, owlAlarmed, owlHidden, owlPerch,
    critterKinds: { rabbitKind, fliesKind, blit, pal, wing, FLAP },
  };
}
