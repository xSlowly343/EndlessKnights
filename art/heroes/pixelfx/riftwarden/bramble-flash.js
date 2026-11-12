import { meta as source } from "./bramble-bloom.js";
import { flash } from "./_flash-kit.js";

const fx = flash("bramble", 1, source);

export const meta = fx.meta;
export const create = fx.create;
