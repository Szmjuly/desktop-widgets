import { CFG } from "./config.js";
import { uid, randInt } from "./utils.js";
import { SPECIES } from "./data_species.js";

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

export function createCreatureInstance(speciesId, level){
  const stats = calcStats(speciesId, level);
  return {
    uid: uid("c"),
    speciesId,
    nickname: null,
    level,
    stats,
    hp: stats.hp,
    maxHp: stats.hp,
    moves: ["tackle"],
    exp: 0,
    fainted: false
  };
}

export function createDefaultInventory(){
  return {
    items: [
      { id: "capsule_basic", name: "Capsule", qty: 5, type: "capture", meta: { bonus: 1.0 } }
    ]
  };
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
    money: 0,
    saveSlot: 1,
    flags: {
      chosenStarter: false,
      starterSpeciesId: null,
      firstRivalDone: false,
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
