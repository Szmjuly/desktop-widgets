import { CFG } from "./config.js";
import { deepCopy } from "./utils.js";
import { createNewPlayer, prefillStorageToTen } from "./models.js";

function getKey(slot) {
  return CFG.saveKey(slot);
}

export function saveGame(game) {
  const slot = game.player?.saveSlot ?? 1;
  const payload = {
    version: CFG.saveVersion,
    savedAt: new Date().toISOString(),
    player: deepCopy(game.player),
  };
  localStorage.setItem(getKey(slot), JSON.stringify(payload));
}

export function loadGame(slot) {
  const raw = localStorage.getItem(getKey(slot));
  if (!raw) return null;

  try {
    const parsed = JSON.parse(raw);
    if (!parsed || parsed.version !== CFG.saveVersion) return null;
    if (!parsed.player) return null;
    return parsed;
  } catch {
    return null;
  }
}

export function getSaveSlotInfo(slot) {
  const loaded = loadGame(slot);
  if (!loaded || !loaded.player) return { exists: false, slot };
  const p = loaded.player;
  const partyCount = (p.party && p.party.length) || 0;
  const mapNames = {
    town: "Home Town", lab: "Lab", house: "House", shop: "Shop",
    rw1: "Route W1", rw2_n: "Route W2 North", rw2_s: "Route W2 South",
    re1: "Route E1", re2_n: "Route E2 North", re2_s: "Route E2 South",
    west_town_n: "Pinegrove", west_town_s: "Dusthaven",
    east_town_n: "Coralport", east_town_s: "Ashvale",
    route_west: "Route West", route_east: "Route East",
  };
  const mapName = mapNames[p.mapId] || p.mapId;
  return {
    exists: true,
    slot,
    playerName: p.name || "Player",
    mapName,
    savedAt: loaded.savedAt,
    partyCount,
  };
}

export function clearSave(slot) {
  localStorage.removeItem(getKey(slot));
}

export function newGameState() {
  const player = createNewPlayer();
  prefillStorageToTen(player);
  return { player };
}
