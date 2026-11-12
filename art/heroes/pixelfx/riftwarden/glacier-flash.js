import { meta as source } from "./glacier-bloom.js";
import { flash } from "./_flash-kit.js";

const fx = flash("glacier", 1, source);

export const meta = fx.meta;
export const create = fx.create;
