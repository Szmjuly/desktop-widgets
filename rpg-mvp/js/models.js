import { CFG } from "./config.js";
import { uid, randInt } from "./utils.js";
import { SPECIES } from "./data_species.js";
import { MOVES } from "./data_moves.js";

export function calcStats(speciesId, level){
  const s = SPECIES[speciesId];
  const b = s.baseStats;

  // Simple derived stats, tuned for MVP.
  // hp grows faster to keep battles readable.
  const hp = Math.floor(b.hp + level * 3);
  const atk = Math.floor(b.atk * 0.4 + level * 2);
  const def = Math.floor(b.def * 0.4 + level * 2);
  const spd = Math.floor(b.spd * 0.4 + level * 2);

  return { hp, atk, def, spd };
}

/** Build the move list a creature knows at a given level (max 4, latest learned). */
export function getMovesForLevel(speciesId, level){
  const sp = SPECIES[speciesId];
  if (!sp || !sp.learnset) return [{ moveId: "tackle", pp: MOVES.tackle.pp }];
  const eligible = sp.learnset.filter(e => e.level <= level);
  // Take the last 4 eligible (most recently learned)
  const picked = eligible.slice(-4);
  return picked.map(e => ({
    moveId: e.moveId,
    pp: (MOVES[e.moveId]?.pp ?? 20),
  }));
}

export function createCreatureInstance(speciesId, level){
  const stats = calcStats(speciesId, level);
  const moves = getMovesForLevel(speciesId, level);
  return {
    uid: uid("c"),
    speciesId,
    nickname: null,
    level,
    stats,
    hp: stats.hp,
    maxHp: stats.hp,
    moves,          // [{ moveId, pp }]
    exp: 0,
    fainted: false,
    status: null,   // null | "burn" | "paralyze" | "poison" | "sleep" | "freeze"
    sleepTurns: 0,
    statStages: { atk: 0, def: 0, spd: 0 },
  };
}

export function createDefaultInventory(){
  return {
    items: [
      { id: "capsule_basic", name: "Capsule", qty: 5, type: "capture", meta: { bonus: 1.0 } },
      { id: "potion", name: "Potion", qty: 3, type: "heal", meta: { hp: 20 } },
    ]
  };
}

export const SHOP_CATALOG = [
  { id: "capsule_basic", name: "Capsule", price: 200, type: "capture", meta: { bonus: 1.0 } },
  { id: "potion", name: "Potion", price: 100, type: "heal", meta: { hp: 20 } },
  { id: "super_potion", name: "Super Potion", price: 300, type: "heal", meta: { hp: 50 } },
];

export function buyItem(player, catalogEntry, qty = 1) {
  const cost = catalogEntry.price * qty;
  if (player.money < cost) return false;
  player.money -= cost;
  const existing = player.inventory.items.find(i => i.id === catalogEntry.id);
  if (existing) {
    existing.qty += qty;
  } else {
    player.inventory.items.push({
      id: catalogEntry.id, name: catalogEntry.name, qty,
      type: catalogEntry.type, meta: { ...catalogEntry.meta },
    });
  }
  return true;
}

export function usePotion(player, creatureUid) {
  const creature = player.party.find(c => c.uid === creatureUid);
  if (!creature || creature.fainted || creature.hp >= creature.maxHp) return null;
  // Try potion first, then super_potion
  let item = player.inventory.items.find(i => i.type === "heal" && i.qty > 0);
  if (!item) return null;
  item.qty--;
  const healed = Math.min(item.meta.hp, creature.maxHp - creature.hp);
  creature.hp += healed;
  return { itemName: item.name, healed, creature };
}

// Rival starter logic: picks the type that beats the player's starter
const RIVAL_COUNTER = { sproutle: "embercub", embercub: "aquaff", aquaff: "sproutle" };

export function getRivalStarterId(playerStarterId) {
  return RIVAL_COUNTER[playerStarterId] || "embercub";
}

export function getItem(inventory, itemId){
  return inventory.items.find(it => it.id === itemId) || null;
}

export function consumeItem(inventory, itemId, n=1){
  const it = getItem(inventory, itemId);
  if (!it || it.qty < n) return false;
  it.qty -= n;
  return true;
}

export function addCreatureToCollections(player, creature){
  if (player.party.length < CFG.partyCap){
    player.party.push(creature);
    return { where: "party" };
  }
  player.storage.push(creature);
  return { where: "storage" };
}

export function createNewPlayer() {
  return {
    id: uid("p"),
    name: "Player",
    pos: { x: 10, y: 7 },
    facing: "down",
    mapId: "town",
    money: 500,
    saveSlot: 1,
    worldSeed: Math.floor(Math.random() * 2147483647),
    flags: {
      chosenStarter: false,
      starterSpeciesId: null,
      rivalName: null,
      rivalDefeated: {},     // mapId → true when rival beaten there
      rivalEncounters: 0,
    },
    party: [],
    storage: [],
    inventory: createDefaultInventory(),
  };
}

export function prefillStorageToTen(player){
  // Requirement interpretation:
  // Start with 10 creatures "in inventory" meaning storage is prefilled.
  // Starter still matters, so we fill storage with placeholders now.
  // Total creatures after starter will become 11, that is fine for MVP.
  const placeholders = ["wildbit", "mosslug", "pebblit"];
  while (player.storage.length < 10){
    const speciesId = placeholders[randInt(0, placeholders.length - 1)];
    const lv = randInt(2, 5);
    player.storage.push(createCreatureInstance(speciesId, lv));
  }
}
