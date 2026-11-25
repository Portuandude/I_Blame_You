// I Blame You player character, SD (super-deformed) style: a head as big as the body, big glossy eyes,
// tousled short hair, a hooded short cape over a tunic, and a crystal staff. Gender-neutral on purpose
// (short hair, no lashes or lipstick, the same tunic and shorts for everyone).
// Drawn at twice the detail of the old chibi (export at scale 2). Faces right; feet on y = 0.
// o.mode picks the animation, time runs 0..duration.
import { hex, mix, clamp, seg, ease, lerp, TAU } from "../../core/index.js";
import { Grid } from "./_lib.js";

export const meta = {
  name: "SD mage",
  desc: "The game's SD mage: idle breathe and blink, walk, bouncy run, rise, fall, dash, staff cast and hurt. Gender-neutral.",
  tags: ["character", "chibi", "sd", "mage", "sprite"],
  size: [150, 112],
  origin: [75, 102],
  duration: 1,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { mode: "idle", seed: 1 },
};

const H = (s) => hex(s);
const C = {
  ink: H("#2a1c2e"),
  skin: H("#ffe6d2"), skinM: H("#fcd0b4"), skinS: H("#eaa98c"), blush: H("#ffa3ad"),
  hairD: H("#5a3a2e"), hair: H("#86573f"), hairM: H("#a8714f"), hairL: H("#c99068"), hairH: H("#f0c497"),
  irisD: H("#1d3a52"), iris: H("#2f7f9e"), irisL: H("#5cc6d4"), white: H("#ffffff"), mouth: H("#9a4a52"), tongue: H("#ff8f98"),
  capeD: H("#1f3f63"), cape: H("#2e5c8e"), capeL: H("#4a82bb"), capeH: H("#6fa6d8"), lining: H("#d9b25a"),
  tunD: H("#cfb48a"), tun: H("#f2e2c0"), tunL: H("#fff6e2"), trim: H("#3d6fae"), trimL: H("#5a8fd0"),
  belt: H("#7a4a2c"), beltL: H("#a0663c"), gold: H("#ffcf4a"), goldS: H("#c98e26"),
  pants: H("#36304a"), pantsL: H("#4a4362"),
  boot: H("#7b4a2c"), bootL: H("#a8683c"), bootS: H("#55301c"),
  wood: H("#7d4f28"), woodL: H("#b07a42"), woodD: H("#55331a"),
  gemD: H("#2a8fc8"), gem: H("#6ae4ff"), gemL: H("#c8fbff"),
};

const GRID = new Grid(-64, -104, 128, 108);
const DUR = { idle: 1.4, walk: 8 / 14, run: 8 / 20, rise: 0.2, fall: 0.2, dash: 0.2, attack: 0.5, hurt: 0.2 };
export const durations = DUR;

function base() {
  return {
    bob: 0, lean: 0, headX: 0, headY: 0,
    legs: [[-5, 0], [5, 0]],   // back, front: [foot x, lift]
    armB: 0.35, armF: 0.8,      // rad, 0 = straight down, + = forward
    staff: 0.6, staffUp: 0,
    hair: 0,                    // hair bounce (px, + = down)
    cape: 0,                    // cape flare back (px)
    squash: 0,
    eyes: "open", mouth: "smile",
    gemGlow: 0, staffBehind: false,
  };
}

function pose(mode, t) {
  const P = base(), D = DUR[mode] || 1, u = (t % D) / D, ph = u * TAU;
  if (mode === "idle") {
    const b = Math.sin(ph);
    P.bob = b > 0.2 ? -1 : 0;
    P.headY = b > 0.6 ? -1 : 0;
    P.hair = b > 0.2 ? 1 : 0;
    P.cape = 1 + Math.round(b);
    P.eyes = u > 0.84 && u < 0.92 ? "blink" : "open";
    P.gemGlow = 0.35 + 0.25 * Math.sin(ph * 2);
    P.armF = 0.8 + 0.05 * b;
  } else if (mode === "walk") {
    const s = Math.sin(ph), c = Math.cos(ph);
    P.legs = [[-1 - 6 * s, Math.max(0, -s) * 4], [1 + 6 * s, Math.max(0, s) * 4]];
    P.bob = Math.abs(c) > 0.7 ? 0 : -2;
    P.hair = Math.abs(c) > 0.7 ? -1 : 1;
    P.lean = 1;
    P.armB = 0.3 - 0.5 * s;
    P.armF = 0.6 + 0.35 * s;
    P.staff = 0.65 + 0.1 * s;
    P.cape = 3 + Math.round(2 * Math.abs(s));
  } else if (mode === "run") {
    // a bouncy SD run: little hops each step, knees up, arms pumping, hair and cape bouncing a beat behind
    const s = Math.sin(ph), hop = Math.abs(Math.sin(ph)), late = Math.abs(Math.sin(ph - 0.9));
    P.legs = [[-3 - 9 * s, Math.max(0, -s) * 8], [3 + 9 * s, Math.max(0, s) * 8]];
    P.bob = -Math.round(hop * 6);
    P.squash = hop < 0.3 ? 2 : 0;
    P.lean = 3;
    P.headX = 2;
    P.headY = Math.round(2 * (hop - late));
    P.hair = Math.round(3 * (hop - late)) + 1;
    P.armB = -0.3 - 1.1 * s;
    P.armF = 0.9 + 0.8 * s;
    P.staff = 0.9 + 0.2 * s;
    P.cape = 7 + Math.round(3 * late);
    P.mouth = "open";
  } else if (mode === "rise") {
    P.legs = [[-5, 7], [5, 4]];
    P.bob = -2; P.hair = 3; P.headY = 1;
    P.armB = -1.4; P.armF = 2.2;
    P.staff = 0.5; P.staffUp = 4;
    P.cape = 4;
    P.mouth = "open";
  } else if (mode === "fall") {
    P.legs = [[-7, 2], [6, 0]];
    P.hair = -4; P.headY = -1;
    P.armB = -2.4; P.armF = 2.5;
    P.staff = 0.3; P.staffUp = 6;
    P.cape = -3;
    P.eyes = "wide"; P.mouth = "o";
  } else if (mode === "dash") {
    const k = ease.outCubic(clamp(u * 1.6));
    P.lean = Math.round(4 + 6 * k);
    P.headX = 3;
    P.bob = 2;
    P.legs = [[-11, 3], [8, 2]];
    P.armB = -1.8; P.armF = 1.4;
    P.hair = -2;
    P.cape = 12;
    P.staff = 1.35;
    P.squash = 1;
    P.eyes = "determined"; P.mouth = "flat";
  } else if (mode === "attack") {
    // wind the staff back over the shoulder, then swing it forward with the crystal flaring
    const w = seg(u, 0, 0.35), s = ease.outBack(seg(u, 0.35, 0.6)), r = seg(u, 0.75, 1);
    P.staff = lerp(lerp(0.15, -0.75, ease.inOutSine(w)), 1.4, s) * (1 - r) + 0.25 * r;
    P.staffUp = Math.round(6 * (1 - r) * Math.max(w, s));
    P.armF = lerp(lerp(0.3, -0.9, w), 1.7, s) * (1 - r) + 0.45 * r;
    P.armB = 0.3 - 0.6 * s * (1 - r);
    P.lean = Math.round(lerp(-2 * w, 4, s) * (1 - r));
    P.headX = Math.round(lerp(-1 * w, 2, s) * (1 - r));
    P.legs = [[-7, 0], [6, 0]];
    P.bob = s > 0.5 && r < 0.5 ? 1 : 0;
    P.hair = s > 0.3 && r < 0.6 ? -2 : 0;
    P.cape = Math.round(2 + 5 * s * (1 - r));
    P.gemGlow = s * (1 - r);
    P.eyes = u > 0.3 && u < 0.7 ? "determined" : "open";
    P.mouth = u > 0.35 && u < 0.7 ? "open" : "smile";
    P.staffBehind = u < 0.36;
  } else if (mode === "hurt") {
    P.lean = -4; P.headX = -3; P.headY = u < 0.5 ? 1 : 0;
    P.legs = [[-7, 0], [2, 2]];
    P.armB = -1.6; P.armF = -1.0;
    P.hair = -3;
    P.cape = -2;
    P.staff = -0.55;
    P.eyes = "hurt"; P.mouth = "wobble";
    P.staffBehind = true;
    P.squash = u < 0.5 ? 2 : 0;
  }
  return P;
}

// ---------- helpers ----------

// soft 3-tone ball, lit from the upper left; ring = the part's own dark edge (reads as a line between parts)
function ball(g, cx, cy, rx, ry, [dark, mid, light, hi], ring = dark) {
  g.ellipse(cx, cy, rx, ry, (nx, ny) => {
    if (Math.hypot((nx * rx) / Math.max(0.5, rx - 1), (ny * ry) / Math.max(0.5, ry - 1)) > 1) return ring;
    const l = -0.55 * nx - 0.75 * ny;
    if (hi && l > 0.62) return hi;
    return l > 0.12 ? light : l > -0.45 ? mid : dark;
  });
}

// ---------- parts ----------

function cape(g, P) {
  const top = -42 + P.bob, x = P.lean - 3, f = P.cape;
  const hem = -12 + P.bob + Math.round(P.squash * 0.5);
  const pts = [[x + 7, top], [x - 5, top - 1], [x - 12 - f * 0.4, top + 10], [x - 15 - f, hem + 1 - Math.round(f * 0.3)],
    [x - 8 - f * 0.6, hem + 3 - Math.round(f * 0.2)], [x - 2 - f * 0.3, hem + 1], [x + 6, hem - 3]];
  g.poly(pts, (px, py) => {
    const v = (py - top) / (hem - top);
    if (px > x - 6 - f * 0.4 * v && v > 0.6) return C.lining; // the gold lining peeks out at the bottom
    return px < x - 10 - f * 0.6 * v ? C.capeD : v < 0.35 ? C.capeL : C.cape;
  });
  // folds
  for (let i = 0; i < 2; i++) g.line(x - 5 - i * 5, top + 8, x - 8 - i * 5 - f * 0.6, hem - 1, C.capeD);
}

function legs(g, P) {
  P.legs.forEach(([fx, lift], i) => {
    const front = i === 1, x = Math.round(fx), y = -Math.round(lift);
    const hipX = P.lean * 0.4 + (front ? 3 : -3), hipY = -12 + P.bob;
    const k = front ? 0 : 0.3;
    const pc = mix(C.pants, C.ink, k), pl = mix(C.pantsL, C.ink, k);
    g.tube([[hipX, hipY], [x, y - 5]], 3.2, (u, d) => (d < 0.45 ? pl : pc));
    // round boot, toe to the front, sole darker
    const bx = x + 1.5, by = y - 3;
    g.ellipse(bx, by, 5.2, 3.6, (nx, ny) => {
      if (ny > 0.55) return mix(C.bootS, C.ink, k);
      if (ny < -0.5) return mix(C.bootL, C.ink, k * 0.6);
      return nx < -0.4 ? mix(C.bootL, C.ink, k) : mix(C.boot, C.ink, k);
    });
    for (let d = -4; d <= 4; d++) g.set(bx + d, by - 3, mix(C.bootS, C.ink, k)); // cuff
  });
}

function tunic(g, P) {
  const top = -40 + P.bob, bot = -10 + P.bob + Math.round(P.squash * 0.5), lx = P.lean;
  for (let y = top; y <= bot; y++) {
    const v = (y - top) / (bot - top);
    const hw = lerp(7.5, 12.5, Math.pow(v, 0.7)) + P.squash;
    const sway = Math.round(-P.cape * 0.15 * v * v);
    const cx = lx * (1 - v * 0.6) + sway;
    const x0 = Math.round(cx - hw), x1 = Math.round(cx + hw);
    for (let x = x0; x <= x1; x++) {
      let c = C.tun;
      if (x <= x0 + 2) c = C.tunL;
      if (x >= x1 - 2) c = C.tunD;
      if (y >= bot - 2) c = y === bot ? C.trim : x < cx ? C.trimL : C.trim;
      g.set(x, y, c);
    }
    // a front seam with buttons-ish trim
    if (y > top + 3 && y < bot - 2) g.set(Math.round(cx + 4), y, C.tunD);
  }
  // belt with a gold buckle and a little pouch on the hip
  const by = top + 16, bcx = lx * 0.5;
  for (let x = -11; x <= 11; x++) g.set(Math.round(bcx + x), by, C.beltL), g.set(Math.round(bcx + x), by + 1, C.belt), g.set(Math.round(bcx + x), by + 2, C.belt);
  g.shade(bcx + 3, by, bcx + 6, by + 2, (x, y) => (y < by + 1 ? C.gold : C.goldS));
  g.ellipse(bcx - 7, by + 5, 3, 3, (nx, ny) => (ny < -0.3 ? C.beltL : C.belt));
  g.set(bcx - 7, by + 3, C.gold);
  return top;
}

function hood(g, P) {
  // the hood rests down on the back of the neck, plus a soft collar in front
  const x = P.lean - 7, y = -42 + P.bob;
  ball(g, x, y, 9, 5.5, [C.capeD, C.cape, C.capeL, C.capeH]);
  g.ellipse(x + 1, y + 1, 4, 2, C.capeD);
  ball(g, P.lean + 3, y + 1, 7, 3, [C.capeD, C.cape, C.capeL]);
  g.set(P.lean + 6, y + 3, C.gold); g.set(P.lean + 7, y + 3, C.goldS); // cape clasp
}

function arm(g, P, front) {
  const sx = P.lean + (front ? 7 : -4), sy = -36 + P.bob;
  const a = front ? P.armF : P.armB, L = 10;
  const hx = sx + Math.sin(a) * L, hy = sy + Math.cos(a) * L;
  const k = front ? 0 : 0.25;
  g.tube([[sx, sy], [hx, hy]], 3.6, (u, d) => mix(d > 0.75 ? C.tunD : u < 0.3 ? C.tunL : C.tun, C.ink, k));
  // sleeve cuff
  const cx = sx + Math.sin(a) * (L - 3), cy = sy + Math.cos(a) * (L - 3);
  g.ellipse(cx, cy, 3.4, 3.4, mix(C.trim, C.ink, k));
  return [hx, hy, k];
}

function hand(g, [hx, hy, k]) {
  g.ellipse(hx, hy, 3.3, 3.3, (nx, ny) => mix(Math.hypot(nx, ny) > 0.75 ? C.skinS : nx + ny < -0.3 ? C.skin : C.skinM, C.ink, k * 0.6));
}

function staff(g, P, [hx, hy]) {
  const a = P.staff, ux = Math.sin(a), uy = -Math.cos(a);
  const bx = hx - ux * 12, by = hy - uy * 12, tx = hx + ux * 22, ty = hy + uy * 22 - P.staffUp;
  g.line(bx, by, tx, ty, C.wood);
  g.line(bx + 1, by, tx + 1, ty, C.woodL);
  g.line(bx - 1, by + 1, tx - 1, ty + 1, C.woodD);
  // a forked head cradling the crystal
  const gx = tx + ux * 5, gy = ty + uy * 5;
  g.line(tx, ty, gx - 4, gy, C.wood); g.line(tx, ty, gx + 4, gy, C.wood);
  g.set(gx - 4, gy - 1, C.woodL); g.set(gx + 4, gy - 1, C.woodL);
  // crystal: a tall faceted diamond
  g.poly([[gx, gy - 9], [gx + 4, gy - 3], [gx, gy + 2], [gx - 4, gy - 3]], (x, y) => {
    if (x < gx && y < gy - 3) return C.gemL;
    if (x >= gx && y >= gy - 3) return C.gemD;
    return C.gem;
  });
  g.set(gx - 1, gy - 6, C.white); g.set(gx - 2, gy - 4, C.white);
  return [gx, gy - 3];
}

function eye(g, x, y, w, P, near) {
  const h = 10;
  if (P.eyes === "blink" || P.eyes === "closed") {
    // a happy closed arc
    for (let i = 0; i < w; i++) g.set(x + i, y + 6 + (i === 0 || i === w - 1 ? 1 : 0), C.ink);
    return;
  }
  if (P.eyes === "hurt") {
    const d = near ? -1 : 1; // > <
    for (let i = 0; i < 4; i++) g.set(x + (d > 0 ? i : w - 1 - i), y + 3 + i, C.ink), g.set(x + (d > 0 ? i : w - 1 - i), y + 9 - i, C.ink);
    return;
  }
  const top = P.eyes === "determined" ? 3 : P.eyes === "wide" ? 0 : 1;
  for (let r = top; r < h; r++)
    for (let i = 0; i < w; i++) {
      const edge = i === 0 || i === w - 1;
      if (edge && (r === h - 1 || r === top)) continue; // round the corners
      let c;
      if (r <= top + 1) c = C.ink;                       // thick upper lid line
      else if (r < 4) c = C.irisD;
      else if (r < 7) c = edge ? C.irisD : C.iris;
      else c = edge ? C.iris : C.irisL;
      if (r === h - 1) c = C.ink;
      g.set(x + i, y + r, c);
    }
  // pupil and two catch-lights
  if (P.eyes !== "wide") for (let r = 4; r < 7; r++) g.set(x + Math.floor(w / 2), y + r, C.irisD), near && g.set(x + Math.floor(w / 2) - 1, y + r, C.irisD);
  g.set(x + 1, y + top + 2, C.white); g.set(x + 2, y + top + 2, C.white); g.set(x + 1, y + top + 3, C.white); g.set(x + 2, y + top + 3, C.white);
  g.set(x + w - 2, y + 7, C.white);
  if (P.eyes === "determined") for (let i = 0; i < w; i++) g.set(x + i, y + 1 + (near ? (i < w / 2 ? 1 : 0) : (i >= w / 2 ? 1 : 0)), C.ink); // brows down
}

function head(g, P) {
  const hx = P.lean + 3 + P.headX, hy = -60 + P.bob + P.headY, hb = P.hair;
  // back hair and nape tufts, behind the face
  g.ellipse(hx - 4, hy + 1, 20, 19, (nx, ny) => (ny > 0.4 ? C.hairD : nx < -0.3 ? C.hair : C.hairM));
  for (let i = 0; i < 3; i++) g.ellipse(hx - 16 + i * 4, hy + 13 + hb - i, 3.5, 5, i === 1 ? C.hair : C.hairD);
  // face: wide and soft, cheeks puffing at the bottom
  g.ellipse(hx + 3, hy + 4, 17, 15, (nx, ny) => {
    if (nx > 0.72 || ny > 0.8) return C.skinS;
    return nx > 0.45 || ny > 0.55 ? C.skinM : C.skin;
  });
  // hair cap: a tufted outline, side-swept bangs in pointed clumps, strand highlights per clump
  const tri = (v) => Math.abs(((v % 1) + 1) % 1 - 0.5) * 2; // 1 at clump tips, 0 between
  const clump = (x) => 1 - tri((x - hx + 2) / 7);           // 1 at a bang tip
  g.shade(hx - 26, hy - 28, hx + 26, hy + 18, (x, y) => {
    const nx = (x - hx + 1) / 22.5, ny = (y - hy + 2 - hb * 0.3) / 20.5;
    const ang = Math.atan2(ny, nx), tuft = 0.06 * (1 - tri(ang * 1.6 + 0.3)) * (ny < 0.2 ? 1 : 0);
    if (nx * nx + ny * ny > (1 + tuft) * (1 + tuft)) return 0;
    const bang = hy - 3 + hb + 10 * clump(x + (y - hy) * 0.35) - (x > hx + 14 ? -7 : 0);
    const side = x < hx - 11 && y < hy + 11 + hb;
    const lock = x > hx + 16 && y < hy + 9 + hb;
    if (y > bang && !side && !lock) return 0;
    // dark strand line between clumps, running up from the valley
    if (y > hy - 9 && y < bang - 1 && tri((x + (y - hy) * 0.35 - hx + 2) / 7) > 0.86) return C.hairD;
    if (y >= bang - 1 && !side && !lock) return C.hair; // the tips are darker
    if (ny < -0.55 && nx < 0.3) return C.hairL;
    if (nx > 0.55 || ny > 0.3) return C.hair;
    return nx < -0.45 ? C.hairL : C.hairM;
  });
  // strand highlights: short slanted strokes on each clump, following the curve of the head
  for (let k = -2; k <= 2; k++) {
    const sx0 = hx + k * 7 - 1, sy0 = hy - 15 + Math.abs(k) * 2;
    for (let i = 0; i < 4; i++) g.set(sx0 + Math.round(i * 0.5) + (k < 0 ? -1 : 0), sy0 + i, i < 2 ? C.hairH : C.hairL);
  }
  // soft shadow the bangs cast on the forehead
  for (let x = hx - 10; x <= hx + 19; x++)
    for (let y = hy - 4; y <= hy + 12; y++) {
      const c = g.get(x, y), up = g.get(x, y - 1);
      if ((c === C.skin || c === C.skinM) && (up === C.hair || up === C.hairD || up === C.hairM)) g.set(x, y, C.skinS);
    }
  // a springy cowlick on top
  const tip = [hx + 2 - hb * 0.6, hy - 27 + hb * 0.5];
  g.tube([[hx - 1, hy - 19], [hx + 1, hy - 24], tip], (u) => lerp(2.2, 0.6, u), (u, d) => (d < 0.5 ? C.hairL : C.hair));
  // eyes: the near one is wider (3/4 view)
  const ey = hy + 1;
  eye(g, hx + 9, ey, 6, P, true);
  eye(g, hx - 3, ey, 5, P, false);
  // blush
  for (const bx of [hx - 4, hx + 10]) for (let i = 0; i < 4; i++) g.set(bx + i, ey + 12, C.blush), i > 0 && i < 3 && g.set(bx + i, ey + 13, C.blush);
  // mouth
  const mx = hx + 6, my = ey + 14;
  if (P.mouth === "smile") g.set(mx - 1, my, C.mouth), g.set(mx, my + 1, C.mouth), g.set(mx + 1, my, C.mouth);
  else if (P.mouth === "open") {
    g.shade(mx - 1, my, mx + 1, my + 2, (x, y) => (y >= my + 2 ? C.tongue : C.mouth));
  } else if (P.mouth === "o") g.ellipse(mx, my + 1, 1.5, 2, C.mouth);
  else if (P.mouth === "flat") g.line(mx - 1, my + 1, mx + 1, my + 1, C.mouth);
  else for (let i = -2; i <= 2; i++) g.set(mx + i, my + (i & 1 ? 1 : 0), C.mouth); // wobble
}

export function draw(p, time, o) {
  const mode = DUR[o.mode] ? o.mode : "idle";
  const t = Math.min(time, DUR[mode] - 1e-6);
  const P = pose(mode, t), g = GRID;
  g.clear();
  const back = arm(g, P, false); // behind the cape: only the hand peeks out when it swings forward
  hand(g, back);
  cape(g, P);
  legs(g, P);
  tunic(g, P);
  hood(g, P);
  let gem, front;
  if (P.staffBehind) (front = arm(g, P, true)), (gem = staff(g, P, front));
  head(g, P);
  if (!P.staffBehind) (front = arm(g, P, true)), (gem = staff(g, P, front));
  hand(g, front);
  g.outline((x, y, nx, ny) => (ny > 0 ? mix(C.ink, C.capeD, 0.25) : C.ink));
  g.draw(p);
  if (P.gemGlow > 0.05) p.glow(gem[0], gem[1], 8 + 12 * P.gemGlow, C.gem, 0.35 * P.gemGlow);
}
