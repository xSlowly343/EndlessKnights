import { chain, PAL } from "../../core/index.js";
import * as fireball from "../fire/fireball.js";
import * as blast from "../explosions/small-explosion.js";

const FLY = 0.32;

export const meta = {
  name: "Fire hurl",
  desc: "A fireball leaves the open hand and crosses the whole strip in a third of a second, bursting on a target far ahead.",
  tags: ["move", "fire", "projectile", "explosion", "one-shot"],
  size: [460, 260],
  origin: [70, 210],
  duration: FLY + 5.4,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { seed: 2, tx: 150, hand: 29 },
};

export function create(o) {
  return chain([
    { fx: fireball, t: 0, until: FLY, x: (t) => 6 + (t / FLY) * (o.tx - 6), y: -o.hand },
    { fx: blast, t: FLY, x: o.tx },
  ])(o);
}
