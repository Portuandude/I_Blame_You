using System;
using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 퍼즐 방 "등불 순서" (2026-11-14, Docs/PuzzleRoomDesign.md 3절). 몹 없는 퍼즐 방의 추리 퍼즐.
    // - 등불 셋: 높이가 다른 자리(바닥·가운데 발판·높은 발판)에 하나씩. 머리의 문양 색 = 켜는 속성. 그 속성 B 마법에만 켜진다.
    // - 벽화: 켜는 순서대로 왼쪽부터 작은 등불 그림 셋을 "높이"로 그린다 (색 없음). 그림 아래 점 1·2·3개.
    //   → 벽화 첫 그림이 가장 높으면 높은 발판 등불(그 문양 색 속성)을 먼저 켠다. 높이로 속성을 알아내는 추리.
    // - 틀린 속성·평타: "팅" 튕김 (진행 그대로). 차례가 아닌 등불을 맞는 속성으로 맞히면 켜진 등불이 모두 꺼진다 (김 빠지는 소리 + 연기).
    // - 셋 다 켜면 onSolved (ChunkStreamer가 유물 상자를 떨군다). 풀다 만 진행은 저장하지 않는다 (다시 오면 처음부터).
    // 안내 문구는 없다 (2026-11-12 사람 지시): 모양·빛·소리로만 알린다. 문을 막지 않으므로 못 풀어도 지나갈 수 있다.
    public class LanternPuzzle : MonoBehaviour
    {
        public struct LanternSpec
        {
            public Vector2 Bottom; // 등불이 선 칸의 바닥 가운데 (월드)
            public Element Element;
            public int Order;      // 켜는 순번 0~2
            public int Rise;       // 바닥에서 선 칸까지 높이 (벽화 그림 높이에 쓴다)
        }

        private readonly List<LanternLight> lanterns = new List<LanternLight>();
        private readonly List<SpriteRenderer> muralHeads = new List<SpriteRenderer>(); // 순번 순서
        private int step;
        private bool solved;
        private bool mirror;
        private Action onSolved;

        private static readonly Color MuralStone = new Color(0.36f, 0.33f, 0.42f, 1f);
        private static readonly Color MuralLine = new Color(0.62f, 0.58f, 0.7f, 1f);
        private static readonly Color MuralLit = new Color(1f, 0.86f, 0.45f, 1f);

        public bool Solved => solved;

        // muralBottom: 벽화(거울이면 거울) 가운데 아래 (월드, 바닥). solved면 처음부터 모두 켜진 채로.
        // mirror: "거울 룬" — 벽화 대신 높이 걸린 거울이 켜는 순서를 좌우 뒤집힌 자리의 그림으로 차례로 깜빡여 보인다.
        public static LanternPuzzle Create(Transform parent, IList<LanternSpec> specs, Vector2 muralBottom, bool alreadySolved, Action onSolved,
            bool mirror = false)
        {
            var go = new GameObject("LanternPuzzle");
            go.transform.SetParent(parent, false);
            var puzzle = go.AddComponent<LanternPuzzle>();
            puzzle.onSolved = onSolved;
            puzzle.solved = alreadySolved;
            puzzle.mirror = mirror;

            var sorted = new List<LanternSpec>(specs);
            sorted.Sort((a, b) => a.Order.CompareTo(b.Order));
            if (mirror) puzzle.BuildMirror(go.transform, muralBottom, sorted);
            else puzzle.BuildMural(go.transform, muralBottom, sorted);

            foreach (var spec in specs)
            {
                var lantern = LanternLight.Create(go.transform, spec, puzzle);
                puzzle.lanterns.Add(lantern);
                if (alreadySolved) lantern.SetLit(true, false);
            }

            if (alreadySolved)
            {
                puzzle.step = puzzle.lanterns.Count;
                foreach (var head in puzzle.muralHeads) head.color = MuralLit;
            }

            return puzzle;
        }

        // 벽화: 어두운 돌판(3 × 3.4유닛) 위에 순번 순서대로 왼쪽부터 등불 그림. 그림 높이 = 실제 등불 높이(바닥 0 · 2 · 4칸)를 0.55배로 줄인 것.
        private const float MuralWidth = 3.2f;
        private const float MuralHeight = 3.6f;
        private const float MuralBase = 0.5f;     // 돌판 아래 끝 (바닥 위)
        private const float MuralIconScale = 0.5f; // 실제 높이 → 그림 높이
        private const float MuralSpacing = 0.95f;

        private void BuildMural(Transform parent, Vector2 bottom, List<LanternSpec> byOrder)
        {
            var mural = new GameObject("Mural").transform;
            mural.SetParent(parent, false);
            mural.position = bottom;
            TrapCommon.AddSprite(mural, "Slab", null, new Vector2(0f, MuralBase + MuralHeight / 2f),
                new Vector2(MuralWidth, MuralHeight), MuralStone, -3);
            TrapCommon.AddSprite(mural, "Frame", null, new Vector2(0f, MuralBase + MuralHeight / 2f),
                new Vector2(MuralWidth + 0.2f, MuralHeight + 0.2f), MuralLine, -4);

            for (int k = 0; k < byOrder.Count; k++)
            {
                float x = (k - (byOrder.Count - 1) / 2f) * MuralSpacing;
                float baseY = MuralBase + 0.75f; // 점 줄 위
                float y = baseY + byOrder[k].Rise * MuralIconScale;
                // 받침 막대 (그림 바닥 줄부터 머리 밑까지) + 머리 원 (색 없음, 켜지면 금색)
                float postHeight = Mathf.Max(0.25f, y - baseY + 0.1f);
                TrapCommon.AddSprite(mural, $"Post_{k}", null, new Vector2(x, baseY + postHeight / 2f - 0.05f),
                    new Vector2(0.12f, postHeight), MuralLine, -2);
                var head = TrapCommon.AddSprite(mural, $"Head_{k}", null, new Vector2(x, y + 0.2f), new Vector2(0.38f, 0.38f), MuralLine, -1);
                head.sprite = SolidSpriteFactory.CreateCircle();
                muralHeads.Add(head);
                // 순번 점 (1·2·3개)
                for (int d = 0; d <= k; d++)
                {
                    float dx = (d - k / 2f) * 0.18f;
                    var dot = TrapCommon.AddSprite(mural, $"Pip_{k}_{d}", null, new Vector2(x + dx, MuralBase + 0.35f),
                        new Vector2(0.11f, 0.11f), MuralLine, -2);
                    dot.sprite = SolidSpriteFactory.CreateCircle();
                }
            }
        }

        // 거울: 바닥 위 MirrorRise 높이에 은빛 틀의 가로 판. 순번 순서대로 작은 등불 그림을 두되, 가로 자리는 실제 등불 자리를
        // 거울 가운데 기준으로 좌우 뒤집은 곳(줄여서). 그림은 모두 같은 높이·색 없음. 안 풀린 동안 순번대로 하얗게 깜빡이고 쉬기를 되풀이한다.
        private const float MirrorRise = 6.5f;
        private const float MirrorWidth = 4.6f;
        private const float MirrorPanelHeight = 1.7f;
        private const float MirrorSpread = 0.16f;  // 실제 가로 거리 → 거울 속 가로 거리
        private const float MirrorBeat = 0.8f;     // 깜빡임 사이
        private const float MirrorOn = 0.5f;       // 한 번 깜빡이는 동안
        private const float MirrorCycle = 4.6f;    // 셋을 보이고 쉬는 데까지
        private static readonly Color MirrorGlass = new Color(0.55f, 0.66f, 0.78f, 1f);
        private static readonly Color MirrorRim = new Color(0.86f, 0.9f, 0.96f, 1f);
        private readonly List<Vector3> mirrorHeadScale = new List<Vector3>();

        private void BuildMirror(Transform parent, Vector2 bottom, List<LanternSpec> byOrder)
        {
            var frame = new GameObject("Mirror").transform;
            frame.SetParent(parent, false);
            frame.position = bottom;
            float cy = MirrorRise + MirrorPanelHeight / 2f;
            TrapCommon.AddSprite(frame, "Rim", null, new Vector2(0f, cy), new Vector2(MirrorWidth + 0.25f, MirrorPanelHeight + 0.25f), MirrorRim, -4);
            TrapCommon.AddSprite(frame, "Glass", null, new Vector2(0f, cy), new Vector2(MirrorWidth, MirrorPanelHeight), MirrorGlass, -3);
            // 걸이 줄 둘 (천장 쪽으로)
            TrapCommon.AddSprite(frame, "CordL", null, new Vector2(-MirrorWidth * 0.35f, MirrorRise + MirrorPanelHeight + 0.9f), new Vector2(0.06f, 1.6f), MirrorRim, -4);
            TrapCommon.AddSprite(frame, "CordR", null, new Vector2(MirrorWidth * 0.35f, MirrorRise + MirrorPanelHeight + 0.9f), new Vector2(0.06f, 1.6f), MirrorRim, -4);

            foreach (var spec in byOrder)
            {
                float x = -(spec.Bottom.x - bottom.x) * MirrorSpread; // 좌우 뒤집기
                float baseY = MirrorRise + 0.3f;
                TrapCommon.AddSprite(frame, $"Post_{spec.Order}", null, new Vector2(x, baseY + 0.3f), new Vector2(0.1f, 0.6f), MuralLine, -2);
                var head = TrapCommon.AddSprite(frame, $"Head_{spec.Order}", null, new Vector2(x, baseY + 0.8f), new Vector2(0.36f, 0.36f), MuralLine, -1);
                head.sprite = SolidSpriteFactory.CreateCircle();
                muralHeads.Add(head);
                mirrorHeadScale.Add(head.transform.localScale);
            }
        }

        private void Update()
        {
            if (!mirror) return;
            float t = Mathf.Repeat(Time.time, MirrorCycle);
            int beat = Mathf.FloorToInt(t / MirrorBeat);
            bool on = !solved && t - beat * MirrorBeat < MirrorOn;
            for (int i = 0; i < muralHeads.Count; i++)
            {
                var head = muralHeads[i];
                if (head == null) continue;
                bool flash = on && i == beat;
                head.color = i < step ? MuralLit : flash ? Color.white : MuralLine;
                if (i < mirrorHeadScale.Count) head.transform.localScale = mirrorHeadScale[i] * (flash ? 1.35f : 1f);
            }
        }

        // 등불이 마법을 받았다.
        internal void Receive(LanternLight lantern, Element element)
        {
            if (solved) return;
            if (element != lantern.Element)
            {
                lantern.Deflect();
                return;
            }

            if (lantern.Lit) return;

            if (lantern.Order != step)
            {
                ResetAll(lantern);
                return;
            }

            lantern.SetLit(true, true);
            if (step < muralHeads.Count) muralHeads[step].color = MuralLit;
            step++;
            if (step < lanterns.Count) return;

            solved = true;
            foreach (var l in lanterns) EffectLibrary.PlayHit(l.Element, l.HeadPosition, 1.8f);
            GameAudio.PlaySfx("GateOpen", 0.9f, 0f);
            onSolved?.Invoke();
        }

        // 차례가 아닌 등불: 켜진 등불이 모두 꺼지고, 맞힌 등불에서도 연기가 난다.
        private void ResetAll(LanternLight wrong)
        {
            foreach (var l in lanterns)
                if (l.Lit || l == wrong) l.Snuff();
            foreach (var head in muralHeads) head.color = MuralLine;
            step = 0;
            GameAudio.PlaySfx("Fizzle", 0.8f);
        }
    }
}
