import { meta as source } from "./glass-rose.js";
import { flash } from "./_flash-kit.js";

const fx = flash("glass", 1, source);

export const meta = fx.meta;
export const create = fx.create;
