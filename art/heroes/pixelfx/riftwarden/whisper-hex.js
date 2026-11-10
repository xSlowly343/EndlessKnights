import { chain } from "../../core/index.js";
import * as missile from "../projectiles/magic-missile.js";
import * as hands from "../shadow/grasping-hands.js";

const HIT = 0.82, TX = 96;

export const meta = {
  name: "Whisper hex",
  desc: "A violet mote leaves the skull on the staff and weaves to the target; where it lands a dark pool opens and shadow hands burst up to clutch it.",
  tags: ["move", "shadow", "curse", "projectile", "one-shot"],
  size: [300, 170],
  origin: [30, 150],
  duration: HIT + 3.1,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { seed: 3 },
};

export const create = chain([
  { fx: missile, t: 0, until: HIT + 0.5, height: 50, reach: TX },
  { fx: hands, t: HIT - 0.05, x: TX },
]);
