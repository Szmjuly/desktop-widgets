import { CFG } from "./config.js";
import { chance, randInt } from "./utils.js";
import { ENCOUNTER_TABLE_TOWN_GRASS } from "./data_species.js";
import { createCreatureInstance } from "./models.js";

function weightedPick(table){
  const total = table.reduce((s, e) => s + e.weight, 0);
  let r = Math.random() * total;
  for (const e of table){
    r -= e.weight;
    if (r <= 0) return e;
  }
  return table[table.length - 1];
}

export function maybeStartEncounter(game){
  if (game.player.mapId !== "town" && game.player.mapId !== "route1") return false;
  const onGrass = game.mapInGrass(game.player.pos.x, game.player.pos.y);
  if (!onGrass) return false;

  if (!chance(CFG.encounterChancePerStep)) return false;

  const entry = weightedPick(ENCOUNTER_TABLE_TOWN_GRASS);
  const lv = randInt(entry.minLv, entry.maxLv);
  const wild = createCreatureInstance(entry.speciesId, lv);

  game.startBattle(wild);
  return true;
}
