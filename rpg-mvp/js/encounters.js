import { CFG } from "./config.js";
import { chance, randInt } from "./utils.js";
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
  const mapId = game.player.mapId;
  // Use game's encounter tables (may be procedurally generated)
  const tables = game.encounterTables || {};
  // Also check the map's encounterTableId for procedural routes
  const map = game.map;
  const tableId = map?.encounterTableId || mapId;
  const table = tables[tableId] || tables[mapId];
  if (!table) return false;

  const onGrass = game.mapInGrass(game.player.pos.x, game.player.pos.y);
  if (!onGrass) return false;

  if (!chance(CFG.encounterChancePerStep)) return false;

  const entry = weightedPick(table);
  const lv = randInt(entry.minLv, entry.maxLv);
  const wild = createCreatureInstance(entry.speciesId, lv);

  game.startBattle(wild);
  return true;
}
