import { CFG } from "./config.js";
import { Input } from "./input.js";
import { Renderer } from "./renderer.js";
import { isBlocked, findTriggerAt, adjacentPos, inGrassRegion } from "./maps.js";
import { UI } from "./ui.js";
import { SPECIES } from "./data_species.js";
import { MOVES } from "./data_moves.js";
import { Battle } from "./battle.js";
import { maybeStartEncounter } from "./encounters.js";
import {
  createCreatureInstance, addCreatureToCollections,
  SHOP_CATALOG, buyItem, usePotion, getRivalStarterId,
} from "./models.js";
import { saveGame, loadGame, getSaveSlotInfo, clearSave, newGameState } from "./save.js";
import { generateWorld } from "./mapgen.js";

// Expose MOVES to window for UI move-select panel
window.__MOVES_CACHE = MOVES;

const canvas = document.getElementById("game");
const gamewrap = document.getElementById("gamewrap");
const mainMenuEl = document.getElementById("mainMenu");
const btnSave = document.getElementById("btnSave");
const btnLoad = document.getElementById("btnLoad");
const btnReset = document.getElementById("btnReset");

const RIVAL_NAMES = ["Blaze","Storm","Ash","Kai","Riven","Zara","Nyx","Jett"];

class Game {
  constructor() {
    // Maps will be generated per-game via seed
    this.maps = {};
    this.map = null;
    this.encounterTables = {};

    this.input = new Input();
    this.renderer = new Renderer(canvas);
    this.ui = new UI();

    this.battle = new Battle(this);

    this.player = null;
    this.screen = "mainMenu";
    this.introPhase = null;
    this.sideMenuOpen = false;

    this.wireButtons();
    this.refreshMainMenuSlots();
  }

  // ── Generate or regenerate world from seed ──
  buildWorldFromSeed(seed) {
    const world = generateWorld(seed);
    this.maps = world.maps;
    this.encounterTables = world.encounterTables;
  }

  wireButtons() {
    document.getElementById("mainMenuNewGame").addEventListener("click", () => this.doNewGame());
    document.getElementById("mainMenuSlot1").addEventListener("click", () => this.tryLoadSlotFromMenu(1));
    document.getElementById("mainMenuSlot2").addEventListener("click", () => this.tryLoadSlotFromMenu(2));
    document.getElementById("mainMenuSlot3").addEventListener("click", () => this.tryLoadSlotFromMenu(3));

    btnSave.addEventListener("click", () => {
      saveGame(this);
      this.ui.showDialog("Game saved.", () => {});
    });
    btnLoad.addEventListener("click", () => this.showLoadSlotMenu());
    btnReset.addEventListener("click", () => this.showResetConfirm());

    this.ui.btnFight.addEventListener("click", () => this.battle.playerFight());
    this.ui.btnCapture.addEventListener("click", () => this.battle.playerCapture());
    this.ui.btnRun.addEventListener("click", () => this.battle.playerRun());

    const nameConfirm = document.getElementById("namePromptConfirm");
    document.getElementById("namePrompt").addEventListener("keydown", (e) => {
      if (e.target.id !== "nameInput") return;
      if (e.key === "Enter") this.confirmName();
    });
    nameConfirm.addEventListener("click", () => this.confirmName());
  }

  refreshMainMenuSlots() {
    for (let slot = 1; slot <= CFG.saveSlots; slot++) {
      const info = getSaveSlotInfo(slot);
      const el = document.getElementById(`slot${slot}Info`);
      if (!el) continue;
      el.textContent = info.exists
        ? `${info.playerName} · ${info.mapName} · Party ${info.partyCount}/6`
        : "— Empty —";
    }
  }

  showMainMenu() {
    this.screen = "mainMenu";
    this.player = null;
    this.introPhase = null;
    mainMenuEl.classList.remove("hidden");
    gamewrap.classList.add("hidden");
    if (document.querySelector(".topbar .controls")) document.querySelector(".topbar .controls").style.display = "none";
    this.refreshMainMenuSlots();
  }

  hideMainMenu() {
    mainMenuEl.classList.add("hidden");
    gamewrap.classList.remove("hidden");
    if (document.querySelector(".topbar .controls")) document.querySelector(".topbar .controls").style.display = "";
    this.screen = "playing";
  }

  doNewGame() {
    const state = newGameState();
    this.player = state.player;
    this.player.saveSlot = 1;
    // Generate procedural world
    this.buildWorldFromSeed(this.player.worldSeed);
    // Assign rival name
    this.player.flags.rivalName = RIVAL_NAMES[Math.floor(Math.random() * RIVAL_NAMES.length)];
    // Player starts in their house
    this.player.mapId = "house";
    this.player.pos = { x: 4, y: 5 };
    this.loadMap(this.player.mapId);
    this.introPhase = "name";
    this.hideMainMenu();
    this.ui.hideBattle();
    this.ui.setHud(this);
    this.showNamePrompt();
  }

  showNamePrompt() {
    const namePrompt = document.getElementById("namePrompt");
    const nameInput = document.getElementById("nameInput");
    nameInput.value = this.player.name || "";
    nameInput.placeholder = "Player";
    namePrompt.classList.remove("hidden");
    nameInput.focus();
  }

  hideNamePrompt() {
    document.getElementById("namePrompt").classList.add("hidden");
  }

  confirmName() {
    const nameInput = document.getElementById("nameInput");
    const name = (nameInput.value || "").trim() || "Player";
    this.player.name = name.slice(0, 20);
    this.hideNamePrompt();
    this.introPhase = "starter";
    saveGame(this);
    this.ui.showDialog(
      `Welcome, ${this.player.name}! The professor has left three creatures for you at the lab. Head outside and find it!`,
      () => {}
    );
  }

  tryLoadSlotFromMenu(slot) {
    const info = getSaveSlotInfo(slot);
    if (!info.exists) {
      const msg = document.getElementById("mainMenuMessage");
      if (msg) {
        msg.textContent = `Slot ${slot} is empty.`;
        msg.classList.remove("hidden");
        window.setTimeout(() => { msg.textContent = ""; msg.classList.add("hidden"); }, 2500);
      }
      return;
    }
    this.hideMainMenu();
    this.loadSlot(slot);
  }

  loadSlot(slot) {
    const loaded = loadGame(slot);
    if (!loaded) return;
    this.player = loaded.player;
    this.player.saveSlot = slot;
    // Rebuild world from saved seed
    this.buildWorldFromSeed(this.player.worldSeed || 12345);
    this.loadMap(this.player.mapId);
    this.introPhase = null;
    this.ui.hideBattle();
    this.ui.setHud(this);
    saveGame(this);
  }

  showLoadSlotMenu() {
    const items = [];
    for (let slot = 1; slot <= CFG.saveSlots; slot++) {
      const info = getSaveSlotInfo(slot);
      items.push({
        label: info.exists ? `Slot ${slot}: ${info.playerName} (${info.mapName})` : `Slot ${slot}: Empty`,
        onPick: () => {
          this.ui.closeMenu();
          if (info.exists) this.loadSlot(slot);
          else this.ui.showDialog("That slot is empty.", () => {});
        },
      });
    }
    this.ui.showMenu("Load game", items, () => {});
  }

  showResetConfirm() {
    this.ui.showMenu("Clear save and start new game?", [
      { label: "Cancel", onPick: () => this.ui.closeMenu() },
      {
        label: "Clear this slot and restart",
        onPick: () => {
          this.ui.closeMenu();
          const slot = this.player?.saveSlot ?? 1;
          clearSave(slot);
          this.startNewGameInPlace(slot);
        },
      },
    ], () => {});
  }

  mapInGrass(x, y) {
    return inGrassRegion(this.map, x, y);
  }

  loadMap(mapId) {
    const nextMap = this.maps[mapId] || this.maps.town;
    this.map = nextMap;
    if (!this.maps[mapId] && this.player) {
      this.player.mapId = nextMap.id;
      this.player.pos = { x: 10, y: 7 };
    }
  }

  startNewGameInPlace(slot) {
    const state = newGameState();
    this.player = state.player;
    this.player.saveSlot = slot;
    this.player.flags.rivalName = RIVAL_NAMES[Math.floor(Math.random() * RIVAL_NAMES.length)];
    this.buildWorldFromSeed(this.player.worldSeed);
    this.player.mapId = "house";
    this.player.pos = { x: 4, y: 5 };
    this.loadMap(this.player.mapId);
    this.introPhase = "name";
    this.ui.hideBattle();
    this.ui.setHud(this);
    this.showNamePrompt();
  }

  start() {
    this.showMainMenu();
    this.loop(performance.now());
  }

  loop(now) {
    if (this.screen === "playing" && this.player) {
      this.update(now);
      this.renderer.render(this);
      this.ui.setHud(this);
    }
    requestAnimationFrame((t) => this.loop(t));
  }

  update(now) {
    if (this.screen !== "playing" || !this.player) return;

    const namePromptEl = document.getElementById("namePrompt");
    if (namePromptEl && !namePromptEl.classList.contains("hidden")) return;

    // Side menu open — handle its keys
    if (this.sideMenuOpen) {
      if (this.input.consumePressed("Escape") || this.input.consumePressed("KeyM")) {
        this.closeSideMenu();
      }
      return;
    }

    if (this.battle.active) {
      this.handleOverlayKeys();
      return;
    }

    if (this.ui.isOverlayOpen()) {
      this.handleOverlayKeys();
      return;
    }

    const moveResult = this.input.getMoveDir(now);
    if (moveResult) {
      if (moveResult.faceOnly) {
        this.player.facing = moveResult.dir;
      } else {
        this.tryMove(moveResult.dir);
      }
    }

    if (this.input.consumePressed("Enter") || this.input.consumePressed("Space")) {
      this.tryInteract();
    }

    if (this.input.consumePressed("KeyM")) {
      this.toggleSideMenu();
    }
  }

  handleOverlayKeys() {
    if (this.ui.activeDialog) {
      if (this.input.consumePressed("Enter") || this.input.consumePressed("Space")) {
        this.ui.closeDialog();
      }
      return;
    }
    if (this.ui.activeMenu) {
      if (this.input.consumePressed("ArrowUp") || this.input.consumePressed("KeyW")) this.ui.menuMove(-1);
      if (this.input.consumePressed("ArrowDown") || this.input.consumePressed("KeyS")) this.ui.menuMove(1);
      if (this.input.consumePressed("Enter") || this.input.consumePressed("Space")) this.ui.menuConfirm();
      if (this.input.consumePressed("Escape") || this.input.consumePressed("Backspace")) this.ui.menuCancel();
      return;
    }
  }

  // ── Side Menu ──
  toggleSideMenu() {
    if (this.sideMenuOpen) this.closeSideMenu();
    else this.openSideMenu();
  }

  openSideMenu() {
    this.sideMenuOpen = true;
    this.ui.showSideMenu(this, {
      onParty: () => this.showPartyView(),
      onBag: () => this.showBagView(),
      onMap: () => { this.closeSideMenu(); this.ui.showDialog(`Current location: ${this.map.name}`, () => {}); },
      onSave: () => { this.closeSideMenu(); saveGame(this); this.ui.showDialog("Game saved.", () => {}); },
      onClose: () => this.closeSideMenu(),
    });
  }

  closeSideMenu() {
    this.sideMenuOpen = false;
    this.ui.hideSideMenu();
  }

  showPartyView() {
    this.closeSideMenu();
    const items = this.player.party.map((c, i) => {
      const sp = SPECIES[c.speciesId];
      const statusStr = c.status ? ` [${c.status.toUpperCase()}]` : "";
      return {
        label: `${sp.name} Lv${c.level} HP:${c.hp}/${c.maxHp}${statusStr}`,
        onPick: () => {
          this.ui.closeMenu();
          const result = usePotion(this.player, c.uid);
          if (result) {
            this.ui.showDialog(`Used ${result.itemName}! ${sp.name} recovered ${result.healed} HP.`, () => {});
            saveGame(this);
          } else {
            this.ui.showDialog(c.hp >= c.maxHp ? `${sp.name} is already at full HP.` : "No healing items available.", () => {});
          }
        },
      };
    });
    if (items.length === 0) items.push({ label: "No creatures in party", onPick: () => this.ui.closeMenu() });
    this.ui.showMenu("Party (select to heal)", items, () => {});
  }

  showBagView() {
    this.closeSideMenu();
    const items = this.player.inventory.items
      .filter(i => i.qty > 0)
      .map(i => ({
        label: `${i.name} x${i.qty}`,
        onPick: () => this.ui.closeMenu(),
      }));
    items.push({ label: `Money: $${this.player.money}`, onPick: () => this.ui.closeMenu() });
    this.ui.showMenu("Bag", items, () => {});
  }

  // ── Shop ──
  openShop() {
    const items = SHOP_CATALOG.map(entry => ({
      label: `${entry.name} — $${entry.price}`,
      onPick: () => {
        if (buyItem(this.player, entry)) {
          this.ui.closeMenu();
          saveGame(this);
          this.ui.showDialog(`Bought ${entry.name}! ($${this.player.money} left)`, () => this.openShop());
        } else {
          this.ui.closeMenu();
          this.ui.showDialog("Not enough money!", () => this.openShop());
        }
      },
    }));
    this.ui.showMenu(`Shop — $${this.player.money}`, items, () => {});
  }

  // ── Movement ──
  tryMove(dir) {
    this.player.facing = dir;

    const next = adjacentPos(this.player.pos, dir);
    if (isBlocked(this.map, next.x, next.y)) return;

    this.player.pos = next;

    const warp = findTriggerAt(this.map, next.x, next.y, "warp");
    if (warp) {
      if (this.player.mapId === "lab" && !this.player.flags.chosenStarter) {
        this.ui.showDialog("Choose one of the three starter creatures before leaving the lab.", () => {});
        return;
      }

      this.player.mapId = warp.to.mapId;
      this.player.pos = { x: warp.to.x, y: warp.to.y };
      this.loadMap(this.player.mapId);
      saveGame(this);

      // Check for rival encounter on route entry
      this.checkRivalEncounter();
      return;
    }

    maybeStartEncounter(this);
  }

  // ── Interaction ──
  tryInteract() {
    const front = adjacentPos(this.player.pos, this.player.facing);
    let actor = this.map.actors.find((a) => a.at.x === front.x && a.at.y === front.y);
    if (!actor) {
      actor = this.map.actors.find((a) => a.at.x === this.player.pos.x && a.at.y === this.player.pos.y);
    }
    if (actor) this.interactWithActor(actor);
  }

  interactWithActor(actor) {
    if (actor.type === "npc") {
      // Shop keeper
      if (actor.subtype === "shop") {
        this.openShop();
        return;
      }
      // Healer
      if (actor.subtype === "healer") {
        this.player.party.forEach(c => {
          c.hp = c.maxHp; c.fainted = false; c.status = null;
          c.statStages = { atk: 0, def: 0, spd: 0 };
          c.moves.forEach(m => { const md = MOVES[m.moveId]; if (md) m.pp = md.pp; });
        });
        saveGame(this);
        this.ui.showDialog("Your creatures have been fully healed!", () => {});
        return;
      }
      this.ui.showDialog(actor.text, () => {});
      return;
    }
    if (actor.type === "starter") {
      this.tryStarterSequence();
      return;
    }
  }

  // ── Starter selection with info ──
  tryStarterSequence() {
    if (this.player.mapId !== "lab") return;

    if (this.player.flags.chosenStarter) {
      const sp = SPECIES[this.player.flags.starterSpeciesId];
      this.ui.showDialog(`You already chose ${sp.name}. Take good care of it.`, () => {});
      return;
    }

    const starters = this.maps.lab.actors.filter((a) => a.type === "starter");
    const items = starters.map((a) => {
      const sp = SPECIES[a.speciesId];
      const moves = (sp.learnset || []).filter(e => e.level <= 5).map(e => MOVES[e.moveId]?.name || e.moveId);
      return {
        label: `${sp.name} (${sp.type.toUpperCase()}) HP:${sp.baseStats.hp} ATK:${sp.baseStats.atk} DEF:${sp.baseStats.def} SPD:${sp.baseStats.spd} — Moves: ${moves.join(", ")}`,
        onPick: () => {
          this.ui.closeMenu();
          this.confirmStarterPick(a.speciesId);
        },
      };
    });

    this.ui.showMenu("Choose your starter", items, () => {
      this.ui.showDialog("Come back when you're ready to choose.", () => {});
    });
  }

  confirmStarterPick(speciesId) {
    const sp = SPECIES[speciesId];
    this.ui.showDialog(`You chose ${sp.name}!`, () => {
      this.player.flags.chosenStarter = true;
      this.player.flags.starterSpeciesId = speciesId;

      const starter = createCreatureInstance(speciesId, 5);
      const res = addCreatureToCollections(this.player, starter);

      saveGame(this);

      const rivalName = this.player.flags.rivalName || "???";
      const rivalStarterId = getRivalStarterId(speciesId);
      const rivalSp = SPECIES[rivalStarterId];

      this.ui.showDialog(`${sp.name} joined your ${res.where}.`, () => {
        this.ui.showDialog(`Your rival ${rivalName} chose ${rivalSp.name}! You'll meet them on the routes ahead.`, () => {});
      });
    });
  }

  // ── Rival encounters ──
  checkRivalEncounter() {
    if (!this.player.flags.chosenStarter) return;
    const rivalMaps = ["rw1", "re1"];
    const mapId = this.player.mapId;
    if (!rivalMaps.includes(mapId)) return;
    if (this.player.flags.rivalDefeated[mapId]) return;

    // Trigger rival battle
    const rivalName = this.player.flags.rivalName || "Rival";
    const rivalStarterId = getRivalStarterId(this.player.flags.starterSpeciesId);
    const rivalLevel = 5 + (this.player.flags.rivalEncounters || 0) * 3;
    const rivalCreature = createCreatureInstance(rivalStarterId, rivalLevel);

    this.ui.showDialog(`${rivalName}: Hey ${this.player.name}! Let's battle!`, () => {
      this.player.flags.rivalEncounters = (this.player.flags.rivalEncounters || 0) + 1;
      this.startRivalBattle(rivalCreature, rivalName, mapId);
    });
  }

  startRivalBattle(rivalCreature, rivalName, mapId) {
    saveGame(this);
    this.battle.start(rivalCreature, `${rivalName}'s`);
    // Override battle end to mark rival defeated and give money
    const origEnd = this.battle.endToMap.bind(this.battle);
    this.battle.endToMap = () => {
      if (rivalCreature.fainted) {
        this.player.flags.rivalDefeated[mapId] = true;
        const prize = 200 + (this.player.flags.rivalEncounters || 1) * 100;
        this.player.money += prize;
        origEnd();
        this.ui.showDialog(`You defeated ${rivalName}! Won $${prize}!`, () => {});
      } else {
        origEnd();
      }
    };
  }

  startBattle(wildCreature) {
    saveGame(this);
    this.battle.start(wildCreature);
  }

  onBattleEnd() {
    saveGame(this);
  }
}

const game = new Game();
game.start();
