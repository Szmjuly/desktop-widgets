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
    this.wildStatus = document.getElementById("wildStatus");
    this.playerName = document.getElementById("playerName");
    this.playerHpFill = document.getElementById("playerHpFill");
    this.playerHpText = document.getElementById("playerHpText");
    this.playerStatus = document.getElementById("playerStatus");
    this.battleLog = document.getElementById("battleLog");

    this.moveSelectEl = document.getElementById("moveSelect");

    this.btnFight = document.getElementById("btnFight");
    this.btnCapture = document.getElementById("btnCapture");
    this.btnRun = document.getElementById("btnRun");

    this.sideMenuEl = document.getElementById("sideMenu");

    this.activeDialog = null; // { text, onClose }
    this.activeMenu = null;   // { title, items:[{label, onPick}], index, onCancel }
  }

  setHud(game){
    if (this.hudMap) this.hudMap.textContent = game.map?.name || "?";
    if (this.hudParty) this.hudParty.textContent = `${game.player.party.length}/6`;
    if (this.hudStorage) this.hudStorage.textContent = `${game.player.storage.length}`;
    const cap = game.player.inventory.items.find(i => i.id === "capsule_basic");
    if (this.hudCapsules) this.hudCapsules.textContent = cap ? `${cap.qty}` : "0";
    const moneyEl = document.getElementById("hudMoney");
    if (moneyEl) moneyEl.textContent = `$${game.player.money}`;
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

  setStatusIndicators(playerStatus, wildStatus){
    const label = (s) => {
      if (!s) return "";
      const map = { burn: "BRN", paralyze: "PAR", poison: "PSN", sleep: "SLP", freeze: "FRZ" };
      return map[s] || "";
    };
    if (this.playerStatus) this.playerStatus.textContent = label(playerStatus);
    if (this.wildStatus) this.wildStatus.textContent = label(wildStatus);
  }

  showMoveSelect(creature, onPick, onCancel){
    if (!this.moveSelectEl) return;
    this.moveSelectEl.innerHTML = "";
    this.moveSelectEl.classList.remove("hidden");

    // Import MOVES dynamically via the creature's move list
    const movesModule = window.__MOVES_CACHE;
    const moves = creature.moves || [];

    moves.forEach((slot) => {
      const data = movesModule?.[slot.moveId];
      const name = data?.name ?? slot.moveId;
      const type = data?.type ?? "normal";
      const pp = slot.pp ?? 0;
      const maxPp = data?.pp ?? "?";
      const power = data?.power || "—";
      const acc = data?.accuracy ?? "—";
      const cat = data?.category ?? "?";

      const btn = document.createElement("button");
      btn.className = `move-btn move-type-${type}`;
      btn.innerHTML = `
        <span class="move-name">${name}</span>
        <span class="move-info">${type.toUpperCase()} · ${cat === "status" ? "Status" : `Pow ${power}`} · Acc ${acc}</span>
        <span class="move-pp">PP ${pp}/${maxPp}</span>
      `;
      btn.disabled = pp <= 0;
      btn.addEventListener("click", () => onPick(slot));
      this.moveSelectEl.appendChild(btn);
    });

    const cancelBtn = document.createElement("button");
    cancelBtn.className = "move-btn move-cancel";
    cancelBtn.textContent = "Back";
    cancelBtn.addEventListener("click", () => onCancel());
    this.moveSelectEl.appendChild(cancelBtn);
  }

  hideMoveSelect(){
    if (!this.moveSelectEl) return;
    this.moveSelectEl.classList.add("hidden");
    this.moveSelectEl.innerHTML = "";
  }

  // ── Side Menu ──
  showSideMenu(game, callbacks){
    if (!this.sideMenuEl) return;
    this.sideMenuEl.innerHTML = "";
    this.sideMenuEl.classList.remove("hidden");

    const title = document.createElement("div");
    title.className = "side-menu-title";
    title.textContent = "Menu";
    this.sideMenuEl.appendChild(title);

    const buttons = [
      { label: "Party", icon: "👥", action: callbacks.onParty },
      { label: "Bag", icon: "🎒", action: callbacks.onBag },
      { label: "Map", icon: "🗺", action: callbacks.onMap },
      { label: "Save", icon: "💾", action: callbacks.onSave },
      { label: "Close", icon: "✕", action: callbacks.onClose },
    ];

    buttons.forEach(b => {
      const btn = document.createElement("button");
      btn.className = "side-menu-btn";
      btn.innerHTML = `<span class="side-menu-icon">${b.icon}</span><span>${b.label}</span>`;
      btn.addEventListener("click", b.action);
      this.sideMenuEl.appendChild(btn);
    });

    // Player info
    if (game.player) {
      const info = document.createElement("div");
      info.className = "side-menu-info";
      info.innerHTML = `
        <div>${game.player.name}</div>
        <div class="side-menu-detail">$${game.player.money}</div>
        <div class="side-menu-detail">${game.map?.name || "?"}</div>
      `;
      this.sideMenuEl.appendChild(info);
    }
  }

  hideSideMenu(){
    if (!this.sideMenuEl) return;
    this.sideMenuEl.classList.add("hidden");
    this.sideMenuEl.innerHTML = "";
  }
}
