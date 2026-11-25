// I Blame You footstep puff: a cute little cloud kicked back off the heel on each step of a run.
// Round cream puffs with a darker underside pop out behind (-x), drift up and shrink away. Faces right (puffs go left).
import { hex, fit, seg, ease, lerp, rng } from "../../core/index.js";

export const meta = {
  name: "Step puff",
  desc: "A small round dust puff kicked back off the heel: three soft cream balls pop out behind, rise a little and shrink away.",
  tags: ["dust", "step", "run", "puff"],
  size: [48, 32],
  origin: [32, 28],
  duration: 0.45,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#6e5a48", "#a8907a", "#d8c6b0", "#f4ebde", "#ffffff"], seed: 1 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 5), D = meta.duration;
  const puffs = [[-3, -2, 3.2, 0], [-8, -4, 2.6, 0.04], [-12, -2, 2, 0.08]];
  puffs.forEach(([px, py, r0, delay], i) => {
    const r = rng(o.seed, i, 3), u = seg(t, delay, D);
    if (u <= 0 || u >= 1) return;
    const grow = ease.outBack(Math.min(1, u * 4)), shrink = 1 - ease.inQuad(seg(u, 0.35, 1));
    const rad = r0 * grow * shrink * r.range(0.9, 1.15);
    if (rad < 0.6) return;
    const x = px * lerp(0.5, 1.2, ease.outCubic(u)) + r.range(-0.5, 0.5), y = py - 3 * ease.outCubic(u);
    // dark underside, a cream body, a bright top-left
    p.circle(x + 0.4, y + 0.6, rad, C[1]);
    p.circle(x, y, rad - 0.2, (d, ang) => (d > 0.55 && ang > 0.3 && ang < 2.8 ? C[2] : d < 0.45 && ang < -1 && ang > -2.6 ? C[4] : C[3]));
  });
  // a couple of grit specks
  for (let i = 0; i < 2; i++) {
    const r = rng(o.seed, i, 9), u = seg(t, 0, 0.3);
    if (u <= 0 || u >= 1) continue;
    p.dot(-2 - r.range(4, 10) * u, -1 - r.range(3, 7) * u + 8 * u * u, C[0], 1 - u);
  }
}
