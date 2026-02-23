// ── Element types ──
export const TYPES = {
  normal:  "normal",
  fire:    "fire",
  water:   "water",
  grass:   "grass",
  rock:    "rock",
  electric:"electric",
  poison:  "poison",
};

// ── Type effectiveness chart ──
// effectiveness[attacker][defender] → multiplier (default 1.0)
const _e = {};
function eff(atk, def, mult){ _e[atk] = _e[atk] || {}; _e[atk][def] = mult; }

eff("fire","grass",2);   eff("fire","water",0.5); eff("fire","rock",0.5); eff("fire","fire",0.5);
eff("water","fire",2);   eff("water","grass",0.5);eff("water","water",0.5);eff("water","rock",2);
eff("grass","water",2);  eff("grass","fire",0.5); eff("grass","grass",0.5);eff("grass","rock",2);
eff("rock","fire",2);    eff("rock","water",0.5); eff("rock","grass",0.5);
eff("electric","water",2);eff("electric","grass",0.5);eff("electric","rock",0.5);eff("electric","electric",0.5);
eff("poison","grass",2); eff("poison","rock",0.5);eff("poison","poison",0.5);

export function typeEffectiveness(moveType, defenderType){
  if (!_e[moveType]) return 1;
  return _e[moveType][defenderType] ?? 1;
}

// ── Status effect IDs ──
export const STATUS = {
  none:     null,
  burn:     "burn",
  paralyze: "paralyze",
  poison:   "poison",
  sleep:    "sleep",
  freeze:   "freeze",
};

// ── Move database ──
// category: "physical" | "special" | "status"
// effect: optional { type: "status", status, chance } or { type: "statmod", stat, stages, chance, target }
export const MOVES = {
  tackle: {
    id: "tackle", name: "Tackle", type: "normal", category: "physical",
    power: 40, accuracy: 100, pp: 35,
    effect: null,
  },
  scratch: {
    id: "scratch", name: "Scratch", type: "normal", category: "physical",
    power: 40, accuracy: 100, pp: 35,
    effect: null,
  },
  ember: {
    id: "ember", name: "Ember", type: "fire", category: "special",
    power: 40, accuracy: 100, pp: 25,
    effect: { type: "status", status: "burn", chance: 10 },
  },
  flame_burst: {
    id: "flame_burst", name: "Flame Burst", type: "fire", category: "special",
    power: 70, accuracy: 100, pp: 15,
    effect: null,
  },
  water_gun: {
    id: "water_gun", name: "Water Gun", type: "water", category: "special",
    power: 40, accuracy: 100, pp: 25,
    effect: null,
  },
  aqua_jet: {
    id: "aqua_jet", name: "Aqua Jet", type: "water", category: "physical",
    power: 40, accuracy: 100, pp: 20,
    effect: null,
  },
  bubble_beam: {
    id: "bubble_beam", name: "Bubble Beam", type: "water", category: "special",
    power: 65, accuracy: 100, pp: 20,
    effect: { type: "statmod", stat: "spd", stages: -1, chance: 10, target: "defender" },
  },
  vine_whip: {
    id: "vine_whip", name: "Vine Whip", type: "grass", category: "physical",
    power: 45, accuracy: 100, pp: 25,
    effect: null,
  },
  razor_leaf: {
    id: "razor_leaf", name: "Razor Leaf", type: "grass", category: "physical",
    power: 55, accuracy: 95, pp: 25,
    effect: null,  // high crit rate handled via flag
    highCrit: true,
  },
  seed_bomb: {
    id: "seed_bomb", name: "Seed Bomb", type: "grass", category: "physical",
    power: 80, accuracy: 100, pp: 15,
    effect: null,
  },
  rock_throw: {
    id: "rock_throw", name: "Rock Throw", type: "rock", category: "physical",
    power: 50, accuracy: 90, pp: 15,
    effect: null,
  },
  rock_slide: {
    id: "rock_slide", name: "Rock Slide", type: "rock", category: "physical",
    power: 75, accuracy: 90, pp: 10,
    effect: null,
  },
  thunder_shock: {
    id: "thunder_shock", name: "Thunder Shock", type: "electric", category: "special",
    power: 40, accuracy: 100, pp: 30,
    effect: { type: "status", status: "paralyze", chance: 10 },
  },
  spark: {
    id: "spark", name: "Spark", type: "electric", category: "physical",
    power: 65, accuracy: 100, pp: 20,
    effect: { type: "status", status: "paralyze", chance: 30 },
  },
  poison_sting: {
    id: "poison_sting", name: "Poison Sting", type: "poison", category: "physical",
    power: 15, accuracy: 100, pp: 35,
    effect: { type: "status", status: "poison", chance: 30 },
  },
  sludge: {
    id: "sludge", name: "Sludge", type: "poison", category: "special",
    power: 65, accuracy: 100, pp: 20,
    effect: { type: "status", status: "poison", chance: 30 },
  },
  bite: {
    id: "bite", name: "Bite", type: "normal", category: "physical",
    power: 60, accuracy: 100, pp: 25,
    effect: null,
  },
  quick_attack: {
    id: "quick_attack", name: "Quick Attack", type: "normal", category: "physical",
    power: 40, accuracy: 100, pp: 30,
    priority: 1,
    effect: null,
  },
  growl: {
    id: "growl", name: "Growl", type: "normal", category: "status",
    power: 0, accuracy: 100, pp: 40,
    effect: { type: "statmod", stat: "atk", stages: -1, chance: 100, target: "defender" },
  },
  harden: {
    id: "harden", name: "Harden", type: "normal", category: "status",
    power: 0, accuracy: 100, pp: 30,
    effect: { type: "statmod", stat: "def", stages: 1, chance: 100, target: "self" },
  },
  leer: {
    id: "leer", name: "Leer", type: "normal", category: "status",
    power: 0, accuracy: 100, pp: 30,
    effect: { type: "statmod", stat: "def", stages: -1, chance: 100, target: "defender" },
  },
  sleep_powder: {
    id: "sleep_powder", name: "Sleep Powder", type: "grass", category: "status",
    power: 0, accuracy: 75, pp: 15,
    effect: { type: "status", status: "sleep", chance: 100 },
  },
};
