export default function icons(fx) {
  const { hex } = fx;
  const RIM = hex("#141a2c");
  const INK = { g: "#9bd04a", G: "#3e7a2a", o: "#ff7a3c", O: "#c8502a", y: "#ffe08a", Y: "#f0e04c", p: "#b07cff", P: "#4a2a7a", a: "#f0b43c", r: "#e0574a", R: "#7a1f1a", w: "#ffffff", s: "#c9d2e6", S: "#5b6788", b: "#5bb8ff", B: "#2a6fb0", t: "#c9a066", T: "#7a5a36", c: "#bfe6f5", k: "#2a3350" };
  const ART = {
    poison: ["...g...", "...g...", "..ggg..", ".gGggg.", ".gGgwg.", ".gGGgg.", "..ggg.."],
    burn: ["...o...", "..oo...", "..oyo.o", ".ooyoo.", ".oyyyo.", ".Oywyo.", "..OOO.."],
    ember: [".......", "..o....", ".....o.", "..OO...", ".OooO..", "OoyoOO.", ".OOOO.."],
    curse: [".......", ".ppppp.", "pPPwPPp", "pPwPwPp", "pPPwPPp", ".ppppp.", "..p.p.."],
    mark: ["...a...", "..a.a..", ".a...a.", "a..r..a", ".a...a.", "..a.a..", "...a..."],
    stun: ["...Y...", "...Y...", "YYYwYYY", ".YwwwY.", "..YwY..", ".YY.YY.", "Y.....Y"],
    blind: [".......", "s.....s", ".sssss.", "skkskks", ".sssss.", "s.....s", "......."],
    fracture: ["s...s..", ".s.s...", "..ws...", "...ws..", "..s.ws.", ".s...s.", "s....s."],
    vulnerable: ["rrr.rrr", "rRr.rRr", "rRRrRRr", ".rRRRr.", ".rR.Rr.", "..r.r..", "...r..."],
    earthShred: [".ttttt.", "ttTtTtt", "tT.tTTt", ".tT.Tt.", ".tTtTt.", "..tTt..", "...t..."],
    waterTime: ["..sss..", ".s.b.s.", "s..b..s", "s..bb.s", "s.....s", ".s...s.", "..sss.."],
    weakened: [".sssss.", "sswswss", "skkskks", "sssssss", ".ss.ss.", ".sssss.", ".s.s.s."],
    fire: ["...o...", "..oo...", "..oyo..", ".ooyoo.", ".oyyyo.", ".oywyo.", "..ooo.."],
    air: [".ccc...", "c...c..", "..cc.c.", "ccccc..", ".......", "cccccc.", ".....c."],
    earth: [".......", "...t...", "..ttT..", ".tttTT.", ".ttTTT.", "tttTTTT", "ttTTTTT"],
    lightning: ["...YY..", "..YY...", ".YY....", ".YYYYY.", "...YY..", "..YY...", ".YY...."],
    water: ["...b...", "...b...", "..bbb..", ".bcbbb.", ".bcbbb.", ".bbbBb.", "..bBb.."],
    corrosion: ["..g....", ".gGg.g.", "gG.Gg..", ".gGg...", "..g.gg.", ".g.gGGg", "....gg."],
    resists: ["...s...", "..sSs..", ".sSSSs.", "sSSSSSs", "..sSs..", "..sSs..", "..sss.."],
    weakTo: ["..rrr..", "..rRr..", "..rRr..", "rRRRRRr", ".rRRRr.", "..rRr..", "...r..."]
  };
  const ORDER = Object.keys(ART);

  function icons(p, u, info) {
    const rows = ART[ORDER[Math.round(u * (info.frames - 1))]];
    const filled = (x, y) => y >= 0 && y < 7 && x >= 0 && x < 7 && rows[y][x] !== ".";
    for (let y = -1; y <= 7; y++)
      for (let x = -1; x <= 7; x++) {
        if (filled(x, y)) p.px(x - 3, y - 3, hex(INK[rows[y][x]]));
        else if (filled(x + 1, y) || filled(x - 1, y) || filled(x, y + 1) || filled(x, y - 1)) p.px(x - 3, y - 3, RIM);
      }
  }

  icons.order = ORDER;
  return { icons };
}
