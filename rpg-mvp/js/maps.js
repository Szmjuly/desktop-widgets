export const TILE = {
  GRASS: 1,
  PATH: 2,
  WATER: 3,
  HOUSE: 4,
  ROOF: 5,
  DOOR: 6,
  FLOOR: 7,
  WALL: 8,
  EXIT: 9,
  LAB: 10,
  TALL_GRASS: 11,
  FENCE: 12,
  SAND: 13,
};

function idx(x, y, w){ return y * w + x; }

function fill(w, h, v){
  return Array.from({ length: w * h }, () => v);
}

function rect(tiles, w, x0, y0, rw, rh, v){
  for (let y = y0; y < y0 + rh; y++){
    for (let x = x0; x < x0 + rw; x++){
      tiles[idx(x,y,w)] = v;
    }
  }
}

function borderWalls(tiles, w, h, wallTile){
  for (let x = 0; x < w; x++){
    tiles[idx(x,0,w)] = wallTile;
    tiles[idx(x,h-1,w)] = wallTile;
  }
  for (let y = 0; y < h; y++){
    tiles[idx(0,y,w)] = wallTile;
    tiles[idx(w-1,y,w)] = wallTile;
  }
}

export function buildMaps(){
  const townW = 20, townH = 15;
  const t = fill(townW, townH, TILE.GRASS);

  // Town roads
  rect(t, townW, 0, 7, 20, 1, TILE.PATH);
  rect(t, townW, 10, 6, 1, 8, TILE.PATH);

  // Water pond (east side)
  rect(t, townW, 15, 9, 4, 3, TILE.WATER);

  // House (left of main street)
  rect(t, townW, 1, 8, 4, 4, TILE.HOUSE);
  rect(t, townW, 1, 8, 4, 1, TILE.ROOF);
  t[idx(2, 8, townW)] = TILE.DOOR;

  // Lab (right of south path)
  rect(t, townW, 11, 10, 5, 4, TILE.LAB);
  rect(t, townW, 11, 10, 5, 1, TILE.ROOF);
  rect(t, townW, 11, 11, 5, 2, TILE.HOUSE);
  t[idx(11, 13, townW)] = TILE.DOOR;

  // Tall grass patch at the top of town where encounters occur.
  // Use visible TALL_GRASS tiles so the player can see the encounter zone.
  rect(t, townW, 0, 0, 20, 5, TILE.TALL_GRASS);
  // Row 5 is normal grass (buffer)
  // Add a path opening into the tall grass.
  rect(t, townW, 9, 4, 3, 2, TILE.PATH);

  // Fence along bottom edge of town (row 14) except at path exits
  for (let x = 0; x < townW; x++) {
    if (x === 0 || x === 19) continue; // leave edges open for route warps
    if (t[idx(x, 14, townW)] === TILE.GRASS) t[idx(x, 14, townW)] = TILE.FENCE;
  }
  // Open path exits on left and right edges of main road
  t[idx(0, 7, townW)] = TILE.PATH;
  t[idx(19, 7, townW)] = TILE.PATH;

  const town = {
    id: "town",
    name: "Town",
    w: townW,
    h: townH,
    tiles: t,
    collides: new Set([TILE.WATER, TILE.HOUSE, TILE.LAB, TILE.ROOF, TILE.FENCE]),
    triggers: [
      { type: "warp", at: { x: 2, y: 8 }, to: { mapId: "house", x: 4, y: 7 } },
      { type: "warp", at: { x: 11, y: 13 }, to: { mapId: "lab", x: 5, y: 8 } },
      { type: "warp", at: { x: 0, y: 7 }, to: { mapId: "route_west", x: 19, y: 5 } },
      { type: "warp", at: { x: 19, y: 7 }, to: { mapId: "route_east", x: 0, y: 5 } },
      { type: "grassRegion", region: { x: 0, y: 0, w: 20, h: 5 } }
    ],
    actors: []
  };

  // Lab interior
  const labW = 11, labH = 9;
  const labTiles = fill(labW, labH, TILE.FLOOR);
  borderWalls(labTiles, labW, labH, TILE.WALL);

  // Place pedestals as floor highlights (still FLOOR tile), actor list will render them
  // Exit tile
  labTiles[idx(5,8,labW)] = TILE.EXIT;

  const lab = {
    id: "lab",
    name: "Lab",
    w: labW,
    h: labH,
    tiles: labTiles,
    collides: new Set([TILE.WALL]),
    triggers: [
      { type: "warp", at: { x: 5, y: 8 }, to: { mapId: "town", x: 10, y: 12 } }
    ],
    actors: [
      { type: "starter", id: "starter_1", at: { x: 3, y: 3 }, speciesId: "sproutle" },
      { type: "starter", id: "starter_2", at: { x: 5, y: 3 }, speciesId: "embercub" },
      { type: "starter", id: "starter_3", at: { x: 7, y: 3 }, speciesId: "aquaff" }
    ]
  };

  // House interior
  const houseW = 9, houseH = 8;
  const houseTiles = fill(houseW, houseH, TILE.FLOOR);
  borderWalls(houseTiles, houseW, houseH, TILE.WALL);
  houseTiles[idx(4,7,houseW)] = TILE.EXIT;

  const house = {
    id: "house",
    name: "House",
    w: houseW,
    h: houseH,
    tiles: houseTiles,
    collides: new Set([TILE.WALL]),
    triggers: [
      { type: "warp", at: { x: 4, y: 7 }, to: { mapId: "town", x: 2, y: 7 } }
    ],
    actors: [
      { type: "npc", id: "npc_1", at: { x: 4, y: 3 }, color: "#ffd27b",
        text: "Nice day, huh? Tall grass up north has some curious critters." }
    ]
  };

  // ── Route West ──
  const rwW = 20, rwH = 11;
  const rw = fill(rwW, rwH, TILE.GRASS);
  // Horizontal path through middle
  rect(rw, rwW, 0, 5, 20, 1, TILE.PATH);
  // Tall grass patches
  rect(rw, rwW, 2, 1, 6, 3, TILE.TALL_GRASS);
  rect(rw, rwW, 12, 2, 5, 3, TILE.TALL_GRASS);
  rect(rw, rwW, 4, 7, 7, 3, TILE.TALL_GRASS);
  // Water feature
  rect(rw, rwW, 15, 7, 3, 3, TILE.WATER);
  // Fences along top and bottom
  for (let x = 0; x < rwW; x++){
    rw[idx(x, 0, rwW)] = TILE.FENCE;
    rw[idx(x, 10, rwW)] = TILE.FENCE;
  }
  // Open path exits on left and right
  rw[idx(0, 5, rwW)] = TILE.PATH;
  rw[idx(19, 5, rwW)] = TILE.PATH;
  rw[idx(0, 0, rwW)] = TILE.GRASS;
  rw[idx(0, 10, rwW)] = TILE.GRASS;
  rw[idx(19, 0, rwW)] = TILE.GRASS;
  rw[idx(19, 10, rwW)] = TILE.GRASS;

  const route_west = {
    id: "route_west",
    name: "Route West",
    w: rwW,
    h: rwH,
    tiles: rw,
    collides: new Set([TILE.WATER, TILE.FENCE]),
    triggers: [
      { type: "warp", at: { x: 19, y: 5 }, to: { mapId: "town", x: 0, y: 7 } },
      { type: "grassRegion", region: { x: 2, y: 1, w: 6, h: 3 } },
      { type: "grassRegion", region: { x: 12, y: 2, w: 5, h: 3 } },
      { type: "grassRegion", region: { x: 4, y: 7, w: 7, h: 3 } },
    ],
    actors: [
      { type: "npc", id: "npc_rw1", at: { x: 10, y: 4 }, color: "#ffb347",
        text: "Watch out! The creatures here are tougher than in town." }
    ]
  };

  // ── Route East ──
  const reW = 20, reH = 11;
  const re = fill(reW, reH, TILE.GRASS);
  // Horizontal path
  rect(re, reW, 0, 5, 20, 1, TILE.PATH);
  // Sandy area
  rect(re, reW, 8, 7, 5, 3, TILE.SAND);
  // Tall grass patches
  rect(re, reW, 1, 1, 7, 3, TILE.TALL_GRASS);
  rect(re, reW, 10, 1, 6, 3, TILE.TALL_GRASS);
  rect(re, reW, 2, 7, 5, 3, TILE.TALL_GRASS);
  rect(re, reW, 14, 7, 5, 3, TILE.TALL_GRASS);
  // Water
  rect(re, reW, 17, 1, 2, 2, TILE.WATER);
  // Fences
  for (let x = 0; x < reW; x++){
    re[idx(x, 0, reW)] = TILE.FENCE;
    re[idx(x, 10, reW)] = TILE.FENCE;
  }
  re[idx(0, 0, reW)] = TILE.GRASS;
  re[idx(0, 10, reW)] = TILE.GRASS;
  re[idx(19, 0, reW)] = TILE.FENCE;
  re[idx(19, 10, reW)] = TILE.FENCE;
  re[idx(0, 5, reW)] = TILE.PATH;

  const route_east = {
    id: "route_east",
    name: "Route East",
    w: reW,
    h: reH,
    tiles: re,
    collides: new Set([TILE.WATER, TILE.FENCE]),
    triggers: [
      { type: "warp", at: { x: 0, y: 5 }, to: { mapId: "town", x: 19, y: 7 } },
      { type: "grassRegion", region: { x: 1, y: 1, w: 7, h: 3 } },
      { type: "grassRegion", region: { x: 10, y: 1, w: 6, h: 3 } },
      { type: "grassRegion", region: { x: 2, y: 7, w: 5, h: 3 } },
      { type: "grassRegion", region: { x: 14, y: 7, w: 5, h: 3 } },
    ],
    actors: [
      { type: "npc", id: "npc_re1", at: { x: 9, y: 4 }, color: "#c77dff",
        text: "The creatures east of town are stronger. Be prepared!" }
    ]
  };

  return { town, lab, house, route_west, route_east };
}

export function getTile(map, x, y){
  if (x < 0 || y < 0 || x >= map.w || y >= map.h) return null;
  return map.tiles[y * map.w + x];
}

export function isBlocked(map, x, y){
  const tile = getTile(map, x, y);
  if (tile == null) return true;
  return map.collides.has(tile);
}

export function findTriggerAt(map, x, y, type){
  return map.triggers.find(t => t.type === type && t.at && t.at.x === x && t.at.y === y) || null;
}

export function inGrassRegion(map, x, y){
  const regions = map.triggers.filter(t => t.type === "grassRegion");
  for (const r of regions){
    const g = r.region;
    if (x >= g.x && y >= g.y && x < g.x + g.w && y < g.y + g.h) return true;
  }
  return false;
}

export function adjacentPos(pos, facing){
  const d = { x: pos.x, y: pos.y };
  if (facing === "up") d.y -= 1;
  if (facing === "down") d.y += 1;
  if (facing === "left") d.x -= 1;
  if (facing === "right") d.x += 1;
  return d;
}

export function tileColor(tileId){
  switch (tileId){
    case TILE.GRASS: return "#1f6b2d";
    case TILE.PATH: return "#c8b47e";
    case TILE.WATER: return "#2f5fff";
    case TILE.HOUSE: return "#8b5a3c";
    case TILE.LAB: return "#5f6a8a";
    case TILE.ROOF: return "#3c2f2f";
    case TILE.DOOR: return "#e8ecff";
    case TILE.FLOOR: return "#2a2f4a";
    case TILE.WALL: return "#0e1020";
    case TILE.EXIT: return "#aab3d6";
    case TILE.TALL_GRASS: return "#0d5a1a";
    case TILE.FENCE: return "#6b5b3a";
    case TILE.SAND: return "#d4c090";
    default: return "#000";
  }
}
