export const SPECIES = {
  sproutle: {
    id: "sproutle",
    name: "Sproutle",
    baseStats: { hp: 45, atk: 49, def: 49, spd: 45 },
    moves: [{ id: "tackle", name: "Tackle", power: 40, accuracy: 1.0 }],
    color: "#5fbf6a"
  },
  embercub: {
    id: "embercub",
    name: "Embercub",
    baseStats: { hp: 39, atk: 52, def: 43, spd: 65 },
    moves: [{ id: "tackle", name: "Tackle", power: 40, accuracy: 1.0 }],
    color: "#ff8a4c"
  },
  aquaff: {
    id: "aquaff",
    name: "Aquaff",
    baseStats: { hp: 44, atk: 48, def: 65, spd: 43 },
    moves: [{ id: "tackle", name: "Tackle", power: 40, accuracy: 1.0 }],
    color: "#4cc0ff"
  },

  wildbit: {
    id: "wildbit",
    name: "Wildbit",
    baseStats: { hp: 35, atk: 40, def: 35, spd: 55 },
    moves: [{ id: "tackle", name: "Tackle", power: 35, accuracy: 1.0 }],
    color: "#d7d36b"
  },
  mosslug: {
    id: "mosslug",
    name: "Mosslug",
    baseStats: { hp: 50, atk: 38, def: 55, spd: 25 },
    moves: [{ id: "tackle", name: "Tackle", power: 30, accuracy: 1.0 }],
    color: "#7ad17a"
  },
  pebblit: {
    id: "pebblit",
    name: "Pebblit",
    baseStats: { hp: 40, atk: 55, def: 60, spd: 20 },
    moves: [{ id: "tackle", name: "Tackle", power: 35, accuracy: 1.0 }],
    color: "#b9b0a3"
  }
};

export const ENCOUNTER_TABLE_TOWN_GRASS = [
  { speciesId: "wildbit", weight: 50, minLv: 2, maxLv: 4 },
  { speciesId: "mosslug", weight: 30, minLv: 2, maxLv: 3 },
  { speciesId: "pebblit", weight: 20, minLv: 3, maxLv: 4 }
];

/** Rival's starter (type advantage: grass &lt; fire &lt; water &lt; grass) */
export function getRivalStarterSpeciesId(playerStarterId) {
  const map = { sproutle: "aquaff", embercub: "sproutle", aquaff: "embercub" };
  return map[playerStarterId] || "sproutle";
}
