import { meta as source } from "./guardian-descent.js";
import { flash } from "./_flash-kit.js";

const fx = flash("guardian", 1, source);

export const meta = fx.meta;
export const create = fx.create;
