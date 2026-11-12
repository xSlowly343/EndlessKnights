import { meta as source } from "./serpent-strike.js";
import { flash } from "./_flash-kit.js";

const fx = flash("serpent", 1, source);

export const meta = fx.meta;
export const create = fx.create;
