import { CFG } from "./config.js";

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
  LAB: 10
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

  // —— Town: clear path layout ——
  // Main street (horizontal): one row y=7, full width — left/right lead to warps
  rect(t, townW, 0, 7, 20, 1, TILE.PATH);
  // North path: x=10 from y=0 to y=7 — walk north on path to Route 1 warp at (10,0)
  rect(t, townW, 10, 0, 1, 8, TILE.PATH);
  // South path: x=10 from y=8 to y=13 — leads down to lab door (lab is beside path, not on it)
  rect(t, townW, 10, 8, 1, 6, TILE.PATH);

  // Water pond (east side)
  rect(t, townW, 15, 9, 4, 3, TILE.WATER);

  // House (left of main street): door at (2,8), path (2,7) is main street
  rect(t, townW, 1, 8, 4, 4, TILE.HOUSE);
  rect(t, townW, 1, 8, 4, 1, TILE.ROOF);
  t[idx(2, 8, townW)] = TILE.DOOR;

  // Lab (right of south path): path runs along left side; door at (11,13), path (10,13) in front
  rect(t, townW, 11, 10, 5, 4, TILE.LAB);
  rect(t, townW, 11, 10, 5, 1, TILE.ROOF);
  rect(t, townW, 11, 11, 5, 2, TILE.HOUSE);
  t[idx(11, 13, townW)] = TILE.DOOR;

  const town = {
    id: "town",
    name: "Town",
    w: townW,
    h: townH,
    tiles: t,
    collides: new Set([TILE.WATER, TILE.HOUSE, TILE.LAB, TILE.ROOF]),
    triggers: [
      { type: "warp", at: { x: 10, y: 0 }, to: { mapId: "route1", x: 5, y: 10 } },
      { type: "warp", at: { x: 0, y: 7 }, to: { mapId: "west_gate", x: 8, y: 5 } },
      { type: "warp", at: { x: 19, y: 7 }, to: { mapId: "east_gate", x: 2, y: 5 } },
      { type: "warp", at: { x: 2, y: 8 }, to: { mapId: "house", x: 4, y: 7 } },
      { type: "warp", at: { x: 11, y: 13 }, to: { mapId: "lab", x: 5, y: 8 } },
      { type: "grassRegion", region: { x: 0, y: 0, w: 20, h: 6 } },
      { type: "grassRegion", region: { x: 0, y: 9, w: 20, h: 6 } }
    ],
    actors: [
      { type: "rival", id: "rival_1", at: { x: 8, y: 7 }, color: "#ff6b8a",
        text: "Hey! I heard you got a creature from the lab. Let's battle!" }
    ]
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

  // Route 1 — north of town (walk up from town grass to enter)
  const routeW = 11, routeH = 12;
  const routeTiles = fill(routeW, routeH, TILE.GRASS);
  rect(routeTiles, routeW, 4, 0, 3, routeH, TILE.PATH);
  routeTiles[idx(5, routeH - 1, routeW)] = TILE.EXIT;

  const route1 = {
    id: "route1",
    name: "Route 1",
    w: routeW,
    h: routeH,
    tiles: routeTiles,
    collides: new Set([TILE.WATER]),
    triggers: [
      { type: "warp", at: { x: 5, y: routeH - 1 }, to: { mapId: "town", x: 10, y: 1 } },
      { type: "grassRegion", region: { x: 0, y: 0, w: routeW, h: routeH } }
    ],
    actors: [
      { type: "npc", id: "route_npc_1", at: { x: 5, y: 4 }, color: "#aab3d6",
        text: "The town is south. Wild creatures hide in the grass!" }
    ]
  };

  // West Gate — left end of main street
  const westW = 9, westH = 7;
  const westTiles = fill(westW, westH, TILE.GRASS);
  rect(westTiles, westW, 0, 3, westW, 1, TILE.PATH);
  westTiles[idx(westW - 1, 3, westW)] = TILE.EXIT;
  const westGate = {
    id: "west_gate",
    name: "West Gate",
    w: westW,
    h: westH,
    tiles: westTiles,
    collides: new Set([TILE.WATER]),
    triggers: [
      { type: "warp", at: { x: westW - 1, y: 3 }, to: { mapId: "town", x: 1, y: 7 } }
    ],
    actors: [
      { type: "npc", id: "west_1", at: { x: 4, y: 3 }, color: "#c8b47e",
        text: "The road east leads back to town." }
    ]
  };

  // East Gate — right end of main street
  const eastW = 5, eastH = 7;
  const eastTiles = fill(eastW, eastH, TILE.GRASS);
  rect(eastTiles, eastW, 0, 3, eastW, 1, TILE.PATH);
  eastTiles[idx(0, 3, eastW)] = TILE.EXIT;
  const eastGate = {
    id: "east_gate",
    name: "East Gate",
    w: eastW,
    h: eastH,
    tiles: eastTiles,
    collides: new Set([TILE.WATER]),
    triggers: [
      { type: "warp", at: { x: 0, y: 3 }, to: { mapId: "town", x: 18, y: 7 } }
    ],
    actors: [
      { type: "npc", id: "east_1", at: { x: 2, y: 3 }, color: "#c8b47e",
        text: "Head west to return to town." }
    ]
  };

  return { town, lab, house, route1, westGate, eastGate };
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
    default: return "#000";
  }
}
