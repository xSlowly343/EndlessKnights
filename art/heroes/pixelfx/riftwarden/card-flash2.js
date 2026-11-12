import { meta as source } from "./card-cut.js";
import { flash } from "./_flash-kit.js";

const fx = flash("card", 2, source);

export const meta = fx.meta;
export const create = fx.create;
