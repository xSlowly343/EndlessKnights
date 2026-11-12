import { meta as source } from "./firework-bloom.js";
import { flash } from "./_flash-kit.js";

const fx = flash("firework", 2, source);

export const meta = fx.meta;
export const create = fx.create;
