import { meta as source } from "./void-burst.js";
import { flash } from "./_flash-kit.js";

const fx = flash("void", 1, source);

export const meta = fx.meta;
export const create = fx.create;
