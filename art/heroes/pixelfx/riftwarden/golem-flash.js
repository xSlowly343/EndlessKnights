import { meta as source } from "./golem-fist.js";
import { flash } from "./_flash-kit.js";

const fx = flash("golem", 1, source);

export const meta = fx.meta;
export const create = fx.create;
