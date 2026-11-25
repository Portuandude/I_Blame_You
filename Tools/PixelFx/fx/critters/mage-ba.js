// I Blame You player character, soft gacha-SD style: a big round head (larger than the body), tall glossy
// gradient eyes, fluffy silver hair, an oversized hooded jacket, stubby limbs, and colored line art instead
// of black outlines. Gender-neutral on purpose (short hair, shorts and knee socks, the same jacket for all).
// Export at scale 2. Faces right; feet on y = 0. o.mode picks the animation, time runs 0..duration.
import { hex, mix, clamp, seg, ease, lerp, TAU } from "../../core/index.js";
import { Grid } from "./_lib.js";

export const meta = {
  name: "SD mage (soft)",
  desc: "The game's soft-SD mage: idle, walk, bouncy run, rise, fall, dash, wand cast and hurt. Gender-neutral.",
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
  line: H("#3a2c4a"),
  skin: H("#fff0e6"), skinM: H("#ffdccb"), skinS: H("#f5b9a6"), blush: H("#ff8fa3"),
  hairD: H("#2c4791"), hair: H("#4569c0"), hairM: H("#6a94dc"), hairL: H("#9cc0f2"), hairH: H("#e4f2ff"),
  white: H("#ffffff"),
  mouth: H("#b5525f"), tongue: H("#ff9aa6"),
  jakD: H("#b4bbd6"), jak: H("#e6e9f4"), jakL: H("#f6f7fc"), jakH: H("#ffffff"),
  trim: H("#5b6db0"), trimS: H("#45528d"), tie: H("#d99a2e"), tieL: H("#ffcf5c"),
  gold: H("#ffd45e"), goldS: H("#e0a43a"),
  shorts: H("#3d4266"), shortsL: H("#555b85"), sock: H("#ffffff"), sockS: H("#d3d6e8"),
  shoe: H("#4a5a9a"), shoeL: H("#6f82c8"), sole: H("#f2f2f6"),
  wand: H("#fff6dc"), wandS: H("#d9b86a"),
  gemD: H("#2a8fc8"), gem: H("#6ae4ff"), gemL: H("#d8fdff"),
};
// odd eyes: the near eye amber, the far eye sky blue (dark top -> bright glow at the bottom)
const EYE_NEAR = { d: H("#5e3212"), m: H("#b8701c"), l: H("#e8a63a"), g: H("#ffe28a") };
const EYE_FAR = { d: H("#1f2f6e"), m: H("#3f6fd0"), l: H("#5fb0f0"), g: H("#a0f0ff") };

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

function legs(g, P) {
  P.legs.forEach(([fx, lift], i) => {
    const front = i === 1, x = Math.round(fx * 0.8), y = -Math.round(lift * 0.75);
    const hipX = P.lean * 0.4 + (front ? 3 : -3), hipY = -11 + P.bob;
    const k = front ? 0 : 0.22;
    g.tube([[hipX, hipY], [x, y - 4]], 3.3, (u, d) => mix(u < 0.3 ? (d < 0.5 ? C.shortsL : C.shorts) : u < 0.45 ? C.skinM : d < 0.5 ? C.sock : C.sockS, C.line, k));
    const bx = x + 1.5, by = y - 2.6;
    g.ellipse(bx, by, 4.8, 3.2, (nx, ny) => mix(ny > 0.45 ? C.sole : nx < -0.3 && ny < 0 ? C.shoeL : C.shoe, C.line, k));
  });
}

function jacket(g, P) {
  // an oversized hooded jacket: bell-shaped, white zip trim, a little tie at the collar
  const top = -33 + P.bob, bot = -9 + P.bob + Math.round(P.squash * 0.5), lx = P.lean;
  for (let y = top; y <= bot; y++) {
    const v = (y - top) / (bot - top);
    const hw = lerp(8, 12.5, Math.pow(v, 0.6)) + P.squash;
    const sway = Math.round(-P.cape * 0.2 * v * v);
    const cx = lx * (1 - v * 0.6) + sway;
    const x0 = Math.round(cx - hw), x1 = Math.round(cx + hw + (v > 0.85 ? 0 : 0));
    for (let x = x0; x <= x1; x++) {
      let c = C.jak;
      if (x <= x0 + 2) c = C.jakL;
      if (x <= x0 + 1 && v < 0.5) c = C.jakH;
      if (x >= x1 - 2) c = C.jakD;
      if (y >= bot - 1) c = x < cx ? C.trim : C.trimS;
      // the white zip trim down the front
      if (Math.abs(x - (cx + 5)) < 1 && y > top + 2) c = C.trim;
      g.set(x, y, c);
    }
  }
  // pockets
  const py = top + 15, pcx = lx * 0.5;
  for (let x = -9; x <= -4; x++) g.set(Math.round(pcx + x), py, C.jakD);
  // collar + tie
  const tx = lx + 5;
  g.poly([[tx - 7, top - 1], [tx, top + 4], [tx + 6, top - 1], [tx + 4, top + 2], [tx, top + 6], [tx - 5, top + 2]], (x, y) => (x < tx ? C.trim : C.trimS));
  g.poly([[tx - 1.5, top + 3], [tx + 1.5, top + 3], [tx + 2, top + 9], [tx, top + 11], [tx - 2, top + 9]], (x, y) => (x < tx ? C.tieL : C.tie));
  g.set(tx, top + 3, C.gold);
}

function hood(g, P) {
  // a big soft hood resting behind the neck, bouncing a little on the run
  const x = P.lean - 7, y = -34 + P.bob - Math.round(P.hair * 0.3);
  ball(g, x, y, 10, 6, [C.jakD, C.jak, C.jakL, C.jakH]);
  g.ellipse(x + 2, y + 1, 5, 2.5, C.jakD);
}

function arm(g, P, front) {
  const sx = P.lean + (front ? 7 : -5), sy = -29 + P.bob;
  const a = front ? P.armF : P.armB, L = 8;
  const hx = sx + Math.sin(a) * L, hy = sy + Math.cos(a) * L;
  const k = front ? 0 : 0.22;
  // floppy oversized sleeve, wider at the cuff
  g.tube([[sx, sy], [hx, hy]], (u) => lerp(3.4, 4.4, u), (u, d) => mix(d > 0.75 ? C.jakD : u < 0.35 ? C.jakL : C.jak, C.line, k));
  const cx = sx + Math.sin(a) * (L - 1), cy = sy + Math.cos(a) * (L - 1);
  g.ellipse(cx, cy, 4, 4, (nx) => mix(nx < 0 ? C.trim : C.trimS, C.line, k));
  return [sx + Math.sin(a) * (L + 2), sy + Math.cos(a) * (L + 2), k];
}

function hand(g, [hx, hy, k]) {
  g.ellipse(hx, hy, 2.8, 2.8, (nx, ny) => mix(Math.hypot(nx, ny) > 0.75 ? C.skinS : C.skin, C.line, k * 0.6));
}

function staff(g, P, [hx, hy]) {
  // a short wand: cream shaft with gold bands and a star crystal on top
  const a = P.staff, ux = Math.sin(a), uy = -Math.cos(a);
  const bx = hx - ux * 7, by = hy - uy * 7, tx = hx + ux * 19, ty = hy + uy * 19 - P.staffUp;
  g.line(bx, by, tx, ty, C.wand);
  g.line(bx + 1, by, tx + 1, ty, C.wandS);
  for (const f of [0.15, 0.85]) g.set(bx + (tx - bx) * f, by + (ty - by) * f, C.gold), g.set(bx + (tx - bx) * f + 1, by + (ty - by) * f, C.goldS);
  const gx = tx + ux * 4, gy = ty + uy * 4;
  // a four-point star crystal
  g.poly([[gx, gy - 7], [gx + 1.6, gy - 1.6], [gx + 7, gy], [gx + 1.6, gy + 1.6], [gx, gy + 7], [gx - 1.6, gy + 1.6], [gx - 7, gy], [gx - 1.6, gy - 1.6]],
    (x, y) => (x < gx && y < gy ? C.gemL : x >= gx && y >= gy ? C.gemD : C.gem));
  g.set(gx, gy, C.white);
  return [gx, gy];
}

function eye(g, x, y, w, P, near) {
  const h = 14, E = near ? EYE_NEAR : EYE_FAR;
  if (P.eyes === "blink" || P.eyes === "closed") {
    for (let i = 0; i < w; i++) g.set(x + i, y + 9 - (i > 0 && i < w - 1 ? 1 : 0), C.line); // ^ happy arc
    return;
  }
  if (P.eyes === "hurt") {
    const d = near ? -1 : 1;
    for (let i = 0; i < 4; i++) g.set(x + (d > 0 ? i : w - 1 - i), y + 5 + i, C.line), g.set(x + (d > 0 ? i : w - 1 - i), y + 11 - i, C.line);
    return;
  }
  const top = P.eyes === "determined" ? 3 : P.eyes === "wide" ? 0 : 1;
  for (let r = top; r < h; r++)
    for (let i = 0; i < w; i++) {
      const edge = i === 0 || i === w - 1;
      if (edge && (r === h - 1 || r === top || r === h - 2)) continue; // tall rounded oval
      let c;
      if (r <= top + 1) c = C.line;                // the upper lid
      else if (r < 6) c = E.d;
      else if (r < 9) c = E.m;
      else if (r < 12) c = edge ? E.m : E.l;
      else c = edge ? E.l : E.g;         // the glow at the bottom of the iris
      g.set(x + i, y + r, c);
    }
  // lid flick at the outer corner, and a soft lower lid
  g.set(near ? x + w : x - 1, y + top, C.line);
  for (let i = 1; i < w - 1; i++) g.set(x + i, y + h, C.skinS);
  // pupil and catch-lights: a big soft one up top, a sparkle below
  if (P.eyes !== "wide") for (let r = 5; r < 9; r++) g.set(x + Math.floor(w / 2), y + r, E.d), g.set(x + Math.floor(w / 2) - 1, y + r, r > 5 && r < 8 ? E.d : E.m);
  g.shade(x + 1, y + top + 2, x + 2, y + top + 4, () => C.white);
  g.set(x + w - 2, y + 11, C.white); g.set(x + w - 3, y + 11, C.white);
  if (P.eyes === "determined") for (let i = 0; i < w; i++) g.set(x + i, y + 1 + (near ? (i < w / 2 ? 1 : 0) : (i >= w / 2 ? 1 : 0)), C.line);
}

// long hair falling down the back to the hips, swept back by the run and swaying a beat late
function longHair(g, P) {
  const hx = P.lean + 3 + P.headX, hy = -56 + P.bob + P.headY, hb = P.hair;
  const bot = -9 + P.bob + Math.max(0, hb), sw = P.cape * 0.55 + 1;
  const tri = (v) => Math.abs(((v % 1) + 1) % 1 - 0.5) * 2;
  g.shade(hx - 34 - sw, hy, hx + 6, bot + 4, (x, y) => {
    const v = clamp((y - hy) / (bot - hy));
    const x0 = hx - 23 - sw * v * v + 2 * v, x1 = hx + 3 - sw * v * v * 0.8 - 3 * v;
    if (x < x0 || x > x1) return 0;
    const tip = bot - 5 * tri((x - x0) / 5.5); // pointed ends
    if (y > tip) return 0;
    const strand = tri((x - x0 + v * 3) / 5.5) > 0.86;
    if (strand && y > hy + 6) return C.hairD;
    return x < x0 + 3 ? C.hairM : x > x1 - 4 ? C.hairD : C.hair;
  });
}

function head(g, P) {
  const hx = P.lean + 3 + P.headX, hy = -56 + P.bob + P.headY, hb = P.hair;
  const tri = (v) => Math.abs(((v % 1) + 1) % 1 - 0.5) * 2;
  // back hair: a fluffy mass with flared tufts at the nape
  g.ellipse(hx - 3, hy + 1, 25, 23, (nx, ny) => (ny > 0.35 ? C.hairD : nx < -0.4 ? C.hair : C.hairM));
  for (let i = 0; i < 4; i++) g.tube([[hx - 12 + i * 3, hy + 10], [hx - 18 + i * 2.5 - hb * 0.4, hy + 19 + hb - i]], (u) => lerp(3.4, 0.8, u), i % 2 ? C.hair : C.hairD);
  // face: a soft round bun
  g.ellipse(hx + 3, hy + 6, 20, 17, (nx, ny) => (nx > 0.75 || ny > 0.82 ? C.skinS : nx > 0.5 || ny > 0.6 ? C.skinM : C.skin));
  // hair cap with soft pointed bangs, long side locks
  const clump = (x, y) => 1 - tri((x - hx + 1 + (y - hy) * 0.3) / 6.5);
  g.shade(hx - 30, hy - 32, hx + 30, hy + 32, (x, y) => {
    const nx = (x - hx + 1) / 26.5, ny = (y - hy + 2 - hb * 0.3) / 24.5;
    const ang = Math.atan2(ny, nx), tuft = 0.05 * (1 - tri(ang * 1.9 + 0.2)) * (ny < 0.1 ? 1 : 0);
    if (nx * nx + ny * ny > (1 + tuft) * (1 + tuft) && !(x > hx + 19 && x < hx + 26 && y > hy)) return 0;
    const bang = hy - 2 + hb + 11 * clump(x, y);
    const side = x < hx - 12 && y < hy + 13 + hb;
    const lock = x > hx + 19 && y < hy + 26 + hb - Math.max(0, x - hx - 21) * 4; // a long pointed lock framing the cheek
    if (y > bang && !side && !lock) return 0;
    if (y > hy - 8 && y < bang - 1 && tri((x - hx + 1 + (y - hy) * 0.3) / 6.5) > 0.86) return C.hairD;
    if (y >= bang - 1 && !side && !lock) return C.hair;
    if (ny < -0.5 && nx < 0.35) return C.hairL;
    if (nx > 0.6 || ny > 0.3) return C.hair;
    return nx < -0.5 ? C.hairL : C.hairM;
  });
  // shine: a broken angel ring
  for (let k = -2; k <= 2; k++) {
    const sx0 = hx + k * 7, sy0 = hy - 14 + Math.abs(k) * 2;
    for (let i = 0; i < 4; i++) g.set(sx0 + i, sy0 + (i === 0 || i === 3 ? 1 : 0), C.hairH);
  }
  // fluffy cowlick
  g.tube([[hx - 2, hy - 20], [hx + 2, hy - 27], [hx + 5 - hb * 0.6, hy - 26 + hb * 0.5]], (u) => lerp(2.4, 0.7, u), (u, d) => (d < 0.5 ? C.hairL : C.hair));
  // star hairpin on the near side
  const sx = hx + 14, sy = hy - 6;
  g.poly([[sx, sy - 4], [sx + 1.3, sy - 1.3], [sx + 4, sy], [sx + 1.3, sy + 1.3], [sx, sy + 4], [sx - 1.3, sy + 1.3], [sx - 4, sy], [sx - 1.3, sy - 1.3]], (x, y) => (x < sx && y < sy ? C.gold : C.goldS));
  // bang shadow on the forehead
  for (let x = hx - 12; x <= hx + 21; x++)
    for (let y = hy - 4; y <= hy + 14; y++) {
      const c = g.get(x, y), up = g.get(x, y - 1);
      if ((c === C.skin || c === C.skinM) && (up === C.hair || up === C.hairM || up === C.hairD)) g.set(x, y, C.skinM);
    }
  // big odd eyes (near one wider), tiny mouth
  const ey = hy + 2;
  eye(g, hx + 10, ey, 8, P, true);
  eye(g, hx - 5, ey, 7, P, false);
  const mx = hx + 6, my = ey + 18;
  if (P.mouth === "smile") g.set(mx - 1, my, C.mouth), g.set(mx, my + 1, C.mouth), g.set(mx + 1, my, C.mouth);
  else if (P.mouth === "open") g.shade(mx - 1, my, mx + 1, my + 2, (x, y) => (y >= my + 2 ? C.tongue : C.mouth));
  else if (P.mouth === "o") g.ellipse(mx, my + 1, 1.5, 2, C.mouth);
  else if (P.mouth === "flat") g.line(mx - 1, my + 1, mx + 1, my + 1, C.mouth);
  else for (let i = -2; i <= 2; i++) g.set(mx + i, my + (i & 1 ? 1 : 0), C.mouth);
}

export function draw(p, time, o) {
  const mode = DUR[o.mode] ? o.mode : "idle";
  const t = Math.min(time, DUR[mode] - 1e-6);
  const P = pose(mode, t), g = GRID;
  g.clear();
  longHair(g, P);
  const back = arm(g, P, false);
  hand(g, back);
  legs(g, P);
  hood(g, P);
  jacket(g, P);
  let gem, front;
  if (P.staffBehind) (front = arm(g, P, true)), (gem = staff(g, P, front));
  head(g, P);
  if (!P.staffBehind) (front = arm(g, P, true)), (gem = staff(g, P, front));
  hand(g, front);
  // colored line art: each outline pixel is a dark version of the color it wraps
  g.outline((x, y, nx, ny) => {
    const c = g.get(x - Math.sign(nx), y - Math.sign(ny)) || g.get(x - 1, y) || g.get(x + 1, y) || g.get(x, y - 1) || g.get(x, y + 1);
    return c ? mix(c, C.line, 0.72) : C.line;
  });
  g.draw(p);
  if (P.gemGlow > 0.05) p.glow(gem[0], gem[1], 8 + 12 * P.gemGlow, C.gem, 0.35 * P.gemGlow);
}
