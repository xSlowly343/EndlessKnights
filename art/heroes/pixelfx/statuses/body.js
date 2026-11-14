export default function body(fx) {
  const { clamp, seg, TAU, rng, cycle, burst, WHITE } = fx;
  const K = fx.kit;
  const VENOM = K.ramp(["#1f4a1a", "#3e7a2a", "#6fae3a", "#9bd04a", "#d6f08a"]);
  const FIRE = K.ramp(["#7a2412", "#c8502a", "#ff7a3c", "#ffb04a", "#ffe08a"]);
  const COAL = K.ramp(["#5a1c10", "#c8502a", "#ff9a3c", "#ffe08a"]);
  const CRACK = K.ramp(["#1b2236", "#9fb4d6", "#e6e9f0", "#5b9bff"]);
  const RUST = K.ramp(["#3a3216", "#6b5a2a", "#7fae3a", "#9bd04a", "#d6f08a"]);
  const LOOP = 8 / 12;

  const left = (g) => -g.w / 2;
  const top = (g) => -(g.h - 1);

  function poison(tier) {
    const drips = [4, 8, 13][tier - 1], pool = [0.16, 0.26, 0.38][tier - 1];
    return (p, phase, info, g) => {
      const s = g.s, level = g.h * pool;
      for (let x = left(g); x < -left(g); x++) {
        const wave = Math.sin((x / (5 * s) + phase) * TAU) * s + Math.sin((x / (3 * s) - phase * 2) * TAU) * 0.6 * s;
        const rim = Math.round(level + wave);
        for (let row = 0; row <= rim; row++) {
          const edge = row === rim;
          if (edge) p.px(x, -row, VENOM[3]);
          else if ((x + row * 2 + Math.floor(row / 2)) % 3 === 0) p.px(x, -row, row < rim * 0.5 ? VENOM[0] : VENOM[1]);
        }
      }
      cycle(phase * LOOP, LOOP, drips * s, 31 + tier, () => LOOP, (age, life, r) => {
        const x = Math.floor(r.range(left(g) + 2, -left(g) - 2)), from = r.range(0.55, 0.95) * g.h, k = age / life;
        const fall = from - (from - level) * k * k, tail = 2 * s + Math.round(k * 3 * s);
        for (let i = 0; i <= tail; i++) {
          const y = -(fall + i);
          if (y < top(g)) break;
          const color = i === 0 ? VENOM[4] : i < s + 1 ? VENOM[3] : i < tail ? VENOM[2] : VENOM[1];
          for (let w = 0; w < s; w++) p.px(x + w, y, color);
        }
        if (s > 1 || k < 0.5) p.px(x + s, -(fall + 1), VENOM[2]);
      });
      cycle(phase * LOOP, LOOP, (2 + tier) * s, 77 + tier, (r) => r.range(0.25, 0.4), (age, life, r) => {
        const x = r.range(left(g) + 3, -left(g) - 3), y = -r.range(1, Math.max(2, level - 1)), k = age / life;
        const radius = (0.6 + k * 1.6) * s;
        if (k < 0.75) p.circle(x, y, radius, k < 0.4 ? VENOM[3] : VENOM[4]);
        else for (let i = 0; i < 4; i++) p.dot(x + Math.cos(i * TAU / 4 + 0.8) * (radius + 1), y + Math.sin(i * TAU / 4 + 0.8) * (radius + 1), VENOM[4]);
      });
    };
  }

  function burnBody(p, phase, info, g) {
    const s = g.s;
    for (let x = left(g); x < -left(g); x++) {
      const lick = Math.sin((x / (6 * s) + phase) * TAU) * 2.2 * s + Math.sin((x / (2.5 * s) - phase * 2) * TAU) * 1.2 * s + Math.sin((x / (11 * s) + phase * 3) * TAU) * s;
      const depth = Math.round(g.h * 0.34 + lick);
      for (let d = 0; d < depth; d++) {
        const y = d, heat = 1 - d / depth;
        const bayer = ((x & 1) * 2 + (y & 1) * 3 + ((x ^ y) & 1)) % 4;
        if (d === depth - 1) p.px(x, y, FIRE[1]);
        else if (heat > 0.72) p.px(x, y, (x + y) % 2 ? FIRE[4] : FIRE[3]);
        else if (heat > 0.4 ? bayer !== 0 : bayer > 1) p.px(x, y, heat > 0.4 ? FIRE[2] : FIRE[1]);
      }
    }
    cycle(phase * LOOP, LOOP, 7 * s, 53, (r) => r.range(0.3, 0.5), (age, life, r) => {
      const x = r.range(left(g) + 2, -left(g) - 2), y = r.range(0.3, 0.65) * g.h, k = age / life;
      const rise = y - k * 9 * s;
      for (let i = 0; i < 2 * s + 1; i++) p.px(x + Math.round(Math.sin(k * 5 + i) * 0.8), rise + i, i === 0 ? FIRE[4] : i < 2 ? FIRE[3] : FIRE[2]);
    });
  }

  function ember(p, phase, info, g) {
    const s = g.s;
    burst(34 * s, 91, (r, i) => {
      const x = Math.floor(r.range(left(g) + 1, -left(g) - 1)), y = Math.floor(r.range(left(g) + 1, -left(g) - 1));
      const beat = K.pulse(phase, 1 + (i % 2), r.next());
      if (beat < 0.12) return;
      const hot = beat > 0.8;
      p.px(x, y, hot ? COAL[3] : beat > 0.5 ? COAL[2] : COAL[1]);
      if (hot && s > 1) p.px(x + 1, y, COAL[2]);
      if (hot) {
        p.px(x, y + 1, COAL[1]);
        p.px(x - 1, y, COAL[0]);
      }
    });
    cycle(phase * LOOP, LOOP, 3 * s, 97, () => LOOP * 0.8, (age, life, r) => {
      const x = r.range(left(g) + 3, -left(g) - 3), y = r.range(-0.2, 0.3) * g.h, k = age / life;
      p.px(x + Math.sin(k * 6 + r.next() * 6) * 1.5, y - k * 10 * s, k < 0.5 ? COAL[3] : k < 0.8 ? COAL[2] : COAL[1]);
    });
  }

  function crackPath(r, g, slot, slots) {
    const s = g.s, points = [];
    const spread = Math.sqrt((slot + 0.5) / slots) * g.w * 0.3, turn = slot * 2.4 + 0.6;
    let x = Math.cos(turn) * spread, y = Math.sin(turn) * spread;
    let heading = r.range(0.2, 1.3) * r.sign() + (r.chance(0.5) ? Math.PI : 0) + Math.PI / 2;
    const steps = r.int(4, 6);
    for (let i = 0; i < steps; i++) {
      points.push([x, y]);
      heading += r.range(-0.9, 0.9);
      const reach = r.range(2, 4.5) * s;
      x += Math.cos(heading) * reach;
      y += Math.sin(heading) * reach * 0.9;
    }
    return points;
  }

  function pixelsOf(points) {
    const out = [];
    for (let i = 1; i < points.length; i++) {
      const [ax, ay] = points[i - 1], [bx, by] = points[i];
      const n = Math.max(1, Math.ceil(Math.max(Math.abs(bx - ax), Math.abs(by - ay))));
      for (let k = 0; k < n; k++) out.push([Math.floor(ax + ((bx - ax) * k) / n), Math.floor(ay + ((by - ay) * k) / n)]);
    }
    return out;
  }

  function fracture(tier) {
    const cracks = [1, 2, 4, 6][tier - 1];
    return (p, phase, info, g) => {
      for (let c = 0; c < cracks; c++) {
        const r = rng(401, c, g.s), main = pixelsOf(crackPath(r, g, c, cracks));
        const branch = main.length > 6 ? pixelsOf([main[Math.floor(main.length * 0.45)], [main[Math.floor(main.length * 0.45)][0] + r.range(-5, 5) * g.s, main[Math.floor(main.length * 0.45)][1] + r.range(-5, 5) * g.s]]) : [];
        for (const [x, y] of main.concat(branch)) p.px(x + 1, y + 1, CRACK[0]);
        const glint = Math.floor(((phase + c * 0.37) % 1) * (main.length + 6)) - 3;
        main.forEach(([x, y], i) => {
          const lit = Math.abs(i - glint) <= g.s;
          p.px(x, y, lit ? WHITE : CRACK[2]);
          if (g.s > 1) p.px(x + 1, y, lit ? CRACK[2] : CRACK[1]);
        });
        for (const [x, y] of branch) p.px(x, y, CRACK[1]);
      }
      if (tier < 4) return;
      cycle(phase * LOOP, LOOP, 5 * g.s, 409, () => 0.3, (age, life, r) => {
        const x = r.range(left(g) + 5, -left(g) - 5), y = r.range(left(g) + 5, -left(g) - 5);
        fx.sparkle(p, x, y, (1 + g.s) * (1 - Math.abs(age / life - 0.5) * 2), WHITE, CRACK[3], 0.8);
      });
    };
  }

  function corrosion(p, u, info, g) {
    const s = g.s, cx = 0, cy = 0;
    const grow = clamp(seg(u, 0, 0.55)), eat = seg(u, 0.45, 1);
    const radius = (3 + grow * 9) * s;
    for (let x = left(g); x < -left(g); x++)
      for (let y = left(g); y < -left(g); y++) {
        const angle = Math.atan2(y - cy, x - cx);
        const ragged = radius * (0.78 + 0.22 * Math.sin(angle * 5 + 1.3) + 0.12 * Math.sin(angle * 9 - 0.7));
        const d = Math.hypot(x - cx, y - cy) / Math.max(ragged, 0.1);
        if (d > 1) continue;
        const grain = fx.rand(Math.floor(x / s), Math.floor(y / s), 7);
        if (grain < eat * 1.15 - (1 - d) * 0.25) continue;
        p.px(x, y, d > 0.82 ? RUST[3] : d > 0.6 ? RUST[2] : grain > 0.6 ? RUST[1] : RUST[0]);
      }
    burst(7 * s, 613, (r) => {
      const a = r.range(0, TAU), reach = radius * r.range(0.5, 1.1), k = clamp(u * 1.4 - r.range(0, 0.4));
      if (k <= 0 || k >= 1) return;
      p.px(cx + Math.cos(a) * reach, cy + Math.sin(a) * reach - k * 5 * s, k < 0.5 ? RUST[4] : RUST[3]);
    });
  }

  return { poison1: poison(1), poison2: poison(2), poison3: poison(3), burnBody, ember, fracture1: fracture(1), fracture2: fracture(2), fracture3: fracture(3), fracture4: fracture(4), corrosion };
}
