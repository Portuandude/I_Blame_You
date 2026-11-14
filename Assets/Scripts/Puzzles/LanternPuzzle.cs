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
        private Action onSolved;

        private static readonly Color MuralStone = new Color(0.36f, 0.33f, 0.42f, 1f);
        private static readonly Color MuralLine = new Color(0.62f, 0.58f, 0.7f, 1f);
        private static readonly Color MuralLit = new Color(1f, 0.86f, 0.45f, 1f);

        public bool Solved => solved;

        // muralBottom: 벽화 가운데 아래 (월드, 바닥). solved면 처음부터 모두 켜진 채로.
        public static LanternPuzzle Create(Transform parent, IList<LanternSpec> specs, Vector2 muralBottom, bool alreadySolved, Action onSolved)
        {
            var go = new GameObject("LanternPuzzle");
            go.transform.SetParent(parent, false);
            var puzzle = go.AddComponent<LanternPuzzle>();
            puzzle.onSolved = onSolved;
            puzzle.solved = alreadySolved;

            var sorted = new List<LanternSpec>(specs);
            sorted.Sort((a, b) => a.Order.CompareTo(b.Order));
            puzzle.BuildMural(go.transform, muralBottom, sorted);

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
