import { CFG } from "./config.js";

export class Input {
  constructor(){
    this.down = new Set();
    this.pressed = new Set();
    this.lastMoveAt = 0;

    window.addEventListener("keydown", (e) => {
      if (e.repeat) return;
      this.down.add(e.code);
      this.pressed.add(e.code);
    });
    window.addEventListener("keyup", (e) => {
      this.down.delete(e.code);
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

  getMoveDir(now){
    const canRepeat = (now - this.lastMoveAt) >= CFG.moveRepeatMs;

    const wants =
      (this.consumePressed("ArrowUp") || (canRepeat && this.down.has("ArrowUp"))) ? "up" :
      (this.consumePressed("ArrowDown") || (canRepeat && this.down.has("ArrowDown"))) ? "down" :
      (this.consumePressed("ArrowLeft") || (canRepeat && this.down.has("ArrowLeft"))) ? "left" :
      (this.consumePressed("ArrowRight") || (canRepeat && this.down.has("ArrowRight"))) ? "right" :
      null;

    if (wants){
      this.lastMoveAt = now;
      return wants;
    }
    return null;
  }
}
