import { chain, PAL } from "../../core/index.js";
import * as fireball from "../fire/fireball.js";
import * as blast from "../explosions/small-explosion.js";

const FLY = 0.32, TX = 150, HAND = 29;

export const meta = {
  name: "Fire hurl",
  desc: "A fireball leaves the open hand and crosses the whole strip in a third of a second, bursting on a target far ahead.",
  tags: ["move", "fire", "projectile", "explosion", "one-shot"],
  size: [400, 260],
  origin: [70, 210],
  duration: FLY + 5.4,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { seed: 2 },
};

export const create = chain([
  { fx: fireball, t: 0, until: FLY, x: (t) => 6 + (t / FLY) * (TX - 6), y: -HAND },
  { fx: blast, t: FLY, x: TX },
]);
