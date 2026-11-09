using System;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 맵 퍼즐: 봉인문. 바닥부터 천장 위까지 길을 막는 마력 벽이고, 문 색(가운데 문양)과 같은 속성의 B 마법을 맞혀야 열린다.
    // 다른 속성이나 평타는 튕겨 나가며 안내 문구만 띄운다. 연 문은 ChunkStreamer가 기록해서 청크를 다시 만들어도 열린 채로 둔다.
    // 마나는 계속 재생되므로 잘못 쏘거나 영창을 틀려도 기다리면 다시 시도할 수 있다 (갇히지 않는다).
    public class ElementGate : MonoBehaviour, IElementReceiver
    {
        private const float HintCooldown = 1.5f;
        private const float SigilHeight = 2.2f;

        private Element required;
        private Action onOpened;
        private bool opened;
        private float nextHintTime;

        public Element Required => required;

        // bottom: 문이 선 칸의 바닥 가운데 (월드). alreadyOpen이면 아무것도 만들지 않고 null.
        public static ElementGate Create(Transform parent, Vector2 bottom, Element required, bool alreadyOpen, Action onOpened)
        {
            if (alreadyOpen) return null;

            float height = ChunkData.Height - bottom.y + 2f; // 천장 위까지 막아서 점프로 넘지 못하게
            var go = new GameObject("ElementGate");
            go.transform.SetParent(parent, false);
            go.transform.position = bottom;
            go.layer = TrapCommon.GroundLayer; // 마법 투사체는 Ground 레이어에 맞는다

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, height);
            collider.offset = new Vector2(0f, height / 2f);

            var gate = go.AddComponent<ElementGate>();
            gate.required = required;
            gate.onOpened = onOpened;

            var color = ElementInfo.Color(required);
            var frames = TrapCommon.Frames("RuneGate");
            var barrier = EffectLibrary.CreateVisual(go.transform, "Visual", frames, 10f, true, new Color(color.r, color.g, color.b, 0.6f), 1f, 2);
            barrier.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            if (frames == null) barrier.transform.localScale = new Vector3(0.8f, height, 1f);
            else
            {
                barrier.color = new Color(color.r, color.g, color.b, 0.85f);
                barrier.transform.localScale = new Vector3(1f, height / 3f, 1f); // 그림은 1 × 3유닛
            }

            // 문양 그림이 없으면 속성 색 사각형으로 대신한다.
            TrapCommon.AddSprite(go.transform, "Sigil", TrapCommon.Frame($"RuneSigil_{required}"),
                new Vector2(0f, SigilHeight), new Vector2(0.7f, 0.7f), color, 3);
            return gate;
        }

        public void ReceiveElement(Element element)
        {
            if (opened) return;

            if (element != required)
            {
                PostHint($"봉인이 튕겨냈다! {ElementInfo.KoreanName(required)} 속성 마법만 통한다");
                return;
            }

            opened = true;
            var center = transform.position + Vector3.up * SigilHeight;
            EffectLibrary.PlayHit(required, center, 2f);
            GameAudio.PlaySfx($"Hit_{required}", 0.9f);
            StageEvents.PostNotice("봉인이 풀렸다!");
            onOpened?.Invoke();
            Destroy(gameObject);
        }

        // 플레이어가 문에 부딪히면 어떤 속성이 필요한지 알려 준다.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (opened || TrapCommon.PlayerFrom(collision.collider) == null) return;
            PostHint($"{ElementInfo.KoreanName(required)} 속성 마법(B)으로 봉인을 풀어라");
        }

        private void PostHint(string text)
        {
            if (Time.time < nextHintTime) return;
            nextHintTime = Time.time + HintCooldown;
            StageEvents.PostNotice(text);
        }
    }
}
