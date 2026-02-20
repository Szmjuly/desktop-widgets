import { SPECIES } from "./data_species.js";
import { clamp, chance } from "./utils.js";
import { consumeItem, addCreatureToCollections } from "./models.js";
import { saveGame } from "./save.js";

function creatureName(c){
  const sp = SPECIES[c.speciesId];
  return c.nickname || sp.name;
}

function computeDamage(attacker, defender, movePower){
  const L = attacker.level;
  const atk = attacker.stats.atk;
  const def = Math.max(1, defender.stats.def);
  const base = Math.floor((((2 * L) / 5 + 2) * movePower * (atk / def)) / 50) + 2;
  const rand = 0.85 + Math.random() * 0.15;
  return Math.max(1, Math.floor(base * rand));
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

export class Battle {
  constructor(game){
    this.game = game;
    this.active = false;
    this.wild = null;
    this.playerC = null;
    this.locked = false;
  }

  start(opponentCreature, options = {}){
    const isRival = !!options.isRival;
    this.isRival = isRival;
    this.active = true;
    this.locked = false;
    this.wild = opponentCreature;
    this.playerC = this.game.player.party[0] || null;

    this.game.ui.showBattle();
    this.game.ui.battleLogClear();
    this.game.ui.setBattleRivalMode(isRival);

    const wName = creatureName(this.wild);
    const pName = this.playerC ? creatureName(this.playerC) : "No Party Creature";
    const opponentLabel = isRival ? `Rival's ${wName}` : `Wild ${wName}`;

    this.game.ui.setBattleHeader(pName, opponentLabel);
    this.game.ui.setHpBars(this.playerC?.hp ?? 0, this.playerC?.maxHp ?? 0, this.wild.hp, this.wild.maxHp);
    if (isRival) {
      this.game.ui.battleLogPush(`Your rival sent out ${wName}!`);
    } else {
      this.game.ui.battleLogPush(`A wild ${wName} appeared!`);
    }

    if (!this.playerC){
      this.game.ui.battleLogPush("You have no creatures. You run away!");
      this.endToMap();
      return;
    }
  }

  syncBars(){
    if (!this.playerC || !this.wild) return;
    this.game.ui.setHpBars(this.playerC.hp, this.playerC.maxHp, this.wild.hp, this.wild.maxHp);
  }

  setLocked(v){
    this.locked = v;
    this.game.ui.setBattleButtonsEnabled(!v);
  }

  playerFight(){
    if (!this.active || this.locked) return;
    this.setLocked(true);

    const movePower = 40;
    const dmg = computeDamage(this.playerC, this.wild, movePower);
    const wName = creatureName(this.wild);

    this.game.ui.battleLogPush(`${creatureName(this.playerC)} used Tackle!`);
    this.game.ui.battleLogPush(`It dealt ${dmg} damage to ${wName}.`);
    applyDamage(this.wild, dmg);
    this.syncBars();

    if (this.wild.fainted){
      this.game.ui.battleLogPush(`${wName} fainted. You win!`);
      this.finishSoon(() => this.endToMap());
      return;
    }

    this.finishSoon(() => this.wildTurn());
  }

  playerRun(){
    if (!this.active || this.locked) return;
    this.setLocked(true);
    this.game.ui.battleLogPush("You ran away safely.");
    this.finishSoon(() => this.endToMap());
  }

  playerCapture(){
    if (!this.active || this.locked) return;
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
    const p = captureChance(this.wild, bonus);
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

    const movePower = 35;
    const dmg = computeDamage(this.wild, this.playerC, movePower);
    const oppLabel = this.isRival ? "Rival's" : "Wild";

    this.game.ui.battleLogPush(`${oppLabel} ${creatureName(this.wild)} used Tackle!`);
    this.game.ui.battleLogPush(`It dealt ${dmg} damage.`);
    applyDamage(this.playerC, dmg);
    this.syncBars();

    if (this.playerC.fainted){
      this.game.ui.battleLogPush(`${creatureName(this.playerC)} fainted. You black out and return to town.`);
      this.finishSoon(() => {
        // Heal for MVP, and warp player to safe spot
        this.playerC.fainted = false;
        this.playerC.hp = this.playerC.maxHp;
        this.game.player.pos = { x: 10, y: 7 };
        this.game.player.mapId = "town";
        this.game.loadMap("town");
        saveGame(this.game);
        this.endToMap();
      });
      return;
    }

    this.finishSoon(() => this.setLocked(false));
  }

  finishSoon(fn){
    window.setTimeout(fn, 450);
  }

  endToMap(){
    const wasRival = this.isRival;
    this.active = false;
    this.isRival = false;
    this.wild = null;
    this.playerC = null;
    this.locked = false;
    this.game.ui.hideBattle();
    this.game.ui.setBattleRivalMode(false);
    this.game.ui.setBattleButtonsEnabled(true);
    this.game.onBattleEnd(wasRival);
  }
}
