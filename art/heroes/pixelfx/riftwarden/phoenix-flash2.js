import { meta as source } from "./phoenix-rise.js";
import { flash } from "./_flash-kit.js";

const fx = flash("phoenix", 2, source);

export const meta = fx.meta;
export const create = fx.create;
