export const CFG = {
  tileSize: 32,
  canvasW: 640,
  canvasH: 480,

  moveRepeatMs: 110,

  encounterChancePerStep: 0.15,

  partyCap: 6,

  saveVersion: 1,
  saveSlots: 3,
  saveKeyPrefix: "miniRpgSave_slot",
  saveKey(slot) {
    return `${this.saveKeyPrefix}${slot}_v${this.saveVersion}`;
  }
};
