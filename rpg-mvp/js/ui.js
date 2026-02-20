export class UI {
  constructor(){
    this.dialogEl = document.getElementById("dialog");
    this.dialogTextEl = document.getElementById("dialogText");

    this.menuEl = document.getElementById("menu");
    this.menuTitleEl = document.getElementById("menuTitle");
    this.menuItemsEl = document.getElementById("menuItems");

    this.hudMap = document.getElementById("hudMap");
    this.hudParty = document.getElementById("hudParty");
    this.hudStorage = document.getElementById("hudStorage");
    this.hudCapsules = document.getElementById("hudCapsules");

    this.battleEl = document.getElementById("battle");
    this.wildName = document.getElementById("wildName");
    this.wildHpFill = document.getElementById("wildHpFill");
    this.wildHpText = document.getElementById("wildHpText");
    this.playerName = document.getElementById("playerName");
    this.playerHpFill = document.getElementById("playerHpFill");
    this.playerHpText = document.getElementById("playerHpText");
    this.battleLog = document.getElementById("battleLog");

    this.btnFight = document.getElementById("btnFight");
    this.btnCapture = document.getElementById("btnCapture");
    this.btnRun = document.getElementById("btnRun");

    this.activeDialog = null; // { text, onClose }
    this.activeMenu = null;   // { title, items:[{label, onPick}], index, onCancel }
  }

  setHud(game){
    this.hudMap.textContent = game.map.name;
    this.hudParty.textContent = `${game.player.party.length}/6`;
    this.hudStorage.textContent = `${game.player.storage.length}`;
    const cap = game.player.inventory.items.find(i => i.id === "capsule_basic");
    this.hudCapsules.textContent = cap ? `${cap.qty}` : "0";
  }

  isOverlayOpen(){
    return !!this.activeDialog || !!this.activeMenu || !this.battleEl.classList.contains("hidden");
  }

  showDialog(text, onClose){
    this.activeDialog = { text, onClose: onClose || null };
    this.dialogTextEl.textContent = text;
    this.dialogEl.classList.remove("hidden");
  }

  closeDialog(){
    if (!this.activeDialog) return;
    const cb = this.activeDialog.onClose;
    this.activeDialog = null;
    this.dialogEl.classList.add("hidden");
    if (cb) cb();
  }

  showMenu(title, items, onCancel){
    this.activeMenu = { title, items, index: 0, onCancel: onCancel || null };
    this.menuTitleEl.textContent = title;
    this.menuEl.classList.remove("hidden");
    this.renderMenu();
  }

  renderMenu(){
    const m = this.activeMenu;
    if (!m) return;
    this.menuItemsEl.innerHTML = "";
    m.items.forEach((it, i) => {
      const div = document.createElement("div");
      div.className = "menu-item" + (i === m.index ? " selected" : "");
      div.textContent = it.label;
      this.menuItemsEl.appendChild(div);
    });
  }

  menuMove(delta){
    const m = this.activeMenu;
    if (!m) return;
    const n = m.items.length;
    m.index = (m.index + delta + n) % n;
    this.renderMenu();
  }

  menuConfirm(){
    const m = this.activeMenu;
    if (!m) return;
    const it = m.items[m.index];
    if (it && it.onPick) it.onPick();
  }

  menuCancel(){
    const m = this.activeMenu;
    if (!m) return;
    const cb = m.onCancel;
    this.activeMenu = null;
    this.menuEl.classList.add("hidden");
    if (cb) cb();
  }

  closeMenu(){
    if (!this.activeMenu) return;
    this.activeMenu = null;
    this.menuEl.classList.add("hidden");
  }

  showBattle(){
    this.battleEl.classList.remove("hidden");
  }

  hideBattle(){
    this.battleEl.classList.add("hidden");
  }

  setBattleButtonsEnabled(enabled){
    this.btnFight.disabled = !enabled;
    this.btnCapture.disabled = !enabled;
    this.btnRun.disabled = !enabled;
  }

  setBattleRivalMode(isRival){
    this.btnCapture.style.display = isRival ? "none" : "";
  }

  setBattleHeader(playerCreatureName, wildName){
    this.playerName.textContent = playerCreatureName;
    this.wildName.textContent = wildName;
  }

  setHpBars(playerHp, playerMax, wildHp, wildMax){
    const pPct = playerMax <= 0 ? 0 : Math.max(0, Math.floor((playerHp / playerMax) * 100));
    const wPct = wildMax <= 0 ? 0 : Math.max(0, Math.floor((wildHp / wildMax) * 100));
    this.playerHpFill.style.width = `${pPct}%`;
    this.wildHpFill.style.width = `${wPct}%`;
    this.playerHpText.textContent = `HP ${playerHp}/${playerMax}`;
    this.wildHpText.textContent = `HP ${wildHp}/${wildMax}`;

    // quick color logic, keep simple
    this.playerHpFill.style.filter = pPct <= 25 ? "hue-rotate(330deg)" : "none";
    this.wildHpFill.style.filter = wPct <= 25 ? "hue-rotate(330deg)" : "none";
  }

  battleLogPush(line){
    const div = document.createElement("div");
    div.textContent = line;
    this.battleLog.appendChild(div);
    this.battleLog.scrollTop = this.battleLog.scrollHeight;
  }

  battleLogClear(){
    this.battleLog.innerHTML = "";
  }
}
