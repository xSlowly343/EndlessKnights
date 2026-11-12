import { meta as source } from "./ink-enso.js";
import { flash } from "./_flash-kit.js";

const fx = flash("ink", 2, source);

export const meta = fx.meta;
export const create = fx.create;
