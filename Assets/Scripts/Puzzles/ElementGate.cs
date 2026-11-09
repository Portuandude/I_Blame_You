using System;
using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 맵 퍼즐: 봉인문. 바닥부터 천장 위까지 길을 막는 마력 벽이고, 문 색(가운데 문양)과 같은 속성의 B 마법을 맞혀야 열린다.
    // 다른 속성이나 평타는 튕겨 나가며 안내 문구만 띄운다. 연 문은 ChunkStreamer가 기록해서 청크를 다시 만들어도 열린 채로 둔다.
    // 마나는 계속 재생되므로 잘못 쏘거나 영창을 틀려도 기다리면 다시 시도할 수 있다 (갇히지 않는다).
    // 두 속성 봉인문: 문양이 위아래로 둘이고 아래 문양부터 순서대로 맞힌다. 하나를 맞히면 그 문양이 꺼지고 벽 색이 다음 속성으로 바뀐다.
    // 틀려도 진행은 초기화되지 않는다. 맞힌 개수는 ChunkStreamer가 기록해서 청크를 다시 만들어도 이어진다.
    public class ElementGate : MonoBehaviour, IElementReceiver
    {
        private const float HintCooldown = 1.5f;
        private static readonly List<ElementGate> Active = new List<ElementGate>();
        private const float SigilHeight = 2.2f;
        private const float SigilSpacing = 1f;

        private Element[] sequence;
        private int step;
        private SpriteRenderer barrier;
        private SpriteRenderer[] sigils;
        private Action onOpened;
        private Action<int> onStep;
        private bool opened;
        private float nextHintTime;

        public Element Required => sequence[step];

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        // 벽에 닿지 않는 마법(땅 가시: 바닥에서 솟음)을 문 앞에서 썼을 때 안내한다.
        // from에서 facing 쪽으로 range 안, 높이 차 6칸 이내에 닫힌 문이 있으면 그 문이 받은 것으로 친다.
        public static void NotifyNearby(Vector2 from, float facing, float range, Element element)
        {
            foreach (var gate in Active)
            {
                if (gate == null || gate.opened) continue;
                float dx = (gate.transform.position.x - from.x) * Mathf.Sign(facing);
                if (dx < 0f || dx > range || Mathf.Abs(from.y - gate.transform.position.y) > 6f) continue;
                gate.ReceiveElement(element);
                return;
            }
        }

        // bottom: 문이 선 칸의 바닥 가운데 (월드). alreadyOpen이면 아무것도 만들지 않고 null.
        // elements: 맞혀야 하는 속성 순서 (1~2개). startStep: 이미 맞힌 개수 (청크를 다시 만들 때), onStep: 하나 맞힐 때마다 새 개수.
        public static ElementGate Create(Transform parent, Vector2 bottom, Element[] elements, bool alreadyOpen, Action onOpened,
            int startStep = 0, Action<int> onStep = null)
        {
            if (alreadyOpen || elements == null || elements.Length == 0) return null;
            startStep = Mathf.Clamp(startStep, 0, elements.Length - 1);
            var required = elements[startStep];

            float height = ChunkData.Height - bottom.y + 2f; // 천장 위까지 막아서 점프로 넘지 못하게
            var go = new GameObject("ElementGate");
            go.transform.SetParent(parent, false);
            go.transform.position = bottom;
            go.layer = TrapCommon.GroundLayer; // 마법 투사체는 Ground 레이어에 맞는다

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, height);
            collider.offset = new Vector2(0f, height / 2f);

            var gate = go.AddComponent<ElementGate>();
            gate.sequence = elements;
            gate.step = startStep;
            gate.onOpened = onOpened;
            gate.onStep = onStep;

            var color = ElementInfo.Color(required);
            var frames = TrapCommon.Frames("RuneGate");
            var barrier = gate.barrier = EffectLibrary.CreateVisual(go.transform, "Visual", frames, 10f, true, new Color(color.r, color.g, color.b, 0.6f), 1f, 2);
            barrier.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            if (frames == null) barrier.transform.localScale = new Vector3(0.8f, height, 1f);
            else
            {
                barrier.color = new Color(color.r, color.g, color.b, 0.85f);
                barrier.transform.localScale = new Vector3(1f, height / 3f, 1f); // 그림은 1 × 3유닛
            }

            // 문양 그림이 없으면 속성 색 사각형으로 대신한다. 순서대로 아래에서 위로.
            gate.sigils = new SpriteRenderer[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                gate.sigils[i] = TrapCommon.AddSprite(go.transform, $"Sigil_{i}", TrapCommon.Frame($"RuneSigil_{elements[i]}"),
                    new Vector2(0f, SigilHeight + i * SigilSpacing), new Vector2(0.7f, 0.7f), ElementInfo.Color(elements[i]), 3);
                if (i < startStep) Dim(gate.sigils[i]);
            }
            return gate;
        }

        // 맞힌 문양은 어둡게 꺼진다.
        private static void Dim(SpriteRenderer sigil)
        {
            if (sigil != null) sigil.color = new Color(sigil.color.r * 0.4f, sigil.color.g * 0.4f, sigil.color.b * 0.4f, 0.5f);
        }

        private static string Names(Element[] elements, int from)
        {
            var names = new string[elements.Length - from];
            for (int i = from; i < elements.Length; i++) names[i - from] = ElementInfo.KoreanName(elements[i]);
            return string.Join(" → ", names);
        }

        public void ReceiveElement(Element element)
        {
            if (opened) return;

            var required = sequence[step];
            if (element != required)
            {
                PostHint($"봉인이 튕겨냈다! {ElementInfo.KoreanName(required)} 속성 마법만 통한다");
                return;
            }

            var center = transform.position + Vector3.up * (SigilHeight + step * SigilSpacing);
            EffectLibrary.PlayHit(required, center, 2f);
            GameAudio.PlaySfx($"Hit_{required}", 0.9f);

            step++;
            if (step < sequence.Length)
            {
                // 맞힌 문양은 꺼지고, 벽 색이 다음 속성으로 바뀐다.
                Dim(sigils[step - 1]);
                onStep?.Invoke(step);
                if (barrier != null)
                {
                    var next = ElementInfo.Color(sequence[step]);
                    barrier.color = new Color(next.r, next.g, next.b, barrier.color.a);
                }
                nextHintTime = 0f;
                PostHint($"봉인 하나가 풀렸다! 다음은 {ElementInfo.KoreanName(sequence[step])} 속성");
                return;
            }

            opened = true;
            StageEvents.PostNotice("봉인이 풀렸다!");
            onOpened?.Invoke();
            Destroy(gameObject);
        }

        // 플레이어가 문에 부딪히면 어떤 속성이 필요한지 알려 준다.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (opened || TrapCommon.PlayerFrom(collision.collider) == null) return;
            string order = sequence.Length - step > 1 ? $"{Names(sequence, step)} 순서로" : $"{ElementInfo.KoreanName(sequence[step])} 속성";
            PostHint($"{order} 마법(B)을 맞혀 봉인을 풀어라");
        }

        private void PostHint(string text)
        {
            if (Time.time < nextHintTime) return;
            nextHintTime = Time.time + HintCooldown;
            StageEvents.PostNotice(text);
        }
    }
}
