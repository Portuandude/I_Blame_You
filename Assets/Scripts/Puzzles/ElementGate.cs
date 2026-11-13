using System;
using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 맵 퍼즐: 봉인문. 바닥부터 천장 위까지 길을 막는 마력 벽이고, 문 색(가운데 문양)과 같은 속성의 B 마법을 맞혀야 열린다.
    // 다른 속성이나 평타는 튕겨 나가며 벽이 하얗게 번쩍인다 (안내 문구는 없다: 2026-11-12 사람 지시, 문양 색·방어막 거품으로만 알린다). 연 문은 ChunkStreamer가 기록해서 청크를 다시 만들어도 열린 채로 둔다.
    // 맞힘·튕겨냄·열림 연출(고리·"팅"·벽 조각·빛 기둥)은 ElementGateFx (그림·소리만).
    // 마나는 계속 재생되므로 잘못 쏘거나 영창을 틀려도 기다리면 다시 시도할 수 있다 (갇히지 않는다).
    // 두 속성 봉인문: 문양이 위아래로 둘이고 아래 문양부터 순서대로 맞힌다. 하나를 맞히면 그 문양이 꺼지고 벽 색이 다음 속성으로 바뀐다.
    // 틀려도 진행은 초기화되지 않는다. 맞힌 개수는 ChunkStreamer가 기록해서 청크를 다시 만들어도 이어진다.
    // 협동 봉인문(shieldFirst): 아래 문양은 A의 방어막 속성이다. A가 그 속성 방어막을 두른 채 문에 닿으면 풀리고, 그다음 위 문양은 B 마법으로 푼다.
    // 아래 문양 뒤에는 방어막 거품이 보여서 마법이 아니라 방어막으로 푸는 문양임을 알린다.
    public class ElementGate : MonoBehaviour, IElementReceiver
    {
        private const float DeflectCooldown = 0.4f;
        private const float DeflectFlash = 0.15f;
        private static readonly List<ElementGate> Active = new List<ElementGate>();
        private const float SigilHeight = 2.2f;
        private const float SigilSpacing = 1f;
        private const float ShieldCheckInterval = 0.1f;
        private const float ShieldTouchReach = 1.3f; // 문 가운데에서 플레이어 가운데까지 가로 거리 (문 반폭 0.5 + 몸 반폭 + 여유)
        private const float ShieldTouchHeight = 2.5f; // 문 바닥에서 이 높이까지만 본다 (윗 발판에서 켠 방어막은 닿은 것으로 치지 않음)

        private Element[] sequence;
        private int step;
        private SpriteRenderer barrier;
        private SpriteRenderer[] sigils;
        private Action onOpened;
        private Action<int> onStep;
        private bool opened;
        private bool shieldFirst;
        private SpriteRenderer shieldMark;
        private float nextShieldCheck;
        private float height;
        private float nextDeflectTime;

        public Element Required => sequence[step];
        private bool ShieldStep => shieldFirst && step == 0;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        // 벽에 닿지 않는 마법(땅 가시: 바닥에서 솟음)을 문 앞에서 썼을 때 튕겨내는 번쩍임을 보인다.
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

        // bottom: 문이 선 칸의 바닥 가운데 (월드). height: 막을 높이 (ChunkData.GateHeight, 방 안 좌표로 잰 칸 수).
        // alreadyOpen이면 아무것도 만들지 않고 null.
        // elements: 맞혀야 하는 속성 순서 (1~2개). startStep: 이미 맞힌 개수 (청크를 다시 만들 때), onStep: 하나 맞힐 때마다 새 개수.
        public static ElementGate Create(Transform parent, Vector2 bottom, float height, Element[] elements, bool alreadyOpen, Action onOpened,
            int startStep = 0, Action<int> onStep = null, bool shieldFirst = false)
        {
            if (alreadyOpen || elements == null || elements.Length == 0) return null;
            startStep = Mathf.Clamp(startStep, 0, elements.Length - 1);
            var required = elements[startStep];

            height = Mathf.Max(1f, height);
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
            gate.shieldFirst = shieldFirst && elements.Length > 1;
            gate.height = height;

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

            if (gate.shieldFirst && startStep == 0)
            {
                var c = ElementInfo.Color(elements[0]);
                gate.shieldMark = EffectLibrary.CreateVisual(go.transform, "SigilShield", EffectLibrary.ShieldFrames, 10f, true,
                    new Color(c.r, c.g, c.b, 0.5f), 1.1f, 2);
                gate.shieldMark.transform.localPosition = new Vector3(0f, SigilHeight, 0f);
                gate.shieldMark.color = new Color(c.r, c.g, c.b, 0.55f);
                if (EffectLibrary.ShieldFrames != null) gate.shieldMark.transform.localScale = Vector3.one * 0.7f;
            }
            return gate;
        }

        // 맞힌 문양은 어둡게 꺼진다.
        private static void Dim(SpriteRenderer sigil)
        {
            if (sigil != null) sigil.color = new Color(sigil.color.r * 0.4f, sigil.color.g * 0.4f, sigil.color.b * 0.4f, 0.5f);
        }

        public void ReceiveElement(Element element)
        {
            if (opened) return;

            var required = sequence[step];
            if (ShieldStep)
            {
                Deflect();
                return;
            }

            if (element != required)
            {
                Deflect();
                return;
            }

            Advance();
        }

        private void Advance()
        {
            var required = sequence[step];
            var center = transform.position + Vector3.up * (SigilHeight + step * SigilSpacing);
            EffectLibrary.PlayHit(required, center, 2f);
            GameAudio.PlaySfx($"Hit_{required}", 0.9f);
            if (step < sigils.Length) ElementGateFx.SigilHit(sigils[step], ElementInfo.Color(required)); // 연출: 고리 + 문양 부풂

            step++;
            if (step < sequence.Length)
            {
                // 맞힌 문양은 꺼지고, 벽 색이 다음 속성으로 바뀐다.
                Dim(sigils[step - 1]);
                if (shieldMark != null) shieldMark.gameObject.SetActive(false);
                onStep?.Invoke(step);
                if (barrier != null)
                {
                    var next = ElementInfo.Color(sequence[step]);
                    barrier.color = new Color(next.r, next.g, next.b, barrier.color.a);
                }
                return;
            }

            opened = true;
            GameAudio.PlaySfx("GateOpen", 0.85f, 0f);
            ElementGateFx.Shatter(transform.position, height, ElementInfo.Color(required)); // 연출: 벽 조각·빛 기둥·차르르
            StageEvents.PostNotice("봉인이 풀렸다!");
            onOpened?.Invoke();
            Destroy(gameObject);
        }

        // 방어막 문양: 플레이어가 문에 부딪히면 그 방어막으로 풀리는지 본다.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            var player = TrapCommon.PlayerFrom(collision.collider);
            if (opened || player == null) return;
            if (ShieldStep)
            {
                TryShield(player);
            }
        }

        // 방어막 문양: 방어막 미니게임 동안 캐릭터가 멈춰 있어 충돌 이벤트가 다시 오지 않으므로, 문 옆에 붙어 있는지 주기적으로 본다.
        private void Update()
        {
            if (opened || !ShieldStep || Time.time < nextShieldCheck) return;
            nextShieldCheck = Time.time + ShieldCheckInterval;

            Vector2 center = (Vector2)transform.position + Vector2.up * (ShieldTouchHeight / 2f);
            foreach (var hit in Physics2D.OverlapBoxAll(center, new Vector2(ShieldTouchReach * 2f, ShieldTouchHeight), 0f))
            {
                var player = TrapCommon.PlayerFrom(hit);
                if (player != null && TryShield(player)) return;
            }
        }

        private bool TryShield(PlayerMovement player)
        {
            var shield = player.GetComponent<ElementShield>();
            if (shield == null || !shield.Active) return false;
            if (shield.ActiveElement != sequence[0])
            {
                Deflect();
                return false;
            }

            Advance();
            return true;
        }

        // 틀린 속성·평타·다른 방어막: 문구 대신 벽이 잠깐 하얗게 번쩍여 튕겨냈음을 보여 준다.
        private void Deflect()
        {
            if (barrier == null || Time.time < nextDeflectTime) return;
            nextDeflectTime = Time.time + DeflectCooldown;
            StartCoroutine(FlashBarrier());
            ElementGateFx.Deflected(transform.position + Vector3.up * Mathf.Min(SigilHeight, height * 0.5f)); // 연출: "팅" + 흰 불꽃
        }

        private System.Collections.IEnumerator FlashBarrier()
        {
            barrier.color = new Color(1f, 1f, 1f, barrier.color.a);
            yield return new WaitForSecondsRealtime(DeflectFlash);
            if (opened || barrier == null) yield break;
            var color = ElementInfo.Color(sequence[step]); // 그사이 한 단계 풀렸으면 다음 속성 색
            barrier.color = new Color(color.r, color.g, color.b, barrier.color.a);
        }
    }
}
