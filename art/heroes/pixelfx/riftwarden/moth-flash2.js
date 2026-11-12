import { meta as source } from "./moth-scatter.js";
import { flash } from "./_flash-kit.js";

const fx = flash("moth", 2, source);

export const meta = fx.meta;
export const create = fx.create;
