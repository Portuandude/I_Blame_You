using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 맵 퍼즐: 마법 스위치 룬. 낭떠러지 건너편 절벽 첫 칸에 선 작은 룬 기둥이고, 문양(봉인문 문양 그림 재사용)과 같은 속성의
    // B 마법을 맞히면 같은 청크의 멈춰 있던 발판이 ActiveTime초 동안 오간다. 다시 맞히면 시간이 늘어난다.
    // 몸이 지나가는 트리거라 길을 막지 않는다: Projectile이 트리거에 붙은 IElementReceiver도 찾아 부른다.
    // 안내 문구는 없다 (2026-11-12 사람 지시): 모양으로 알린다. 꺼져 있는 동안 문양이 천천히 숨 쉬듯 커졌다 작아지고,
    // 낭떠러지 쪽에서 룬을 가리키는 빛 화살 무늬(>>)가 차례로 흐른다. 맞으면 빛 고리가 돌고, 틀린 속성은 문양이 하얗게 번쩍인다.
    // (마나는 재생되므로 갇히지 않는다.)
    public class PlatformSwitch : MonoBehaviour, IElementReceiver
    {
        public const float ActiveTime = 10f;
        private const float SigilHeight = 1.25f;          // 룬 문양 가운데 높이 (선 칸 바닥 기준)
        private const float DeflectCooldown = 0.4f;
        private const float DeflectFlash = 0.15f;
        private const int ArrowCount = 3;
        private const float ArrowNearest = 1.3f;          // 룬에서 가장 가까운 화살까지 (유닛, 낭떠러지 쪽 = 왼쪽)
        private const float ArrowSpacing = 0.8f;

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
        private float nextDeflectTime;
        private float flashUntil;
        private bool wasActive;
        private SpriteRenderer[] arrows;
        private Color arrowColor;

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

            // 룬을 가리키는 빛 화살 무늬 (>>): 낭떠러지 쪽(왼쪽)에서 문양 높이로 룬을 향한다. 꺼져 있을 때만 흐른다.
            var arrowSprite = TrapCommon.Frame("RuneArrow");
            rune.arrowColor = new Color(Mathf.Lerp(color.r, 1f, 0.25f), Mathf.Lerp(color.g, 1f, 0.25f), Mathf.Lerp(color.b, 1f, 0.25f), 1f);
            rune.arrows = new SpriteRenderer[ArrowCount];
            for (int i = 0; i < ArrowCount; i++)
            {
                var arrow = TrapCommon.AddSprite(go.transform, $"Arrow_{i}", arrowSprite,
                    new Vector2(-(ArrowNearest + i * ArrowSpacing), SigilHeight), new Vector2(0.3f, 0.3f), rune.arrowColor, 3);
                if (arrowSprite != null) arrow.color = rune.arrowColor;
                rune.arrows[i] = arrow;
            }
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
                Deflect();
                return;
            }

            var sigilAt = transform.position + Vector3.up * SigilHeight;
            var streamColor = ElementInfo.Color(required);
            foreach (var platform in platforms)
                if (platform != null)
                {
                    if (platform.SwitchDriven) SwitchPlatformFx.Stream(sigilAt, platform.transform, streamColor); // 문양에서 발판으로 마력 줄기
                    platform.Activate(ActiveTime);
                }

            EffectLibrary.PlayHit(required, transform.position + Vector3.up * SigilHeight, 1.5f);
            GameAudio.PlaySfx($"Hit_{required}", 0.6f);
            GameAudio.PlaySfx("Switch_On", 0.8f, 0f);
            ApplyGlow(true);
        }

        // 틀린 속성: 문양이 잠깐 하얗게 번쩍이고 "팅" + 흰 불꽃 조각 (문구 없음).
        private void Deflect()
        {
            if (Time.time < nextDeflectTime) return;
            nextDeflectTime = Time.time + DeflectCooldown;
            flashUntil = Time.time + DeflectFlash;
            ElementGateFx.Deflected(transform.position + Vector3.up * SigilHeight); // 봉인문과 같은 "팅" + 흰 불꽃 조각
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

            if (!active) AnimateIdle();
        }

        // 꺼져 있는 동안: 문양이 천천히 숨 쉬고, 화살 무늬가 바깥에서 룬 쪽으로 차례로 밝아진다. 틀리게 맞으면 하얗게 번쩍.
        private void AnimateIdle()
        {
            if (sigil != null)
            {
                float breathe = 1f + 0.12f * Mathf.Sin(Time.time * 2.4f);
                sigil.transform.localScale = sigilScale * breathe;
                float k = 0.55f + 0.2f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f));
                sigil.color = Time.time < flashUntil ? Color.white
                    : new Color(sigilColor.r * k, sigilColor.g * k, sigilColor.b * k, 0.85f);
            }
            if (arrows == null) return;
            const float flowPeriod = 1.2f;
            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i] == null) continue;
                // i가 클수록 바깥(먼 쪽): 바깥 → 룬 순서로 밝아진다.
                float phase = Mathf.Repeat(Time.time / flowPeriod - (arrows.Length - 1 - i) / (float)arrows.Length, 1f);
                float a = 0.25f + 0.75f * Mathf.Clamp01(1f - phase * 2.5f);
                arrows[i].color = new Color(arrowColor.r, arrowColor.g, arrowColor.b, a);
            }
        }

        private void ApplyGlow(bool active)
        {
            wasActive = active;
            if (halo != null) halo.gameObject.SetActive(active);
            if (arrows != null)
                foreach (var arrow in arrows)
                    if (arrow != null) arrow.gameObject.SetActive(!active);
            if (sigil == null) return;
            sigil.transform.localScale = sigilScale;
            float k = active ? 1f : 0.55f;
            sigil.color = new Color(sigilColor.r * k + (active ? 0.15f : 0f), sigilColor.g * k + (active ? 0.15f : 0f),
                sigilColor.b * k + (active ? 0.15f : 0f), active ? 1f : 0.8f);
        }

    }
}
