export function clamp(v, a, b){
  return Math.max(a, Math.min(b, v));
}

export function randInt(min, max){
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

export function chance(p){
  return Math.random() < p;
}

export function pick(arr){
  return arr[Math.floor(Math.random() * arr.length)];
}

export function uid(prefix="id"){
  return `${prefix}_${Math.random().toString(16).slice(2)}_${Date.now().toString(16)}`;
}

export function deepCopy(obj){
  return JSON.parse(JSON.stringify(obj));
}
