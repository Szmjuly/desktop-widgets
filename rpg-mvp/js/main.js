import { CFG } from "./config.js";
import { Input } from "./input.js";
import { Renderer } from "./renderer.js";
import { buildMaps, isBlocked, findTriggerAt, adjacentPos, inGrassRegion } from "./maps.js";
import { UI } from "./ui.js";
import { SPECIES, getRivalStarterSpeciesId } from "./data_species.js";
import { Battle } from "./battle.js";
import { maybeStartEncounter } from "./encounters.js";
import { createCreatureInstance, addCreatureToCollections } from "./models.js";
import { saveGame, loadGame, getSaveSlotInfo, clearSave, newGameState } from "./save.js";

const canvas = document.getElementById("game");
const gamewrap = document.getElementById("gamewrap");
const mainMenuEl = document.getElementById("mainMenu");
const btnSave = document.getElementById("btnSave");
const btnLoad = document.getElementById("btnLoad");
const btnReset = document.getElementById("btnReset");

class Game {
  constructor() {
    this.maps = buildMaps();
    this.map = this.maps.town;

    this.input = new Input();
    this.renderer = new Renderer(canvas);
    this.ui = new UI();

    this.battle = new Battle(this);

    this.player = null;
    /** "mainMenu" | "playing" */
    this.screen = "mainMenu";
    /** "name" | "starter" | "rival" | null - only set for new game intro */
    this.introPhase = null;

    this.wireButtons();
    this.refreshMainMenuSlots();
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

    const nameInput = document.getElementById("nameInput");
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
    this.player.mapId = "lab";
    this.player.pos = { x: 5, y: 6 };
    this.loadMap("lab");
    saveGame(this);
    this.ui.showDialog(
      `Welcome, ${this.player.name}! The professor has left three creatures for you. Choose your starter at one of the pedestals.`,
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
    this.map = this.maps[mapId];
  }

  /** Restart from name/starter/rival intro without returning to main menu (e.g. after Reset). */
  startNewGameInPlace(slot) {
    const state = newGameState();
    this.player = state.player;
    this.player.saveSlot = slot;
    this.loadMap(this.player.mapId);
    this.introPhase = "name";
    this.ui.hideBattle();
    this.ui.setHud(this);
    this.showNamePrompt();
  }
  startNewGame() {
    const state = newGameState();
    this.player = state.player;
    this.player.saveSlot = 1;
    this.loadMap(this.player.mapId);
    saveGame(this);
    this.ui.setHud(this);
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

    if (this.battle.active) {
      this.handleOverlayKeys();
      return;
    }

    if (this.ui.isOverlayOpen()) {
      this.handleOverlayKeys();
      return;
    }

    const dir = this.input.getMoveDir(now);
    if (dir) this.tryMove(dir);

    if (this.input.consumePressed("Enter") || this.input.consumePressed("Space")) {
      this.tryInteract();
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
      if (this.input.consumePressed("ArrowUp")) this.ui.menuMove(-1);
      if (this.input.consumePressed("ArrowDown")) this.ui.menuMove(1);
      if (this.input.consumePressed("Enter") || this.input.consumePressed("Space")) this.ui.menuConfirm();
      if (this.input.consumePressed("Escape") || this.input.consumePressed("Backspace")) this.ui.menuCancel();
      return;
    }
  }

  tryMove(dir) {
    this.player.facing = dir;

    const next = adjacentPos(this.player.pos, dir);
    if (isBlocked(this.map, next.x, next.y)) return;

    this.player.pos = next;

    const warp = findTriggerAt(this.map, next.x, next.y, "warp");
    if (warp) {
      this.player.mapId = warp.to.mapId;
      this.player.pos = { x: warp.to.x, y: warp.to.y };
      this.loadMap(this.player.mapId);
      saveGame(this);
      return;
    }

    maybeStartEncounter(this);
  }

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
      this.ui.showDialog(actor.text, () => {});
      return;
    }
    if (actor.type === "rival") {
      this.interactRival(actor);
      return;
    }
    if (actor.type === "starter") {
      this.tryStarterSequence();
      return;
    }
  }

  interactRival(actor) {
    if (this.player.flags.firstRivalDone) {
      this.ui.showDialog("You're strong. Let's battle again sometime!", () => {});
      return;
    }
    if (!this.player.flags.chosenStarter || this.player.party.length === 0) {
      this.ui.showDialog("Get a creature from the lab first, then we'll battle!", () => {});
      return;
    }
    this.ui.showDialog("Let's battle! Show me what you've got!", () => {
      const rivalSpeciesId = getRivalStarterSpeciesId(this.player.flags.starterSpeciesId);
      const rivalCreature = createCreatureInstance(rivalSpeciesId, 5);
      this.startRivalBattle(rivalCreature);
    });
  }

  startRivalBattle(rivalCreature) {
    saveGame(this);
    this.battle.start(rivalCreature, { isRival: true });
  }

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
      return {
        label: sp.name,
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

      this.ui.showDialog(`${sp.name} joined your ${res.where}.`, () => {
        if (this.introPhase === "starter") {
          this.introPhase = "rival";
          this.ui.showDialog("Head back to town. Someone by the lab is waiting to battle you!", () => {});
        }
      });
    });
  }

  startBattle(wildCreature) {
    saveGame(this);
    this.battle.start(wildCreature, { isRival: false });
  }

  onBattleEnd(wasRival) {
    if (wasRival) {
      this.player.flags.firstRivalDone = true;
    }
    saveGame(this);
  }
}

const game = new Game();
game.start();
