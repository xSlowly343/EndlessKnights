import { meta as source } from "./thunder-strike.js";
import { flash } from "./_flash-kit.js";

const fx = flash("thunder", 1, source);

export const meta = fx.meta;
export const create = fx.create;
