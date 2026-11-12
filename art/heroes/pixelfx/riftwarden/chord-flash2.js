import { meta as source } from "./chord-ring.js";
import { flash } from "./_flash-kit.js";

const fx = flash("chord", 2, source);

export const meta = fx.meta;
export const create = fx.create;
