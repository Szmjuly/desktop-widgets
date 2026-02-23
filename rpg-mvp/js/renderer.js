import { CFG } from "./config.js";
import { TILE, tileColor } from "./maps.js";
import { SPECIES } from "./data_species.js";

export class Renderer {
  constructor(canvas){
    this.canvas = canvas;
    this.ctx = canvas.getContext("2d");
  }

  clear(){
    this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
  }

  drawMap(map){
    const { ctx } = this;
    const ts = CFG.tileSize;

    for (let y = 0; y < map.h; y++){
      for (let x = 0; x < map.w; x++){
        const tile = map.tiles[y * map.w + x];
        ctx.fillStyle = tileColor(tile);
        ctx.fillRect(x * ts, y * ts, ts, ts);

        // Draw tall grass blades overlay
        if (tile === TILE.TALL_GRASS){
          ctx.fillStyle = "rgba(30,120,40,0.55)";
          const px = x * ts;
          const py = y * ts;
          for (let i = 0; i < 4; i++){
            const bx = px + 4 + i * 7;
            const by = py + ts - 4;
            ctx.beginPath();
            ctx.moveTo(bx, by);
            ctx.lineTo(bx + 2, py + 8);
            ctx.lineTo(bx + 4, by);
            ctx.fill();
          }
        }

        // subtle grid
        ctx.strokeStyle = "rgba(255,255,255,0.03)";
        ctx.strokeRect(x * ts, y * ts, ts, ts);
      }
    }
  }

  drawActors(map){
    const { ctx } = this;
    const ts = CFG.tileSize;

    for (const a of map.actors){
      const cx = a.at.x * ts + ts / 2;
      const cy = a.at.y * ts + ts / 2;

      if (a.type === "starter"){
        const sp = SPECIES[a.speciesId];
        ctx.fillStyle = "rgba(255,255,255,0.12)";
        ctx.beginPath();
        ctx.roundRect(a.at.x*ts + 6, a.at.y*ts + 10, ts - 12, ts - 10, 6);
        ctx.fill();

        ctx.fillStyle = sp.color;
        ctx.beginPath();
        ctx.arc(cx, cy, 10, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = "rgba(0,0,0,0.3)";
        ctx.fillRect(a.at.x*ts + 8, a.at.y*ts + ts - 10, ts - 16, 3);
      }

      if (a.type === "npc"){
        ctx.fillStyle = a.color || "#ffd27b";
        ctx.beginPath();
        ctx.arc(cx, cy, 10, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = "rgba(0,0,0,0.35)";
        ctx.stroke();

        // Label for special NPCs
        if (a.subtype === "shop" || a.subtype === "healer"){
          ctx.fillStyle = "rgba(255,255,255,0.85)";
          ctx.font = "bold 8px sans-serif";
          ctx.textAlign = "center";
          ctx.fillText(a.subtype === "shop" ? "SHOP" : "HEAL", cx, cy + ts / 2 + 2);
        }
      }
    }
  }

  drawPlayer(player){
    const { ctx } = this;
    const ts = CFG.tileSize;

    const x = player.pos.x * ts;
    const y = player.pos.y * ts;

    // body
    ctx.fillStyle = "#7bdcff";
    ctx.beginPath();
    ctx.roundRect(x + 6, y + 6, ts - 12, ts - 12, 8);
    ctx.fill();

    // facing indicator
    ctx.fillStyle = "rgba(0,0,0,0.35)";
    const fx = x + ts / 2;
    const fy = y + ts / 2;
    let dx = 0, dy = 0;
    if (player.facing === "up") dy = -8;
    if (player.facing === "down") dy = 8;
    if (player.facing === "left") dx = -8;
    if (player.facing === "right") dx = 8;
    ctx.beginPath();
    ctx.arc(fx + dx, fy + dy, 4, 0, Math.PI * 2);
    ctx.fill();
  }

  render(game){
    this.clear();
    if (!game.map) return;
    this.drawMap(game.map);
    this.drawActors(game.map);
    if (game.player) this.drawPlayer(game.player);
  }
}
