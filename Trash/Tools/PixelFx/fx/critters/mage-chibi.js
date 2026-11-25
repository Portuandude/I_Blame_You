// I Blame You player character: a chibi mage (big head, short body, floppy wizard hat), drawn for a cute
// side-scrolling MMO look and kept gender-neutral (round bob with bangs, the same robe for everyone).
// Faces right; feet on y = 0. One file draws every animation the game needs: o.mode picks it, time runs 0..duration.
import { hex, mix, clamp, seg, ease, lerp, TAU } from "../../core/index.js";
import { Grid } from "./_lib.js";

export const meta = {
  name: "Chibi mage",
  desc: "The game's chibi mage: idle breathe, walk, a bouncy run, rise, fall, dash, staff cast and hurt flinch. Gender-neutral bob and robe.",
  tags: ["character", "chibi", "mage", "sprite"],
  size: [96, 72],
  origin: [48, 64],
  duration: 1,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { mode: "idle", seed: 1 },
};

const H = (s) => hex(s);
const C = {
  ink: H("#2b1d33"),
  skin: H("#ffe3cc"), skinS: H("#f4bc9c"), blush: H("#ff9cb0"),
  hair: H("#7a4e3a"), hairL: H("#a26c4e"), hairH: H("#cf9a6c"),
  hat: H("#3d3a92"), hatL: H("#5b57c4"), hatH: H("#8a86ec"), band: H("#ffc94a"), bandS: H("#d4962a"),
  robe: H("#4f86d9"), robeL: H("#79aef0"), robeS: H("#3762ad"), trim: H("#fff0b0"), trimS: H("#e3c46a"),
  leg: H("#3a3150"), shoe: H("#8a5130"), shoeL: H("#b0703e"),
  wood: H("#8e5c2c"), woodL: H("#b47c40"), gem: H("#7af0ff"), gemL: H("#e8ffff"), gemS: H("#2fb0d8"),
  eye: H("#2b1d33"), iris: H("#6a4fb0"), eyeL: H("#ffffff"),
};

const GRID = new Grid(-26, -48, 52, 52);
const DUR = { idle: 1.4, walk: 8 / 14, run: 8 / 20, rise: 0.2, fall: 0.2, dash: 0.2, attack: 0.5, hurt: 0.2 };
export const durations = DUR;

// a pose: everything the drawing needs. Units are pixels at 1x; y grows downward.
function base() {
  return {
    bob: 0,          // whole upper body (robe, head, hat) up/down
    lean: 0,         // upper body slides forward (+x) as it leans
    tilt: 0,         // head tilt in px at the top of the head
    legs: [[-3, 0, 0], [2, 0, 0]], // back, front: [foot x, foot lift (px up), knee bend]
    armB: 0.3, armF: 0.9,           // arm swing angle (rad, 0 = down, + = forward)
    staff: 0.45,     // staff angle (rad from upright, + = tipped forward)
    staffUp: 0,      // staff raised (px)
    hatLag: 0,       // hat tip flops back (+ = more)
    squash: 0,       // + = squashed (wider, shorter)
    eyes: "open",    // open | closed | hurt | blink
    robeFlare: 0,    // robe hem flicks back
    gemGlow: 0,
  };
}

function pose(mode, t) {
  const P = base(), D = DUR[mode] || 1, u = (t % D) / D, ph = u * TAU;
  if (mode === "idle") {
    const b = Math.sin(ph);
    P.bob = b > 0.3 ? -1 : 0;
    P.hatLag = 0.6 + 0.6 * Math.sin(ph + 0.8);
    P.armF = 0.9 + 0.05 * b;
    P.eyes = u > 0.86 && u < 0.93 ? "blink" : "open";
    P.gemGlow = 0.3 + 0.2 * Math.sin(ph * 2);
  } else if (mode === "walk") {
    const s = Math.sin(ph);
    P.legs = [[-1 - 3 * s, Math.max(0, Math.sin(ph + Math.PI)) * 2, 0], [1 + 3 * s, Math.max(0, s) * 2, 0]];
    P.bob = Math.abs(Math.cos(ph)) > 0.7 ? 0 : -1;
    P.armB = 0.25 - 0.35 * s;
    P.armF = 0.9 + 0.3 * s;
    P.hatLag = 1 + 0.8 * Math.abs(s);
    P.lean = 1;
    P.staff = 0.5 + 0.1 * s;
  } else if (mode === "run") {
    // a bouncy, toddling run: little hops each step, legs paddling, arms pumping, hat flopping behind
    const s = Math.sin(ph), hop = Math.abs(Math.sin(ph));
    P.legs = [[-2 - 4 * s, Math.max(0, -s) * 4, 1], [2 + 4 * s, Math.max(0, s) * 4, 1]];
    P.bob = -Math.round(hop * 3);
    P.squash = hop < 0.25 ? 0.5 : 0;
    P.lean = 2;
    P.tilt = 1;
    P.armB = -0.2 - 0.9 * s;
    P.armF = 1.0 + 0.6 * s;
    P.hatLag = 2.5 + 1.2 * hop;
    P.robeFlare = 1 + hop;
    P.staff = 0.8 + 0.15 * s;
  } else if (mode === "rise") {
    P.legs = [[-3, 3, 1], [3, 2, 1]];
    P.bob = -1;
    P.armB = -1.2; P.armF = 1.9;
    P.hatLag = 3;
    P.staff = 0.6; P.staffUp = 2;
  } else if (mode === "fall") {
    P.legs = [[-4, 1, 0], [3, 0, 0]];
    P.armB = -2.2; P.armF = 2.3;
    P.hatLag = -1.5;
    P.robeFlare = -1;
    P.staff = 0.4; P.staffUp = 3;
    P.eyes = "closed";
  } else if (mode === "dash") {
    const k = ease.outCubic(clamp(u * 1.6));
    P.lean = Math.round(2 + 3 * k);
    P.tilt = 2;
    P.bob = 1;
    P.legs = [[-6, 2, 1], [4, 1, 1]];
    P.armB = -1.6; P.armF = 1.2;
    P.hatLag = 4;
    P.robeFlare = 3;
    P.staff = 1.25;
    P.squash = 0.3;
  } else if (mode === "attack") {
    // wind the staff back, then swing it out in front with the gem flaring
    const w = seg(u, 0, 0.35), s = ease.outBack(seg(u, 0.35, 0.6)), r = seg(u, 0.75, 1);
    P.staff = lerp(lerp(0.15, -0.7, ease.inOutSine(w)), 1.35, s) * (1 - r) + 0.15 * r;
    P.staffUp = Math.round(3 * (1 - r) * Math.max(w, s));
    P.armF = lerp(lerp(0.4, -0.6, w), 1.6, s) * (1 - r) + 0.4 * r;
    P.armB = 0.3 - 0.4 * s * (1 - r);
    P.lean = Math.round(lerp(-1 * w, 2, s) * (1 - r));
    P.legs = [[-4, 0, 0], [3, 0, 0]];
    P.bob = s > 0.5 && r < 0.5 ? 1 : 0;
    P.hatLag = 1 + 2 * s * (1 - r);
    P.gemGlow = s * (1 - r);
    P.eyes = u > 0.3 && u < 0.7 ? "closed" : "open";
    P.staffBehind = u < 0.36; // wound back over the shoulder: behind the head
  } else if (mode === "hurt") {
    P.lean = -2;
    P.tilt = -2;
    P.legs = [[-4, 0, 0], [1, 1, 0]];
    P.armB = -1.4; P.armF = -0.8;
    P.hatLag = -2;
    P.staff = -0.5;
    P.eyes = "hurt";
    P.staffBehind = true;
    P.squash = u < 0.5 ? 0.4 : 0;
  }
  return P;
}

// ---------- parts ----------

function legs(g, P) {
  // back leg first (a shade darker), then the front one. Short stubby legs and round shoes.
  P.legs.forEach(([fx, lift, bend], i) => {
    const front = i === 1, x = Math.round(fx), y = -Math.round(lift);
    const legC = front ? C.leg : mix(C.leg, C.ink, 0.35);
    const hipX = front ? 1 : -2, hipY = -4 + P.bob;
    g.line(hipX, hipY, x + (bend ? -1 : 0), y - 2, legC);
    g.line(hipX + 1, hipY, x + 1 + (bend ? -1 : 0), y - 2, legC);
    // shoe: 4 wide, toe to the front
    const sc = front ? C.shoe : mix(C.shoe, C.ink, 0.3);
    for (let dx = -1; dx <= 2; dx++) g.set(x + dx, y - 1, sc);
    for (let dx = -1; dx <= 3; dx++) g.set(x + dx, y, sc);
    g.set(x, y - 1, front ? C.shoeL : sc);
  });
}

function robe(g, P) {
  // a little bell-shaped robe with a trimmed hem and a white collar
  const top = -12 + P.bob, bot = -3 + P.bob + Math.round(P.squash), lx = P.lean;
  for (let y = top; y <= bot; y++) {
    const v = (y - top) / (bot - top);
    const hw = lerp(3.5, 6.5, Math.pow(v, 0.8)) + P.squash * 1.5;
    const flare = Math.round(-P.robeFlare * v * v);
    const x0 = Math.round(lx * (1 - v) - hw + flare), x1 = Math.round(lx * (1 - v) + hw + flare * 0.3);
    for (let x = x0; x <= x1; x++) {
      let c = C.robe;
      if (x <= x0 + 1) c = C.robeL;
      if (x >= x1 - 1) c = C.robeS;
      if (y >= bot - 1) c = y === bot ? C.trimS : C.trim;
      g.set(x, y, c);
    }
  }
  // collar and a gold clasp
  for (let x = -3; x <= 3; x++) g.set(lx + x, top, C.trim);
  g.set(lx + 1, top + 1, C.band);
  g.set(lx + 1, top + 3, C.band);
  // belt-ish sash
  for (let x = -4; x <= 4; x++) g.set(lx + x + (x > 2 ? 0 : 0), top + 5, C.robeS);
}

function head(g, P) {
  const cx = P.lean + 1, cy = -20 + P.bob + Math.round(P.squash * 0.5), tilt = P.tilt;
  // face
  g.ellipse(cx, cy, 9.5, 8.5, (nx, ny) => (nx > 0.6 || ny > 0.66 ? C.skinS : C.skin));
  // hair: a round bob framing the face, bangs over the forehead, a cowlick
  g.shade(cx - 11, cy - 10, cx + 11, cy + 7, (x, y) => {
    const nx = (x - cx - tilt * 0.3) / 10.2, ny = (y - cy) / 9.2;
    if (nx * nx + ny * ny > 1) return 0;
    const bangs = y < cy - 3 + (((Math.floor(x - cx) % 3) + 3) % 3 === 0 ? 1 : 0) + (x - cx > 3 ? 1 : 0);
    const sides = (x < cx - 6 || x > cx + 8) && y < cy + 5;
    if (!bangs && !sides) return 0;
    if (y < cy - 6 && x < cx - 1) return C.hairH;
    return x > cx + 3 || y > cy + 1 ? C.hair : C.hairL;
  });
  g.set(cx - 1, cy - 10, C.hair); g.set(cx - 2, cy - 11, C.hair);
  // face: big round eyes looking forward, a blush, a tiny mouth
  const ey = cy + 1, ex = [cx - 3, cx + 3];
  for (const x of ex) {
    if (P.eyes === "open") {
      // big glossy eyes: dark top, a lighter iris at the bottom, a white catch-light
      for (let dy = -2; dy <= 1; dy++) g.set(x, ey + dy, C.eye), g.set(x + 1, ey + dy, dy === 1 ? C.iris : C.eye);
      g.set(x, ey + 1, C.iris);
      g.set(x + 1, ey - 2, C.eyeL); g.set(x, ey - 1, C.eyeL);
    } else if (P.eyes === "blink" || P.eyes === "closed") {
      g.set(x, ey + 1, C.eye); g.set(x + 1, ey + 1, C.eye);
    } else {
      // hurt: > <
      const d = x === ex[0] ? 1 : -1;
      g.set(x + (d > 0 ? 0 : 1), ey - 1, C.eye); g.set(x + (d > 0 ? 1 : 0), ey, C.eye); g.set(x + (d > 0 ? 0 : 1), ey + 1, C.eye);
    }
  }
  g.set(cx - 5, ey + 3, C.blush); g.set(cx - 4, ey + 3, C.blush);
  g.set(cx + 5, ey + 3, C.blush); g.set(cx + 6, ey + 3, C.blush);
  if (P.eyes === "hurt") g.set(cx, ey + 4, C.eye), g.set(cx + 1, ey + 4, C.eye);
  else g.set(cx, ey + 4, C.skinS), g.set(cx + 1, ey + 4, C.skinS);
  return { cx, cy };
}

function hat(g, P, h) {
  const cx = h.cx + P.tilt * 0.4, by = h.cy - 8;
  // brim
  g.ellipse(cx, by, 12.5, 2.4, (nx, ny) => (ny < -0.2 ? C.hatL : nx > 0.6 ? C.hat : C.hatL));
  // cone: from the brim up, bending back at the tip; the tip flops with hatLag
  const lag = P.hatLag;
  const pts = [];
  for (let i = 0; i <= 12; i++) {
    const v = i / 12;
    const x = cx + v * v * (-4 - lag * 1.4) + (v > 0.8 ? -(v - 0.8) * 10 * (1 + lag * 0.15) : 0);
    const y = by - 1 - v * 12 + (v > 0.8 ? (v - 0.8) * 12 : 0);
    pts.push([x, y]);
  }
  g.tube(pts, (v) => lerp(6.5, 0.9, v), (v, d) => (d > 0.55 && v < 0.85 ? (pts[0][0] < cx ? C.hat : C.hat) : v > 0.7 ? C.hatL : d < 0.3 ? C.hatH : C.hatL));
  // band and a star
  for (let x = -6; x <= 6; x++) g.set(cx + x, by - 2, C.band), g.set(cx + x, by - 1, C.bandS);
  g.set(cx + 2, by - 4, C.band); g.set(cx + 1, by - 5, C.band); g.set(cx + 3, by - 5, C.band); g.set(cx + 2, by - 6, C.band);
  g.set(cx + 2, by - 5, H("#fff6c8"));
}

function arm(g, P, front, h) {
  const sx = P.lean + (front ? 4 : -3), sy = -11 + P.bob;
  const a = front ? P.armF : P.armB, L = 5;
  const hx = sx + Math.sin(a) * L, hy = sy + Math.cos(a) * L;
  const col = front ? C.robe : C.robeS;
  g.tube([[sx, sy], [hx, hy]], 1.3, col);
  g.ellipse(hx, hy, 1.4, 1.4, front ? C.skin : C.skinS);
  return [hx, hy];
}

function staff(g, P, hand) {
  // held in the front hand: a short crooked wand-staff with a glowing gem
  const a = P.staff, len = 14, [hx, hy] = hand;
  const ux = Math.sin(a), uy = -Math.cos(a);
  const bx = hx - ux * 5, byy = hy - uy * 5 - P.staffUp * 0, tx = hx + ux * (len - 5), ty = hy + uy * (len - 5) - P.staffUp;
  g.line(bx, byy, tx, ty, C.wood);
  g.line(bx + 1, byy, tx + 1, ty, C.woodL);
  // curl at the top holding the gem
  g.set(tx - 1, ty - 1, C.wood); g.set(tx + 2, ty - 1, C.wood);
  g.ellipse(tx + 0.5, ty - 2.5, 2.2, 2.2, (nx, ny) => (nx < -0.2 && ny < -0.2 ? C.gemL : nx > 0.4 || ny > 0.4 ? C.gemS : C.gem));
  return [tx + 0.5, ty - 2.5];
}

export function draw(p, time, o) {
  const mode = DUR[o.mode] ? o.mode : "idle";
  const t = Math.min(time, DUR[mode] - 1e-6);
  const P = pose(mode, t), g = GRID;
  g.clear();
  arm(g, P, false);
  legs(g, P);
  robe(g, P);
  let gem;
  if (P.staffBehind) gem = staff(g, P, arm(g, P, true));
  const h = head(g, P);
  hat(g, P, h);
  if (!P.staffBehind) gem = staff(g, P, arm(g, P, true, h));
  g.outline((x, y, nx, ny) => (ny < 0 && nx <= 0 ? mix(C.ink, C.hat, 0.35) : C.ink));
  g.draw(p);
  // the gem's light
  if (P.gemGlow > 0.05) p.glow(gem[0], gem[1], 6 + 8 * P.gemGlow, C.gem, 0.35 * P.gemGlow);
}
