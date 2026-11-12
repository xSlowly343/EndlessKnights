import { meta as source } from "./guardian-descent.js";
import { flash } from "./_flash-kit.js";

const fx = flash("guardian", 2, source);

export const meta = fx.meta;
export const create = fx.create;
