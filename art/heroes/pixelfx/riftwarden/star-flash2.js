import { meta as source } from "./star-fall.js";
import { flash } from "./_flash-kit.js";

const fx = flash("star", 2, source);

export const meta = fx.meta;
export const create = fx.create;
