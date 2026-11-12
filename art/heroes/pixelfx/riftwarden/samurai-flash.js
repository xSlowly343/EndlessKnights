import { meta as source } from "./samurai-iai.js";
import { flash } from "./_flash-kit.js";

const fx = flash("samurai", 1, source);

export const meta = fx.meta;
export const create = fx.create;
