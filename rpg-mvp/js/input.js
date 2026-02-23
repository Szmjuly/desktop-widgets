import { CFG } from "./config.js";

const DIR_KEYS = {
  ArrowUp: "up", ArrowDown: "down", ArrowLeft: "left", ArrowRight: "right",
  KeyW: "up", KeyS: "down", KeyA: "left", KeyD: "right",
};

export class Input {
  constructor(){
    this.down = new Set();
    this.pressed = new Set();       // single-frame presses (consumed once)
    this.lastMoveAt = 0;
    this.holdStart = {};            // code → timestamp when key went down
    this.holdThresholdMs = 120;     // ms before a held arrow triggers movement

    window.addEventListener("keydown", (e) => {
      if (e.repeat) return;
      this.down.add(e.code);
      this.pressed.add(e.code);
      if (!this.holdStart[e.code]) this.holdStart[e.code] = performance.now();
    });
    window.addEventListener("keyup", (e) => {
      this.down.delete(e.code);
      delete this.holdStart[e.code];
    });
  }

  consumePressed(code){
    if (this.pressed.has(code)){
      this.pressed.delete(code);
      return true;
    }
    return false;
  }

  clearPressed(){
    this.pressed.clear();
  }

  /** Returns { dir, faceOnly } or null.
   *  faceOnly=true means the key was tapped (not held long enough to walk). */
  getMoveDir(now){
    const canRepeat = (now - this.lastMoveAt) >= CFG.moveRepeatMs;

    // Check each direction key
    for (const [code, dir] of Object.entries(DIR_KEYS)){
      // Fresh press
      if (this.pressed.has(code)){
        this.pressed.delete(code);
        const heldMs = now - (this.holdStart[code] ?? now);
        if (heldMs < this.holdThresholdMs){
          // Tap — face only (no movement yet)
          return { dir, faceOnly: true };
        }
        // Held past threshold on first press frame — move
        this.lastMoveAt = now;
        return { dir, faceOnly: false };
      }
      // Held key repeat
      if (canRepeat && this.down.has(code)){
        const heldMs = now - (this.holdStart[code] ?? 0);
        if (heldMs >= this.holdThresholdMs){
          this.lastMoveAt = now;
          return { dir, faceOnly: false };
        }
      }
    }
    return null;
  }
}
