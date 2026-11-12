import { meta as source } from "./raven-omen.js";
import { flash } from "./_flash-kit.js";

const fx = flash("raven", 2, source);

export const meta = fx.meta;
export const create = fx.create;
