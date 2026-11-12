import { meta as source } from "./blade-ring.js";
import { flash } from "./_flash-kit.js";

const fx = flash("blade", 1, source);

export const meta = fx.meta;
export const create = fx.create;
