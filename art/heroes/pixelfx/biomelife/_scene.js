export default function scene(fx) {
  const { hex, TAU, rand, wrap, clamp, tileFbm } = fx;
  const W = 160, H = 60, HILLS = 23, MEADOW = 38, GROUND = 52;
  const ZONE = { left: 120, top: GROUND - 20 };

  const ramp = (list) => list.map((c) => hex(c));
  const inZone = (x, y) => wrap(x, W) >= ZONE.left - 1 && y >= ZONE.top - 1;
  const loop = (u, turns = 1, shift = 0) => Math.sin((u * turns + shift) * TAU);
  const beat = (u, steps, shift = 0) => wrap(Math.floor(u * steps + 0.0001) + shift, steps);

  function tile(p) {
    return {
      px: (x, y, c) => p.px(wrap(Math.floor(x), W), y, c),
      dot: (x, y, c, a = 1) => p.dot(wrap(Math.floor(x), W), y, c, a),
      blend: (x, y, c, a) => p.blend(wrap(Math.floor(x), W), y, c, a),
      glow: (x, y, r, c, k) => {
        const at = wrap(x, W);
        p.glow(at, y, r, c, k);
        if (at < r) p.glow(at + W, y, r, c, k);
        if (at > W - r) p.glow(at - W, y, r, c, k);
      },
    };
  }

  function cloud(t, cx, base, wide, tall, seed, tones, alpha = 1, soft = false) {
    const lumps = Math.max(2, Math.round(wide / (tall * 1.15)));
    const circles = [];
    for (let i = 0; i < lumps; i++) {
      const v = (i + 0.5) / lumps, edge = 1 - Math.abs(v - 0.5) * 1.25;
      const r = tall * (0.5 + 0.3 * rand(seed, i, 2)) * Math.max(0.45, edge) * 1.25;
      circles.push([cx - wide / 2 + v * wide + (rand(seed, i, 1) - 0.5) * tall * 0.5, base - r * 0.3, r]);
    }
    const inside = (x, y) => y <= base && circles.some(([lx, ly, r]) => (x + 0.5 - lx) ** 2 + (y + 0.5 - ly) ** 2 <= r * r);
    const foot = Math.max(1, Math.round(tall * 0.22));
    for (let x = Math.floor(cx - wide / 2 - tall * 1.3); x <= cx + wide / 2 + tall * 1.3; x++) {
      for (let y = Math.floor(base - tall * 1.7); y <= base; y++) {
        if (!inside(x, y)) continue;
        const crown = !inside(x, y - 1) || !inside(x, y - 2) || (tall > 5 && !inside(x - 1, y - 2));
        const color = y > base - foot ? tones[2] : crown ? tones[0] : tones[1];
        if (soft) t.blend(x, y, color, alpha);
        else t.dot(x, y, color, alpha);
      }
    }
  }

  function veil(p, top, bottom, color, alphaAt) {
    for (let y = top; y < bottom; y++) {
      const a = alphaAt((y - top) / Math.max(1, bottom - top - 1), y);
      if (a <= 0.004) continue;
      for (let x = 0; x < W; x++) p.blend(x, y, color, typeof a === "function" ? a(x) : a);
    }
  }

  function haze(p, u, top, bottom, color, strength, seed, turns = 1, grain = 0.07) {
    const cells = Math.round(W * grain);
    for (let y = top; y < bottom; y++) {
      const v = (y - top) / Math.max(1, bottom - top - 1), shape = Math.sin(v * Math.PI);
      for (let x = 0; x < W; x++) {
        const n = tileFbm((x / W) * cells + u * turns * cells, y * grain * 1.8, cells, 0, seed, 2);
        const a = strength * shape * clamp(0.55 + n * 0.9);
        if (a > 0.01) p.blend(x, y, color, a);
      }
    }
  }

  return { W, H, HILLS, MEADOW, GROUND, ZONE, ramp, inZone, loop, beat, tile, cloud, veil, haze };
}
