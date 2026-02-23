import { TILE } from "./maps.js";
import { ENCOUNTER_TABLES } from "./data_species.js";

// ── Seeded RNG ──
function mulberry32(seed) {
  let s = seed | 0;
  return function () {
    s = (s + 0x6d2b79f5) | 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function idx(x, y, w) { return y * w + x; }
function fill(w, h, v) { return Array.from({ length: w * h }, () => v); }
function rect(tiles, w, x0, y0, rw, rh, v) {
  for (let y = y0; y < y0 + rh; y++)
    for (let x = x0; x < x0 + rw; x++)
      tiles[idx(x, y, w)] = v;
}
function borderWalls(tiles, w, h, wallTile) {
  for (let x = 0; x < w; x++) { tiles[idx(x, 0, w)] = wallTile; tiles[idx(x, h - 1, w)] = wallTile; }
  for (let y = 0; y < h; y++) { tiles[idx(0, y, w)] = wallTile; tiles[idx(w - 1, y, w)] = wallTile; }
}

// ── Route generator ──
function generateRoute(rng, id, name, exitLeft, exitRight, exitTop, exitBottom, encounterTableId) {
  const w = 20, h = 11;
  const tiles = fill(w, h, TILE.GRASS);

  // Horizontal path
  rect(tiles, w, 0, 5, 20, 1, TILE.PATH);

  // Random tall grass patches (2-4)
  const numPatches = 2 + Math.floor(rng() * 3);
  const grassRegions = [];
  for (let i = 0; i < numPatches; i++) {
    const gx = 1 + Math.floor(rng() * 14);
    const gy = rng() < 0.5 ? (1 + Math.floor(rng() * 3)) : (7 + Math.floor(rng() * 2));
    const gw = 3 + Math.floor(rng() * 5);
    const gh = 2 + Math.floor(rng() * 2);
    const clampedW = Math.min(gw, w - gx - 1);
    const clampedH = Math.min(gh, h - gy - 1);
    if (clampedW > 0 && clampedH > 0) {
      rect(tiles, w, gx, gy, clampedW, clampedH, TILE.TALL_GRASS);
      grassRegions.push({ type: "grassRegion", region: { x: gx, y: gy, w: clampedW, h: clampedH } });
    }
  }

  // Random water (0-2 ponds)
  const numPonds = Math.floor(rng() * 3);
  for (let i = 0; i < numPonds; i++) {
    const px = 2 + Math.floor(rng() * 15);
    const py = rng() < 0.5 ? (1 + Math.floor(rng() * 3)) : (7 + Math.floor(rng() * 2));
    const pw = 2 + Math.floor(rng() * 2);
    const ph = 1 + Math.floor(rng() * 2);
    rect(tiles, w, Math.min(px, w - pw - 1), py, pw, ph, TILE.WATER);
  }

  // Random sand patches
  if (rng() < 0.3) {
    const sx = 3 + Math.floor(rng() * 12);
    const sy = 7 + Math.floor(rng() * 2);
    rect(tiles, w, sx, sy, 3 + Math.floor(rng() * 3), 2, TILE.SAND);
  }

  // Fences top/bottom
  for (let x = 0; x < w; x++) {
    tiles[idx(x, 0, w)] = TILE.FENCE;
    tiles[idx(x, h - 1, w)] = TILE.FENCE;
  }

  // Vertical path if top/bottom exits
  if (exitTop || exitBottom) {
    const vx = 10;
    for (let y = 0; y < h; y++) tiles[idx(vx, y, w)] = TILE.PATH;
  }

  const triggers = [...grassRegions];
  const actors = [];

  // Exits
  if (exitLeft) {
    tiles[idx(0, 5, w)] = TILE.PATH;
    tiles[idx(0, 0, w)] = TILE.GRASS;
    tiles[idx(0, h - 1, w)] = TILE.GRASS;
    triggers.push({ type: "warp", at: { x: 0, y: 5 }, to: exitLeft });
  }
  if (exitRight) {
    tiles[idx(w - 1, 5, w)] = TILE.PATH;
    tiles[idx(w - 1, 0, w)] = TILE.GRASS;
    tiles[idx(w - 1, h - 1, w)] = TILE.GRASS;
    triggers.push({ type: "warp", at: { x: w - 1, y: 5 }, to: exitRight });
  }
  if (exitTop) {
    tiles[idx(10, 0, w)] = TILE.PATH;
    triggers.push({ type: "warp", at: { x: 10, y: 0 }, to: exitTop });
  }
  if (exitBottom) {
    tiles[idx(10, h - 1, w)] = TILE.PATH;
    triggers.push({ type: "warp", at: { x: 10, y: h - 1 }, to: exitBottom });
  }

  // Random NPC
  if (rng() < 0.6) {
    const npcTexts = [
      "Watch out! The creatures here are tougher than in town.",
      "I've been training my creatures all day!",
      "Did you know type matchups matter a lot in battles?",
      "There's a shop back in town if you need supplies.",
      "I heard there's rare creatures deeper in the routes.",
    ];
    actors.push({
      type: "npc", id: `npc_${id}`, at: { x: 5 + Math.floor(rng() * 10), y: 4 },
      color: `hsl(${Math.floor(rng() * 360)},60%,65%)`,
      text: npcTexts[Math.floor(rng() * npcTexts.length)],
    });
  }

  return {
    id, name, w, h, tiles,
    collides: new Set([TILE.WATER, TILE.FENCE]),
    triggers, actors,
    encounterTableId: encounterTableId || id,
  };
}

// ── Small town generator ──
function generateSmallTown(rng, id, name, exits) {
  const w = 16, h = 12;
  const tiles = fill(w, h, TILE.GRASS);

  // Central path cross
  rect(tiles, w, 0, 5, 16, 1, TILE.PATH);
  rect(tiles, w, 8, 0, 1, 12, TILE.PATH);

  // A few buildings
  rect(tiles, w, 2, 2, 4, 3, TILE.HOUSE);
  rect(tiles, w, 2, 2, 4, 1, TILE.ROOF);

  rect(tiles, w, 11, 2, 4, 3, TILE.HOUSE);
  rect(tiles, w, 11, 2, 4, 1, TILE.ROOF);

  // Water feature
  if (rng() < 0.5) {
    rect(tiles, w, 2 + Math.floor(rng() * 4), 8, 3, 2, TILE.WATER);
  }

  const triggers = [];
  const actors = [
    { type: "npc", id: `npc_${id}_1`, at: { x: 4, y: 6 },
      color: `hsl(${Math.floor(rng() * 360)},55%,65%)`,
      text: `Welcome to ${name}! Rest up before heading out.` },
    { type: "npc", id: `npc_${id}_heal`, at: { x: 12, y: 6 },
      color: "#ff9ec7", subtype: "healer",
      text: "Let me heal your creatures! ... All healed up!" },
  ];

  // Shop keeper
  actors.push({
    type: "npc", id: `npc_${id}_shop`, at: { x: 6, y: 6 },
    color: "#7bffb5", subtype: "shop",
    text: "Welcome to my shop!",
  });

  // Exits
  if (exits.left) {
    tiles[idx(0, 5, w)] = TILE.PATH;
    triggers.push({ type: "warp", at: { x: 0, y: 5 }, to: exits.left });
  }
  if (exits.right) {
    tiles[idx(w - 1, 5, w)] = TILE.PATH;
    triggers.push({ type: "warp", at: { x: w - 1, y: 5 }, to: exits.right });
  }
  if (exits.top) {
    tiles[idx(8, 0, w)] = TILE.PATH;
    triggers.push({ type: "warp", at: { x: 8, y: 0 }, to: exits.top });
  }
  if (exits.bottom) {
    tiles[idx(8, h - 1, w)] = TILE.PATH;
    triggers.push({ type: "warp", at: { x: 8, y: h - 1 }, to: exits.bottom });
  }

  return {
    id, name, w, h, tiles,
    collides: new Set([TILE.WATER, TILE.HOUSE, TILE.ROOF]),
    triggers, actors,
  };
}

// ── Encounter table for procedural routes ──
const PROC_ENCOUNTER_POOLS = {
  easy: [
    { speciesId: "wildbit", weight: 40, minLv: 3, maxLv: 5 },
    { speciesId: "mosslug", weight: 30, minLv: 3, maxLv: 5 },
    { speciesId: "thornpup", weight: 20, minLv: 3, maxLv: 5 },
    { speciesId: "pebblit", weight: 10, minLv: 4, maxLv: 6 },
  ],
  medium: [
    { speciesId: "zapplet", weight: 25, minLv: 5, maxLv: 7 },
    { speciesId: "sootfox", weight: 25, minLv: 5, maxLv: 7 },
    { speciesId: "venomite", weight: 20, minLv: 5, maxLv: 8 },
    { speciesId: "thornpup", weight: 15, minLv: 5, maxLv: 7 },
    { speciesId: "coralshell", weight: 15, minLv: 5, maxLv: 8 },
  ],
  hard: [
    { speciesId: "sootfox", weight: 20, minLv: 7, maxLv: 10 },
    { speciesId: "venomite", weight: 20, minLv: 7, maxLv: 10 },
    { speciesId: "coralshell", weight: 20, minLv: 7, maxLv: 10 },
    { speciesId: "zapplet", weight: 20, minLv: 7, maxLv: 10 },
    { speciesId: "pebblit", weight: 20, minLv: 8, maxLv: 11 },
  ],
};

/**
 * Generate the full procedural world.
 * Returns { maps, encounterTables, seed }.
 * The home town, lab, and house are always the same.
 * Routes and secondary towns are procedurally generated.
 */
export function generateWorld(seed) {
  const rng = mulberry32(seed);
  const maps = {};
  const encounterTables = { ...ENCOUNTER_TABLES };

  // ── Home Town (always the same layout) ──
  // We import the static town from buildMaps — but to keep it self-contained,
  // we rebuild it here with the same logic.
  const townW = 20, townH = 15;
  const t = fill(townW, townH, TILE.GRASS);
  rect(t, townW, 0, 7, 20, 1, TILE.PATH);
  rect(t, townW, 10, 6, 1, 8, TILE.PATH);
  rect(t, townW, 15, 9, 4, 3, TILE.WATER);
  rect(t, townW, 1, 8, 4, 4, TILE.HOUSE);
  rect(t, townW, 1, 8, 4, 1, TILE.ROOF);
  t[idx(2, 8, townW)] = TILE.DOOR;
  rect(t, townW, 11, 10, 5, 4, TILE.LAB);
  rect(t, townW, 11, 10, 5, 1, TILE.ROOF);
  rect(t, townW, 11, 11, 5, 2, TILE.HOUSE);
  t[idx(11, 13, townW)] = TILE.DOOR;
  rect(t, townW, 0, 0, 20, 5, TILE.TALL_GRASS);
  rect(t, townW, 9, 4, 3, 2, TILE.PATH);
  for (let x = 0; x < townW; x++) {
    if (x === 0 || x === 19) continue;
    if (t[idx(x, 14, townW)] === TILE.GRASS) t[idx(x, 14, townW)] = TILE.FENCE;
  }
  t[idx(0, 7, townW)] = TILE.PATH;
  t[idx(19, 7, townW)] = TILE.PATH;

  // Shop building in town (south-west area)
  rect(t, townW, 6, 10, 4, 3, TILE.HOUSE);
  rect(t, townW, 6, 10, 4, 1, TILE.ROOF);
  t[idx(7, 12, townW)] = TILE.DOOR;

  maps.town = {
    id: "town", name: "Home Town", w: townW, h: townH, tiles: t,
    collides: new Set([TILE.WATER, TILE.HOUSE, TILE.LAB, TILE.ROOF, TILE.FENCE]),
    triggers: [
      { type: "warp", at: { x: 2, y: 8 }, to: { mapId: "house", x: 4, y: 7 } },
      { type: "warp", at: { x: 11, y: 13 }, to: { mapId: "lab", x: 5, y: 8 } },
      { type: "warp", at: { x: 7, y: 12 }, to: { mapId: "shop", x: 4, y: 7 } },
      { type: "warp", at: { x: 0, y: 7 }, to: { mapId: "rw1", x: 19, y: 5 } },
      { type: "warp", at: { x: 19, y: 7 }, to: { mapId: "re1", x: 0, y: 5 } },
      { type: "grassRegion", region: { x: 0, y: 0, w: 20, h: 5 } },
    ],
    actors: [],
  };

  // ── Lab ──
  const labW = 11, labH = 9;
  const labTiles = fill(labW, labH, TILE.FLOOR);
  borderWalls(labTiles, labW, labH, TILE.WALL);
  labTiles[idx(5, 8, labW)] = TILE.EXIT;
  maps.lab = {
    id: "lab", name: "Lab", w: labW, h: labH, tiles: labTiles,
    collides: new Set([TILE.WALL]),
    triggers: [{ type: "warp", at: { x: 5, y: 8 }, to: { mapId: "town", x: 10, y: 12 } }],
    actors: [
      { type: "starter", id: "starter_1", at: { x: 3, y: 3 }, speciesId: "sproutle" },
      { type: "starter", id: "starter_2", at: { x: 5, y: 3 }, speciesId: "embercub" },
      { type: "starter", id: "starter_3", at: { x: 7, y: 3 }, speciesId: "aquaff" },
    ],
  };

  // ── House ──
  const houseW = 9, houseH = 8;
  const houseTiles = fill(houseW, houseH, TILE.FLOOR);
  borderWalls(houseTiles, houseW, houseH, TILE.WALL);
  houseTiles[idx(4, 7, houseW)] = TILE.EXIT;
  maps.house = {
    id: "house", name: "House", w: houseW, h: houseH, tiles: houseTiles,
    collides: new Set([TILE.WALL]),
    triggers: [{ type: "warp", at: { x: 4, y: 7 }, to: { mapId: "town", x: 2, y: 7 } }],
    actors: [
      { type: "npc", id: "npc_mom", at: { x: 4, y: 3 }, color: "#ffd27b",
        text: "Be careful out there! The professor left creatures for you at the lab." },
    ],
  };

  // ── Shop interior ──
  const shopW = 9, shopH = 8;
  const shopTiles = fill(shopW, shopH, TILE.FLOOR);
  borderWalls(shopTiles, shopW, shopH, TILE.WALL);
  shopTiles[idx(4, 7, shopW)] = TILE.EXIT;
  maps.shop = {
    id: "shop", name: "Shop", w: shopW, h: shopH, tiles: shopTiles,
    collides: new Set([TILE.WALL]),
    triggers: [{ type: "warp", at: { x: 4, y: 7 }, to: { mapId: "town", x: 7, y: 11 } }],
    actors: [
      { type: "npc", id: "npc_shopkeep", at: { x: 4, y: 2 }, color: "#7bffb5",
        subtype: "shop", text: "Welcome! What would you like to buy?" },
    ],
  };

  // ── Procedural routes ──
  // West branch: rw1 → rw2_n (north) / rw2_s (south) → west_town_n / west_town_s
  // East branch: re1 → re2_n (north) / re2_s (south) → east_town_n / east_town_s

  // Route West 1
  maps.rw1 = generateRoute(rng, "rw1", "Route W1",
    null, // left exit — dead end or further
    { mapId: "town", x: 0, y: 7 }, // right → town
    { mapId: "rw2_n", x: 10, y: 10 }, // top → north branch
    { mapId: "rw2_s", x: 10, y: 0 }, // bottom → south branch
    "route_west"
  );
  // Fix: left exit goes nowhere, set it to the left edge
  maps.rw1.triggers = maps.rw1.triggers.filter(tr => !(tr.type === "warp" && tr.at.x === 0));
  maps.rw1.tiles[idx(0, 5, 20)] = TILE.FENCE;

  encounterTables.rw1 = PROC_ENCOUNTER_POOLS.easy;

  // Route West 2 North
  maps.rw2_n = generateRoute(rng, "rw2_n", "Route W2 North",
    null, null,
    { mapId: "west_town_n", x: 8, y: 11 },
    { mapId: "rw1", x: 10, y: 0 },
    null
  );
  encounterTables.rw2_n = PROC_ENCOUNTER_POOLS.medium;

  // Route West 2 South
  maps.rw2_s = generateRoute(rng, "rw2_s", "Route W2 South",
    null, null,
    { mapId: "rw1", x: 10, y: 10 },
    { mapId: "west_town_s", x: 8, y: 0 },
    null
  );
  encounterTables.rw2_s = PROC_ENCOUNTER_POOLS.medium;

  // West Town North
  maps.west_town_n = generateSmallTown(rng, "west_town_n", "Pinegrove", {
    bottom: { mapId: "rw2_n", x: 10, y: 0 },
  });

  // West Town South
  maps.west_town_s = generateSmallTown(rng, "west_town_s", "Dusthaven", {
    top: { mapId: "rw2_s", x: 10, y: 10 },
  });

  // Route East 1
  maps.re1 = generateRoute(rng, "re1", "Route E1",
    { mapId: "town", x: 19, y: 7 }, // left → town
    null, // right — dead end
    { mapId: "re2_n", x: 10, y: 10 },
    { mapId: "re2_s", x: 10, y: 0 },
    "route_east"
  );
  maps.re1.triggers = maps.re1.triggers.filter(tr => !(tr.type === "warp" && tr.at.x === 19));
  maps.re1.tiles[idx(19, 5, 20)] = TILE.FENCE;

  encounterTables.re1 = PROC_ENCOUNTER_POOLS.easy;

  // Route East 2 North
  maps.re2_n = generateRoute(rng, "re2_n", "Route E2 North",
    null, null,
    { mapId: "east_town_n", x: 8, y: 11 },
    { mapId: "re1", x: 10, y: 0 },
    null
  );
  encounterTables.re2_n = PROC_ENCOUNTER_POOLS.hard;

  // Route East 2 South
  maps.re2_s = generateRoute(rng, "re2_s", "Route E2 South",
    null, null,
    { mapId: "re1", x: 10, y: 10 },
    { mapId: "east_town_s", x: 8, y: 0 },
    null
  );
  encounterTables.re2_s = PROC_ENCOUNTER_POOLS.hard;

  // East Town North
  maps.east_town_n = generateSmallTown(rng, "east_town_n", "Coralport", {
    bottom: { mapId: "re2_n", x: 10, y: 0 },
  });

  // East Town South
  maps.east_town_s = generateSmallTown(rng, "east_town_s", "Ashvale", {
    top: { mapId: "re2_s", x: 10, y: 10 },
  });

  return { maps, encounterTables, seed };
}
