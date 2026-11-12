import { meta as source } from "./vine-maw.js";
import { flash } from "./_flash-kit.js";

const fx = flash("vine", 2, source);

export const meta = fx.meta;
export const create = fx.create;
