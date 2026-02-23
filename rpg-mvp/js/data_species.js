export const SPECIES = {
  // ── Starters ──
  sproutle: {
    id: "sproutle",
    name: "Sproutle",
    type: "grass",
    baseStats: { hp: 45, atk: 49, def: 49, spd: 45 },
    learnset: [
      { moveId: "tackle", level: 1 },
      { moveId: "growl", level: 1 },
      { moveId: "vine_whip", level: 5 },
      { moveId: "razor_leaf", level: 9 },
      { moveId: "sleep_powder", level: 13 },
      { moveId: "seed_bomb", level: 17 },
    ],
    color: "#5fbf6a"
  },
  embercub: {
    id: "embercub",
    name: "Embercub",
    type: "fire",
    baseStats: { hp: 39, atk: 52, def: 43, spd: 65 },
    learnset: [
      { moveId: "scratch", level: 1 },
      { moveId: "growl", level: 1 },
      { moveId: "ember", level: 5 },
      { moveId: "quick_attack", level: 9 },
      { moveId: "bite", level: 13 },
      { moveId: "flame_burst", level: 17 },
    ],
    color: "#ff8a4c"
  },
  aquaff: {
    id: "aquaff",
    name: "Aquaff",
    type: "water",
    baseStats: { hp: 44, atk: 48, def: 65, spd: 43 },
    learnset: [
      { moveId: "tackle", level: 1 },
      { moveId: "growl", level: 1 },
      { moveId: "water_gun", level: 5 },
      { moveId: "aqua_jet", level: 9 },
      { moveId: "harden", level: 13 },
      { moveId: "bubble_beam", level: 17 },
    ],
    color: "#4cc0ff"
  },

  // ── Wild (town grass) ──
  wildbit: {
    id: "wildbit",
    name: "Wildbit",
    type: "normal",
    baseStats: { hp: 35, atk: 40, def: 35, spd: 55 },
    learnset: [
      { moveId: "tackle", level: 1 },
      { moveId: "quick_attack", level: 4 },
      { moveId: "bite", level: 8 },
    ],
    color: "#d7d36b"
  },
  mosslug: {
    id: "mosslug",
    name: "Mosslug",
    type: "grass",
    baseStats: { hp: 50, atk: 38, def: 55, spd: 25 },
    learnset: [
      { moveId: "tackle", level: 1 },
      { moveId: "vine_whip", level: 4 },
      { moveId: "poison_sting", level: 7 },
      { moveId: "sleep_powder", level: 11 },
    ],
    color: "#7ad17a"
  },
  pebblit: {
    id: "pebblit",
    name: "Pebblit",
    type: "rock",
    baseStats: { hp: 40, atk: 55, def: 60, spd: 20 },
    learnset: [
      { moveId: "tackle", level: 1 },
      { moveId: "harden", level: 3 },
      { moveId: "rock_throw", level: 6 },
      { moveId: "rock_slide", level: 12 },
    ],
    color: "#b9b0a3"
  },

  // ── Wild (Route West) ──
  zapplet: {
    id: "zapplet",
    name: "Zapplet",
    type: "electric",
    baseStats: { hp: 35, atk: 42, def: 30, spd: 70 },
    learnset: [
      { moveId: "tackle", level: 1 },
      { moveId: "thunder_shock", level: 4 },
      { moveId: "quick_attack", level: 7 },
      { moveId: "spark", level: 11 },
    ],
    color: "#ffe14c"
  },
  thornpup: {
    id: "thornpup",
    name: "Thornpup",
    type: "grass",
    baseStats: { hp: 42, atk: 50, def: 45, spd: 48 },
    learnset: [
      { moveId: "scratch", level: 1 },
      { moveId: "vine_whip", level: 4 },
      { moveId: "bite", level: 7 },
      { moveId: "razor_leaf", level: 11 },
    ],
    color: "#3daa5f"
  },

  // ── Wild (Route East) ──
  sootfox: {
    id: "sootfox",
    name: "Sootfox",
    type: "fire",
    baseStats: { hp: 38, atk: 50, def: 35, spd: 65 },
    learnset: [
      { moveId: "scratch", level: 1 },
      { moveId: "ember", level: 4 },
      { moveId: "quick_attack", level: 7 },
      { moveId: "flame_burst", level: 12 },
    ],
    color: "#e8734a"
  },
  venomite: {
    id: "venomite",
    name: "Venomite",
    type: "poison",
    baseStats: { hp: 40, atk: 45, def: 40, spd: 55 },
    learnset: [
      { moveId: "tackle", level: 1 },
      { moveId: "poison_sting", level: 3 },
      { moveId: "bite", level: 7 },
      { moveId: "sludge", level: 11 },
    ],
    color: "#a855c7"
  },
  coralshell: {
    id: "coralshell",
    name: "Coralshell",
    type: "water",
    baseStats: { hp: 55, atk: 40, def: 65, spd: 25 },
    learnset: [
      { moveId: "tackle", level: 1 },
      { moveId: "water_gun", level: 4 },
      { moveId: "harden", level: 7 },
      { moveId: "bubble_beam", level: 12 },
    ],
    color: "#ff7eb3"
  },
};

// ── Encounter tables per map ──
export const ENCOUNTER_TABLES = {
  town: [
    { speciesId: "wildbit", weight: 50, minLv: 2, maxLv: 4 },
    { speciesId: "mosslug", weight: 30, minLv: 2, maxLv: 3 },
    { speciesId: "pebblit", weight: 20, minLv: 3, maxLv: 4 },
  ],
  route_west: [
    { speciesId: "zapplet",  weight: 35, minLv: 3, maxLv: 5 },
    { speciesId: "thornpup", weight: 30, minLv: 3, maxLv: 5 },
    { speciesId: "wildbit",  weight: 20, minLv: 3, maxLv: 5 },
    { speciesId: "mosslug",  weight: 15, minLv: 4, maxLv: 6 },
  ],
  route_east: [
    { speciesId: "sootfox",    weight: 30, minLv: 4, maxLv: 6 },
    { speciesId: "venomite",   weight: 25, minLv: 4, maxLv: 6 },
    { speciesId: "coralshell", weight: 20, minLv: 4, maxLv: 7 },
    { speciesId: "pebblit",    weight: 15, minLv: 5, maxLv: 7 },
    { speciesId: "wildbit",    weight: 10, minLv: 4, maxLv: 6 },
  ],
};

// Keep legacy export for backwards compat
export const ENCOUNTER_TABLE_TOWN_GRASS = ENCOUNTER_TABLES.town;
