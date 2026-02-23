import { SPECIES } from "./data_species.js";
import { MOVES, typeEffectiveness } from "./data_moves.js";
import { clamp, chance, randInt } from "./utils.js";
import { consumeItem, addCreatureToCollections } from "./models.js";
import { saveGame } from "./save.js";

// ── Helpers ──

function creatureName(c){
  const sp = SPECIES[c.speciesId];
  return c.nickname || sp.name;
}

function stageMult(stage){
  if (stage >= 0) return (2 + stage) / 2;
  return 2 / (2 - stage);
}

function effectiveStat(creature, stat){
  const base = creature.stats[stat] || 0;
  const stage = creature.statStages?.[stat] ?? 0;
  return Math.max(1, Math.floor(base * stageMult(stage)));
}

function computeDamage(attacker, defender, move, isCrit){
  const L = attacker.level;
  const isPhysical = move.category === "physical";
  const atk = effectiveStat(attacker, isPhysical ? "atk" : "atk");
  const def = effectiveStat(defender, isPhysical ? "def" : "def");
  const power = move.power;

  const base = Math.floor((((2 * L) / 5 + 2) * power * (atk / Math.max(1, def))) / 50) + 2;

  // Type effectiveness
  const defType = SPECIES[defender.speciesId]?.type || "normal";
  const eff = typeEffectiveness(move.type, defType);

  // STAB (Same Type Attack Bonus)
  const atkType = SPECIES[attacker.speciesId]?.type || "normal";
  const stab = (move.type === atkType) ? 1.5 : 1.0;

  // Critical hit
  const critMult = isCrit ? 1.5 : 1.0;

  // Random factor
  const rand = 0.85 + Math.random() * 0.15;

  // Burn halves physical damage
  const burnPenalty = (attacker.status === "burn" && isPhysical) ? 0.5 : 1.0;

  return Math.max(1, Math.floor(base * eff * stab * critMult * rand * burnPenalty));
}

function applyDamage(target, dmg){
  target.hp = Math.max(0, target.hp - dmg);
  if (target.hp <= 0){
    target.fainted = true;
  }
}

function captureChance(wild, bonus){
  const hpFactor = (wild.maxHp - wild.hp) / Math.max(1, wild.maxHp);
  const raw = 0.10 + hpFactor * 0.70 * bonus;
  return clamp(raw, 0.05, 0.95);
}

function rollCrit(move){
  const rate = move.highCrit ? 0.125 : (1 / 24);
  return Math.random() < rate;
}

function rollAccuracy(move){
  return randInt(1, 100) <= move.accuracy;
}

// ── Status effect helpers ──

function statusLabel(status){
  const map = { burn: "BRN", paralyze: "PAR", poison: "PSN", sleep: "SLP", freeze: "FRZ" };
  return map[status] || "";
}

function canActWithStatus(creature, log){
  const name = creatureName(creature);
  if (creature.status === "sleep"){
    if (creature.sleepTurns > 0){
      creature.sleepTurns--;
      log(`${name} is fast asleep.`);
      return false;
    }
    creature.status = null;
    log(`${name} woke up!`);
    return true;
  }
  if (creature.status === "freeze"){
    if (Math.random() < 0.20){
      creature.status = null;
      log(`${name} thawed out!`);
      return true;
    }
    log(`${name} is frozen solid!`);
    return false;
  }
  if (creature.status === "paralyze"){
    if (Math.random() < 0.25){
      log(`${name} is paralyzed! It can't move!`);
      return false;
    }
  }
  return true;
}

function applyEndOfTurnStatus(creature, log){
  const name = creatureName(creature);
  if (creature.status === "burn"){
    const dmg = Math.max(1, Math.floor(creature.maxHp / 16));
    creature.hp = Math.max(0, creature.hp - dmg);
    log(`${name} is hurt by its burn! (-${dmg})`);
    if (creature.hp <= 0) creature.fainted = true;
  }
  if (creature.status === "poison"){
    const dmg = Math.max(1, Math.floor(creature.maxHp / 8));
    creature.hp = Math.max(0, creature.hp - dmg);
    log(`${name} is hurt by poison! (-${dmg})`);
    if (creature.hp <= 0) creature.fainted = true;
  }
}

function tryInflictStatus(target, statusId, chancePercent, log){
  if (target.status) return false; // already has a status
  if (target.fainted) return false;
  if (randInt(1, 100) > chancePercent) return false;
  target.status = statusId;
  if (statusId === "sleep") target.sleepTurns = randInt(1, 3);
  const name = creatureName(target);
  const labels = { burn: "was burned!", paralyze: "was paralyzed!", poison: "was poisoned!", sleep: "fell asleep!", freeze: "was frozen!" };
  log(`${name} ${labels[statusId] || "was afflicted!"}`);
  return true;
}

function applyStatMod(target, stat, stages, log){
  if (!target.statStages) target.statStages = { atk: 0, def: 0, spd: 0 };
  const old = target.statStages[stat] ?? 0;
  const clamped = clamp(old + stages, -6, 6);
  if (clamped === old){
    log(`${creatureName(target)}'s ${stat} won't go any ${stages > 0 ? "higher" : "lower"}!`);
    return;
  }
  target.statStages[stat] = clamped;
  const word = stages > 0 ? "rose" : "fell";
  const amt = Math.abs(stages) > 1 ? " sharply" : "";
  log(`${creatureName(target)}'s ${stat} ${word}${amt}!`);
}

// ── Battle class ──

export class Battle {
  constructor(game){
    this.game = game;
    this.active = false;
    this.wild = null;
    this.playerC = null;
    this.locked = false;
    this.moveSelectOpen = false;
  }

  start(opponentCreature, ownerLabel){
    this.active = true;
    this.locked = false;
    this.moveSelectOpen = false;
    this.wild = opponentCreature;
    this.ownerLabel = ownerLabel || "Wild";
    this.playerC = this.game.player.party[0] || null;

    // Reset stat stages for battle
    if (this.playerC) this.playerC.statStages = { atk: 0, def: 0, spd: 0 };
    if (this.wild) this.wild.statStages = { atk: 0, def: 0, spd: 0 };

    this.game.ui.showBattle();
    this.game.ui.battleLogClear();
    this.game.ui.hideMoveSelect();

    const wName = creatureName(this.wild);
    const pName = this.playerC ? creatureName(this.playerC) : "No Party Creature";
    const opponentLabel = `${this.ownerLabel} ${wName} Lv${this.wild.level}`;

    this.game.ui.setBattleHeader(
      `${pName} Lv${this.playerC?.level ?? "?"}`,
      opponentLabel
    );
    this.syncBars();
    this.game.ui.battleLogPush(`${this.ownerLabel} ${wName} appeared!`);

    if (!this.playerC){
      this.game.ui.battleLogPush("You have no creatures. You run away!");
      this.endToMap();
      return;
    }
  }

  syncBars(){
    if (!this.playerC || !this.wild) return;
    this.game.ui.setHpBars(this.playerC.hp, this.playerC.maxHp, this.wild.hp, this.wild.maxHp);
    this.game.ui.setStatusIndicators(this.playerC.status, this.wild.status);
  }

  setLocked(v){
    this.locked = v;
    this.game.ui.setBattleButtonsEnabled(!v);
  }

  // ── Fight button opens move selection ──
  playerFight(){
    if (!this.active || this.locked) return;
    if (this.moveSelectOpen){
      this.closeMoveSelect();
      return;
    }
    this.openMoveSelect();
  }

  openMoveSelect(){
    this.moveSelectOpen = true;
    this.game.ui.showMoveSelect(this.playerC, (moveSlot) => {
      this.closeMoveSelect();
      this.executePlayerMove(moveSlot);
    }, () => {
      this.closeMoveSelect();
    });
  }

  closeMoveSelect(){
    this.moveSelectOpen = false;
    this.game.ui.hideMoveSelect();
  }

  executePlayerMove(moveSlot){
    if (!this.active || this.locked) return;
    this.setLocked(true);

    const moveData = MOVES[moveSlot.moveId];
    if (!moveData){
      this.game.ui.battleLogPush("Unknown move!");
      this.finishSoon(() => this.setLocked(false));
      return;
    }

    // Deduct PP
    if (moveSlot.pp <= 0){
      this.game.ui.battleLogPush("No PP left for this move!");
      this.finishSoon(() => this.setLocked(false));
      return;
    }
    moveSlot.pp--;

    const pName = creatureName(this.playerC);
    const wName = creatureName(this.wild);

    // Status check
    const logFn = (msg) => this.game.ui.battleLogPush(msg);
    if (!canActWithStatus(this.playerC, logFn)){
      this.syncBars();
      if (this.playerC.fainted){
        this.handlePlayerFaint();
        return;
      }
      this.finishSoon(() => this.afterPlayerTurn());
      return;
    }

    this.game.ui.battleLogPush(`${pName} used ${moveData.name}!`);

    // Status moves
    if (moveData.category === "status"){
      if (!rollAccuracy(moveData)){
        this.game.ui.battleLogPush("But it missed!");
      } else {
        this.applyMoveEffect(moveData, this.playerC, this.wild, logFn);
      }
      this.syncBars();
      this.finishSoon(() => this.afterPlayerTurn());
      return;
    }

    // Accuracy check
    if (!rollAccuracy(moveData)){
      this.game.ui.battleLogPush("But it missed!");
      this.syncBars();
      this.finishSoon(() => this.afterPlayerTurn());
      return;
    }

    // Crit check
    const isCrit = rollCrit(moveData);
    const dmg = computeDamage(this.playerC, this.wild, moveData, isCrit);

    // Type effectiveness message
    const defType = SPECIES[this.wild.speciesId]?.type || "normal";
    const eff = typeEffectiveness(moveData.type, defType);
    if (eff > 1) this.game.ui.battleLogPush("It's super effective!");
    else if (eff < 1 && eff > 0) this.game.ui.battleLogPush("It's not very effective...");

    if (isCrit) this.game.ui.battleLogPush("A critical hit!");

    this.game.ui.battleLogPush(`It dealt ${dmg} damage to ${wName}.`);
    applyDamage(this.wild, dmg);
    this.syncBars();

    // Secondary effect
    if (moveData.effect && !this.wild.fainted){
      this.applyMoveEffect(moveData, this.playerC, this.wild, logFn);
      this.syncBars();
    }

    if (this.wild.fainted){
      const prize = 10 + this.wild.level * 8;
      this.game.player.money += prize;
      this.game.ui.battleLogPush(`${wName} fainted. You win! (+$${prize})`);
      this.finishSoon(() => this.endToMap());
      return;
    }

    this.finishSoon(() => this.afterPlayerTurn());
  }

  afterPlayerTurn(){
    if (!this.active) return;
    // End-of-turn status damage for player
    const logFn = (msg) => this.game.ui.battleLogPush(msg);
    applyEndOfTurnStatus(this.playerC, logFn);
    this.syncBars();
    if (this.playerC.fainted){
      this.handlePlayerFaint();
      return;
    }
    this.finishSoon(() => this.wildTurn());
  }

  applyMoveEffect(moveData, user, target, log){
    const eff = moveData.effect;
    if (!eff) return;
    if (eff.type === "status"){
      const t = eff.target === "self" ? user : target;
      tryInflictStatus(t, eff.status, eff.chance, log);
    }
    if (eff.type === "statmod"){
      const t = eff.target === "self" ? user : target;
      if (randInt(1, 100) <= eff.chance){
        applyStatMod(t, eff.stat, eff.stages, log);
      }
    }
  }

  playerRun(){
    if (!this.active || this.locked) return;
    this.closeMoveSelect();
    this.setLocked(true);
    this.game.ui.battleLogPush("You ran away safely.");
    this.finishSoon(() => this.endToMap());
  }

  playerCapture(){
    if (!this.active || this.locked) return;
    this.closeMoveSelect();
    this.setLocked(true);

    const inv = this.game.player.inventory;
    const has = consumeItem(inv, "capsule_basic", 1);
    if (!has){
      this.game.ui.battleLogPush("No Capsules left!");
      this.finishSoon(() => this.setLocked(false));
      return;
    }

    saveGame(this.game); // autosave item use

    const bonus = 1.0;
    // Status bonus for capture
    const statusBonus = this.wild.status ? 1.5 : 1.0;
    const p = captureChance(this.wild, bonus * statusBonus);
    const wName = creatureName(this.wild);

    this.game.ui.battleLogPush("You threw a Capsule!");
    this.game.ui.battleLogPush(`Capture chance: ${(p * 100).toFixed(0)}%`);

    if (chance(p)){
      this.game.ui.battleLogPush(`Gotcha! ${wName} was captured.`);
      const obtained = this.wild;
      const result = addCreatureToCollections(this.game.player, obtained);
      this.game.ui.battleLogPush(`${wName} was sent to your ${result.where}.`);

      saveGame(this.game); // autosave capture
      this.finishSoon(() => this.endToMap());
      return;
    }

    this.game.ui.battleLogPush(`${wName} broke free!`);
    this.finishSoon(() => this.wildTurn());
  }

  wildTurn(){
    if (!this.active) return;

    const logFn = (msg) => this.game.ui.battleLogPush(msg);

    // Status check
    if (!canActWithStatus(this.wild, logFn)){
      this.syncBars();
      if (this.wild.fainted){
        this.game.ui.battleLogPush(`${creatureName(this.wild)} fainted. You win!`);
        this.finishSoon(() => this.endToMap());
        return;
      }
      this.finishSoon(() => this.afterWildTurn());
      return;
    }

    // Pick a random move from the wild creature's moveset
    const moveSlot = this.wild.moves[randInt(0, this.wild.moves.length - 1)];
    const moveData = MOVES[moveSlot.moveId] || MOVES.tackle;
    const wName = creatureName(this.wild);

    this.game.ui.battleLogPush(`Wild ${wName} used ${moveData.name}!`);

    // Status moves
    if (moveData.category === "status"){
      if (!rollAccuracy(moveData)){
        this.game.ui.battleLogPush("But it missed!");
      } else {
        this.applyMoveEffect(moveData, this.wild, this.playerC, logFn);
      }
      this.syncBars();
      this.finishSoon(() => this.afterWildTurn());
      return;
    }

    // Accuracy check
    if (!rollAccuracy(moveData)){
      this.game.ui.battleLogPush("But it missed!");
      this.syncBars();
      this.finishSoon(() => this.afterWildTurn());
      return;
    }

    const isCrit = rollCrit(moveData);
    const dmg = computeDamage(this.wild, this.playerC, moveData, isCrit);

    const atkType = SPECIES[this.playerC.speciesId]?.type || "normal";
    const eff = typeEffectiveness(moveData.type, atkType);
    if (eff > 1) this.game.ui.battleLogPush("It's super effective!");
    else if (eff < 1 && eff > 0) this.game.ui.battleLogPush("It's not very effective...");

    if (isCrit) this.game.ui.battleLogPush("A critical hit!");

    this.game.ui.battleLogPush(`It dealt ${dmg} damage.`);
    applyDamage(this.playerC, dmg);
    this.syncBars();

    // Secondary effect
    if (moveData.effect && !this.playerC.fainted){
      this.applyMoveEffect(moveData, this.wild, this.playerC, logFn);
      this.syncBars();
    }

    if (this.playerC.fainted){
      this.handlePlayerFaint();
      return;
    }

    this.finishSoon(() => this.afterWildTurn());
  }

  afterWildTurn(){
    if (!this.active) return;
    const logFn = (msg) => this.game.ui.battleLogPush(msg);
    applyEndOfTurnStatus(this.wild, logFn);
    this.syncBars();
    if (this.wild.fainted){
      const prize = 10 + this.wild.level * 8;
      this.game.player.money += prize;
      this.game.ui.battleLogPush(`${creatureName(this.wild)} fainted. You win! (+$${prize})`);
      this.finishSoon(() => this.endToMap());
      return;
    }
    this.finishSoon(() => this.setLocked(false));
  }

  handlePlayerFaint(){
    this.game.ui.battleLogPush(`${creatureName(this.playerC)} fainted. You black out and return to town.`);
    this.finishSoon(() => {
      // Heal and warp to safe spot
      this.playerC.fainted = false;
      this.playerC.hp = this.playerC.maxHp;
      this.playerC.status = null;
      this.playerC.statStages = { atk: 0, def: 0, spd: 0 };
      this.game.player.pos = { x: 4, y: 5 };
      this.game.player.mapId = "house";
      this.game.loadMap("house");
      saveGame(this.game);
      this.endToMap();
    });
  }

  finishSoon(fn){
    window.setTimeout(fn, 450);
  }

  endToMap(){
    this.active = false;
    this.wild = null;
    this.playerC = null;
    this.locked = false;
    this.moveSelectOpen = false;
    this.game.ui.hideMoveSelect();
    this.game.ui.hideBattle();
    this.game.ui.setBattleButtonsEnabled(true);
    this.game.onBattleEnd();
  }
}
