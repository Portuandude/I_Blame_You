using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 맵 퍼즐: 마법 스위치 룬. 낭떠러지 건너편 절벽 첫 칸에 선 작은 룬 기둥이고, 문양(봉인문 문양 그림 재사용)과 같은 속성의
    // B 마법을 맞히면 같은 청크의 멈춰 있던 발판이 ActiveTime초 동안 오간다. 다시 맞히면 시간이 늘어난다.
    // 몸이 지나가는 트리거라 길을 막지 않는다: Projectile이 트리거에 붙은 IElementReceiver도 찾아 부른다.
    // 틀린 속성은 안내 문구만 띄운다 (마나는 재생되므로 갇히지 않는다).
    public class PlatformSwitch : MonoBehaviour, IElementReceiver
    {
        public const float ActiveTime = 10f;
        private const float HintCooldown = 1.5f;
        private const float SigilHeight = 1.25f;          // 룬 문양 가운데 높이 (선 칸 바닥 기준)
        private const float ApproachCheckInterval = 0.25f;
        private static readonly Vector2 ApproachBox = new Vector2(6f, 3f); // 발판 출발점 둘레: 여기 들어오면 한 번 안내

        private Element required;
        private readonly List<MovingPlatform> platforms = new List<MovingPlatform>();
        private SpriteRenderer sigil;
        private Color sigilColor;
        private Vector3 sigilScale = Vector3.one;
        private SpriteRenderer halo;
        private Color haloColor;
        private const float TickInterval = 0.5f;
        private int lastTick = -1;
        private const float WarnTime = 2.5f; // 남은 가동 시간이 이보다 적으면 빛 고리가 빠르게 깜빡인다
        private float nextHintTime;
        private float nextApproachCheck;
        private bool approachHinted;
        private bool wasActive;

        // bottom: 룬이 선 칸의 바닥 가운데 (월드).
        public static PlatformSwitch Create(Transform parent, Vector2 bottom, Element element, IList<MovingPlatform> linked)
        {
            var go = new GameObject("PlatformSwitch");
            go.transform.SetParent(parent, false);
            go.transform.position = bottom;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.9f, 1.7f);
            trigger.offset = new Vector2(0f, 0.95f); // 바닥 위 0.1 ~ 1.8: 같은 높이·한 칸 높은 절벽에서 쏜 마법이 모두 닿는다

            var rune = go.AddComponent<PlatformSwitch>();
            rune.required = element;
            if (linked != null)
                foreach (var platform in linked)
                    if (platform != null)
                    {
                        rune.platforms.Add(platform);
                        platform.SetSwitchColor(ElementInfo.Color(element));
                    }

            var color = ElementInfo.Color(element);
            // 받침 기둥(RuneSwitch 그림 1 × 1.8유닛, 없으면 어두운 돌색 사각형) + 위 둥근 받침에 속성 문양.
            var postSprite = TrapCommon.Frame("RuneSwitch");
            TrapCommon.AddSprite(go.transform, "Post", postSprite, new Vector2(0f, postSprite != null ? 0.9f : 0.4f),
                new Vector2(0.35f, 0.8f), new Color(0.32f, 0.3f, 0.4f), 1);
            // 켜져 있는 동안 문양 둘레를 도는 빛 고리 (흰 그림을 속성 색으로 물들인다).
            rune.halo = EffectLibrary.CreateVisual(go.transform, "Halo", TrapCommon.Frames("RuneSwitchGlow"), 12f, true,
                new Color(color.r, color.g, color.b, 0.35f), 1f, 4);
            rune.halo.transform.localPosition = new Vector3(0f, SigilHeight, 0f);
            rune.haloColor = new Color(Mathf.Lerp(color.r, 1f, 0.3f), Mathf.Lerp(color.g, 1f, 0.3f), Mathf.Lerp(color.b, 1f, 0.3f), 1f);
            rune.halo.gameObject.SetActive(false);
            var sigilSprite = TrapCommon.Frame($"RuneSigil_{element}");
            rune.sigil = TrapCommon.AddSprite(go.transform, "Sigil", sigilSprite,
                new Vector2(0f, SigilHeight), new Vector2(0.7f, 0.7f), color, 3);
            rune.sigilColor = sigilSprite != null ? Color.white : color; // 문양 그림은 이미 속성 색이라 흰색 기준으로 밝기만 바꾼다
            rune.sigilScale = rune.sigil.transform.localScale;
            rune.ApplyGlow(false);
            return rune;
        }

        private bool AnyMoving
        {
            get
            {
                foreach (var platform in platforms)
                    if (platform != null && !platform.Idle) return true;
                return false;
            }
        }

        private float Remaining
        {
            get
            {
                float remaining = 0f;
                foreach (var platform in platforms)
                    if (platform != null) remaining = Mathf.Max(remaining, platform.ActiveRemaining);
                return remaining;
            }
        }

        public void ReceiveElement(Element element)
        {
            if (element != required)
            {
                PostHint($"룬이 튕겨냈다! {ElementInfo.KoreanName(required)} 속성 마법을 맞혀야 발판이 움직인다");
                return;
            }

            bool wasMoving = AnyMoving;
            foreach (var platform in platforms)
                if (platform != null) platform.Activate(ActiveTime);

            EffectLibrary.PlayHit(required, transform.position + Vector3.up * SigilHeight, 1.5f);
            GameAudio.PlaySfx($"Hit_{required}", 0.6f);
            GameAudio.PlaySfx("Switch_On", 0.8f, 0f);
            ApplyGlow(true);
            approachHinted = true;
            nextHintTime = 0f;
            PostHint(wasMoving ? "발판이 조금 더 오래 움직인다!" : "룬이 빛난다! 발판이 잠시 움직인다");
        }

        private void Update()
        {
            bool active = AnyMoving;
            if (active != wasActive) ApplyGlow(active);
            if (active && sigil != null)
            {
                // 움직이는 동안 문양이 반짝인다.
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 8f);
                sigil.transform.localScale = sigilScale * pulse;
            }
            if (active && halo != null)
            {
                // 가동 시간이 끝나 가면 빛 고리가 빠르게 깜빡여 알린다 (끝나면 발판은 끝점까지 가서 멈춘다).
                float remaining = Remaining;
                float alpha = remaining <= 0f ? 0.35f
                    : remaining < WarnTime ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 12f)) : 1f;
                halo.color = new Color(haloColor.r, haloColor.g, haloColor.b, alpha);

                // 끝나 가는 동안 0.5초마다 똑딱 소리.
                if (remaining > 0f && remaining < WarnTime)
                {
                    int tick = Mathf.CeilToInt(remaining / TickInterval);
                    if (tick != lastTick)
                    {
                        lastTick = tick;
                        GameAudio.PlaySfx("Switch_Tick", 0.7f, 0f);
                    }
                }
                else lastTick = -1;
            }

            if (approachHinted || platforms.Count == 0 || platforms[0] == null || Time.time < nextApproachCheck) return;
            nextApproachCheck = Time.time + ApproachCheckInterval;
            if (!platforms[0].Idle) return;
            foreach (var hit in Physics2D.OverlapBoxAll(platforms[0].StartCenter, ApproachBox, 0f))
            {
                if (TrapCommon.PlayerFrom(hit) == null) continue;
                approachHinted = true;
                PostHint($"발판이 멈춰 있다. 건너편 룬에 {ElementInfo.KoreanName(required)} 속성 마법(B)을 맞혀라");
                return;
            }
        }

        // 플레이어가 룬을 지나가면 어떤 속성이 필요한지 알려 준다.
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (TrapCommon.PlayerFrom(other) == null) return;
            PostHint($"마법 스위치: {ElementInfo.KoreanName(required)} 속성 마법(B)을 맞히면 발판이 움직인다");
        }

        private void ApplyGlow(bool active)
        {
            wasActive = active;
            if (halo != null) halo.gameObject.SetActive(active);
            if (sigil == null) return;
            sigil.transform.localScale = sigilScale;
            float k = active ? 1f : 0.55f;
            sigil.color = new Color(sigilColor.r * k + (active ? 0.15f : 0f), sigilColor.g * k + (active ? 0.15f : 0f),
                sigilColor.b * k + (active ? 0.15f : 0f), active ? 1f : 0.8f);
        }

        private void PostHint(string text)
        {
            if (Time.time < nextHintTime) return;
            nextHintTime = Time.time + HintCooldown;
            StageEvents.PostNotice(text);
        }
    }
}
