// I Blame You game slime: body only (no puddle, spores or floor glow) so it sits on any background as a sprite.
// Body shading (halfW, inside, body) is copied from slime-eyeless.js, itself pixelfx critters/slime.js, whose
// puddle recipe is adapted from Sprite Fusion's open-source poison puddle (https://www.spritefusion.com/pixel-art-vfx/poison),
// MIT License, Copyright (c) 2026 Hugo Duprez. Eyeless (game rule).
import { fit, seg, ease, lerp, rng, TAU } from "../../core/index.js";
import { Grid, spring } from "./_lib.js";

export const meta = {
  name: "Game slime",
  desc: "Eyeless jelly slime body for the game's animations: idle breathe, hopping run, a squash-and-lunge attack, a splat-and-melt death.",
  tags: ["critter", "slime", "monster", "sprite"],
  size: [112, 88],
  origin: [56, 72],
  duration: 0.8,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#061412", "#0b2f2b", "#12594a", "#1f8f4f", "#47c752", "#a6f06a", "#f0ffd2"], mode: "idle", seed: 1 },
};

const GRID = new Grid(-30, -64, 60, 66);
const W0 = 30, H0 = 19, VC = 0.4, G = 640;
const DUR = { idle: 0.8, run: 0.6, attack: 0.8, die: 0.8 };

function make(s, by, lean, round, melt = 0) {
  const H = H0 * (1 + s) * (1 - melt), W = (W0 / Math.sqrt(1 + s)) * (1 + 0.15 * melt);
  return { s, by, lean, round, q: -1, age: 1, W, H: Math.max(1.5, H) };
}

// the body's squash at time t for each animation: s > 0 tall, s < 0 flat; by lifts it; lean tips the top forward (+x)
function pose(mode, t) {
  if (mode === "idle") {
    const k = Math.sin((t / DUR.idle) * TAU);
    return make(0.06 * k, 0, 0.015 * Math.sin((t / DUR.idle) * TAU * 2), 0.1);
  }
  if (mode === "run") {
    // a bouncy little hop every cycle: squash, spring up leaning forward, land
    const u = t / DUR.run;
    if (u < 0.25) return make(-0.2 * ease.inOutSine(u / 0.25), 0, -0.04, 0.1);
    if (u < 0.75) {
      const q = (u - 0.25) / 0.5;
      return make(lerp(0.15, -0.02, q), 0, 0.14 * (1 - q), 0.1 + 0.6 * Math.sin(Math.PI * q));
    }
    const a = (u - 0.75) / 0.25;
    return make(-0.28 * (1 - a) + 0.04 * Math.sin(a * Math.PI), 0, 0.04 * (1 - a), 0.1);
  }
  if (mode === "attack") {
    // wind up low and back, burst forward into a stretched lunge, round out, splat on the target
    if (t < 0.3) { const u = ease.inOutSine(t / 0.3); return make(-0.32 * u + 0.02 * Math.sin(t * TAU * 18) * u, 0, -0.14 * u, 0.1); }
    if (t < 0.42) { const u = ease.outCubic(seg(t, 0.3, 0.42)); return make(lerp(-0.32, 0.15, u), 0, lerp(-0.14, 0.28, u), 0.1); }
    if (t < 0.6) { const u = seg(t, 0.42, 0.6); return make(lerp(0.15, 0.0, u), 0, lerp(0.28, 0.12, u), 0.1 + 0.7 * Math.sin(Math.PI * u)); }
    const a = t - 0.6;
    return make(a < 0.05 ? -0.35 : -0.35 * spring(a - 0.05, 2.4, 5), 0, 0.12 * Math.exp(-a * 8), 0.1);
  }
  // die: a last flinch up, a hard splat, then it melts into a puddle that dries away
  if (t < 0.12) return make(0.15 * Math.sin((t / 0.12) * Math.PI), 0, 0, 0.1);
  const m = ease.inQuad(seg(t, 0.16, 0.62));
  return make(-0.35, 0, 0, 0.1, m);
}

// half the body's width at height v (0 base .. 1 top): a dome over a soft foot with rounded corners
function halfW(v, P) {
  if (v < 0 || v > 1) return -1;
  const { W, H, round } = P;
  if (v >= VC) {
    const q = (v - VC) / (1 - VC);
    return (W / 2) * Math.sqrt(1 - q * q);
  }
  const q = (VC - v) / VC, hb = v * H, rc = 3;
  let hw = (W / 2) * Math.sqrt(Math.max(0, 1 - round * q * q));
  if (hb < rc) hw -= rc - Math.sqrt(rc * rc - (rc - hb) ** 2);
  return hw;
}
const inside = (P, x, y) => {
  const v = (P.by - 1 - y) / P.H, hw = halfW(v, P);
  return hw >= 0 && Math.abs(x - P.lean * v * P.H) <= hw;
};

// shaded the way a pixel artist shades jelly: a dark dome over light pooling low inside the body, a shadow
// crescent (the rim the body leaves bare when nudged toward the light), a glossy streak hugging the top-left
// contour with a hot spot under it, light bounced up off the floor caught just inside the lower rim
function body(g, C, P) {
  const yb = P.by - 1, half = P.W / 2 + 3;
  const cx = P.lean * 0.45 * P.H, cy = yb - P.H * 0.5, lx = cx - P.W * 0.24, ly = cy - P.H * 0.36;
  const lit = (x, y, k) => inside(P, lx + (x - lx) / k, ly + (y - ly) / k);
  g.clear();
  g.shade(-half, yb - P.H - 1, half, yb, (x, y, o) => {
    if (!inside(P, x, y)) return 0;
    o.t = 1;
    o.a = 0.92;
    let i = lit(x, y, 0.42) ? 5 : lit(x, y, 0.7) ? 4 : 3;
    // light pooled in the jelly along the bottom, brightest just inside the rim
    const v = (yb - y) / P.H;
    if (v < 0.3 && inside(P, x, y + 1.4)) i = !inside(P, x, y + 2.4) ? 5 : v < 0.2 ? 4 : i;
    if (!inside(P, x + 2, y + 2.4)) (i = 2), (o.a = 1);
    const ang = Math.atan2(y - cy, (x - cx) * (P.H / P.W));
    if (!inside(P, x - 1.6, y - 1.6) && inside(P, x - 0.7, y - 0.7) && ang > -2.75 && ang < -1.55) (i = ang > -2.45 && ang < -1.8 ? 6 : 5), (o.a = 1);
    if (i === 2 && inside(P, x + 1, y + 1.2) && !inside(P, x + 2, y + 2.4) && ang > 0.3 && ang < 1.5) i = 4;
    return C[i];
  });
  // the hot spot: a tiny bright window reflection under the streak
  const hx = Math.round(cx - P.W * 0.27), hy = Math.round(cy - P.H * 0.14);
  if (inside(P, hx - 1, hy) && inside(P, hx + 1, hy + 1)) g.set(hx, hy, C[6]), g.set(hx, hy + 1, C[5]);
}


// droplets flung on a splat (attack landing, death)
function drops(p, C, o, t0, t, n, spd) {
  const age = t - t0;
  if (age < 0 || age > 0.5) return;
  for (let i = 0; i < n; i++) {
    const r = rng(o.seed, i, 11), a = -Math.PI / 2 + r.range(-1.25, 1.25), sp = r.range(spd * 0.6, spd);
    const x = Math.cos(a) * sp * age + Math.sign(Math.cos(a)) * 8, y = -3 + Math.sin(a) * sp * age + 0.5 * G * age * age;
    if (y > -1) continue;
    const big = r.chance(0.4);
    p.px(x, y, big ? C[6] : C[5]);
    if (big) p.px(x - 1, y, C[4]), p.px(x, y + 1, C[3]);
  }
}

export function draw(p, time, o) {
  const mode = DUR[o.mode] ? o.mode : "idle", t = Math.min(time, DUR[mode] - 1e-6), C = fit(o.palette, 7);
  const P = pose(mode, t), g = GRID;
  const fade = mode === "die" ? 1 - seg(t, 0.6, 0.78) : 1;
  if (fade <= 0) return;
  body(g, C, P);
  g.outline((x, y, nx, ny) => (ny < 0 && nx <= 0 && y < P.by - 1 - P.H * 0.5 ? C[1] : C[0]));
  g.draw(p, fade); // the dried puddle dithers away
  if (mode === "attack") drops(p, C, o, 0.6, t, 8, 90);
  if (mode === "die") drops(p, C, o, 0.12, t, 12, 120);
  p.glow(P.lean * P.H * 0.4, P.by - P.H * 0.5, 18 + P.W * 0.3, C[4], 0.12 * fade);
}

export function durationFor(mode) { return DUR[mode] || DUR.idle; }
