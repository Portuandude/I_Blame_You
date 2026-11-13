using UnityEngine;

namespace IBlameYou.Systems
{
    // 깜빡 발판 (Docs/ContentIdeas.md 퍼즐 1): 박자에 맞춰 나타났다 사라지는 발판 (아래에서 뚫고 올라갈 수 있다).
    // 보임 VisibleTime → 깜빡이며 경고 WarnTime → 사라짐 GoneTime을 되풀이하고, 박자 순번(beat)마다 BeatDelay씩 늦게 시작해
    // 다리 위 발판들이 왼쪽부터 차례로 깜빡인다. 사라진 동안은 밟을 수 없고 흐린 윤곽만 남는다.
    // 떨어졌을 때 돌아갈 "안전한 땅"으로는 치지 않는다 (UnsafeGround).
    public class BlinkPlatform : MonoBehaviour
    {
        public const float VisibleTime = 2f;
        public const float WarnTime = 0.6f;
        public const float GoneTime = 1.4f;
        public const float BeatDelay = 0.7f;
        private const float Cycle = VisibleTime + WarnTime + GoneTime;
        private const float Thickness = 0.35f;
        private const float GoneAlpha = 0.12f;
        private static readonly Color Body = new Color(0.55f, 0.85f, 1f); // 하늘빛 (무너지는 발판 나무색과 구별)

        private const float CloudAlpha = 0.55f;
        private static readonly Color CloudColor = new Color(1f, 0.97f, 1f); // 솜구름이 받쳐 줄 때는 하얀 솜빛

        // 솜구름 유물을 가진 플레이어가 있는지 (발판마다 매 프레임 찾지 않게 0.5초마다 한 번 확인해 모든 발판이 나눠 쓴다)
        private static float cottonCheckedAt = -10f;
        private static bool cottonOwned;

        private static bool CottonCloudOwned()
        {
            float since = Time.unscaledTime - cottonCheckedAt;
            if (since >= 0f && since < 0.5f) return cottonOwned; // 시계가 되돌아갔으면(플레이 모드 재시작) 다시 확인
            cottonCheckedAt = Time.unscaledTime;
            cottonOwned = false;
            foreach (var relics in FindObjectsByType<IBlameYou.Player.PlayerRelics>(FindObjectsSortMode.None))
                if (relics.Has(IBlameYou.Player.RelicId.CottonCloud)) { cottonOwned = true; break; }
            return cottonOwned;
        }

        private BoxCollider2D solid;
        private SpriteRenderer[] renderers;
        private Transform visual;
        private float offset;
        private int width;

        // 연출용 (BlinkPlatformFx): 지난 프레임 상태와 비교해 바뀌는 순간에만 한 번씩 낸다.
        // 처음 그릴 때·다시 켜질 때(방을 다시 들어옴)는 상태만 기억하고 연출은 내지 않는다.
        private enum State { Visible, Cloud, Gone }
        private const float PopTime = 0.15f;  // 나타날 때 살짝 커졌다 제자리로 돌아오는 시간
        private const float PopScale = 0.12f;
        private const float StandMemory = 0.15f; // 플레이어가 위에 서 있었다고 기억하는 시간
        private bool primed;
        private State lastState;
        private float popAge = PopTime;
        private float standingUntil = -10f;
        private Vector3 standingFeet;

        // topLeft: 발판 윗면 왼쪽 끝 (월드), width: 칸 수, beat: 박자 순번 (0부터).
        public static BlinkPlatform Create(Transform parent, Vector2 topLeft, int width, int beat)
        {
            var go = new GameObject("BlinkPlatform");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(topLeft.x + width / 2f, topLeft.y - Thickness / 2f, 0f);
            go.layer = TrapCommon.GroundLayer;
            go.AddComponent<UnsafeGround>();

            var platform = go.AddComponent<BlinkPlatform>();
            platform.solid = go.AddComponent<BoxCollider2D>();
            platform.solid.size = new Vector2(width, Thickness);
            platform.solid.usedByEffector = true;
            platform.solid.sharedMaterial = PhysicsMaterialFactory.Frictionless();
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            platform.visual = visual;
            platform.width = width;
            var sprite = TrapCommon.Frame("Crumble");
            for (int i = 0; i < width; i++)
                TrapCommon.AddSprite(visual, $"Tile_{i}", sprite, new Vector2(i - (width - 1) / 2f, -0.05f),
                    new Vector2(1f, Thickness + 0.1f), Body, 0);
            platform.renderers = visual.GetComponentsInChildren<SpriteRenderer>();
            platform.offset = beat * BeatDelay;
            platform.Apply();
            return platform;
        }

        private void OnEnable() => primed = false;

        private void Update()
        {
            Apply();
            UpdatePop();
        }

        // 플레이어가 윗면에 서 있으면 발 위치를 기억해 둔다 (꺼질 때 발밑 "퐁"과 먼지).
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (TrapCommon.PlayerFrom(collision.collider) == null) return;
            var feet = collision.collider.bounds;
            if (feet.min.y < solid.bounds.max.y - 0.12f) return; // 아래에서 뚫고 오르는 중
            standingUntil = Time.time + StandMemory;
            standingFeet = new Vector3(feet.center.x, solid.bounds.max.y, 0f);
        }

        // 지금 박자 위치 (0 ~ Cycle). Time.time 기준이라 방을 다시 들어와도 다른 발판과 박자가 맞는다.
        private float Phase => Mathf.Repeat(Time.time - offset, Cycle);

        private void Apply()
        {
            float t = Phase;
            float grace = CottonCloudOwned() ? IBlameYou.Player.RelicBook.CottonCloudGrace : 0f;
            bool cloud = t >= VisibleTime + WarnTime && t < VisibleTime + WarnTime + grace; // 솜구름이 잠깐 받쳐 주는 동안
            bool solidNow = t < VisibleTime + WarnTime + grace;
            float alpha = 1f;
            if (t >= VisibleTime && t < VisibleTime + WarnTime)
                alpha = Mathf.Repeat((t - VisibleTime) / 0.1f, 2f) < 1f ? 0.45f : 1f; // 0.1초마다 깜빡
            else if (cloud)
                alpha = CloudAlpha;
            else if (!solidNow)
                alpha = GoneAlpha;

            if (solid.enabled != solidNow) solid.enabled = solidNow;
            PlayFx(!solidNow ? State.Gone : cloud ? State.Cloud : State.Visible);
            foreach (var r in renderers)
            {
                var c = cloud ? CloudColor : Body;
                r.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        // 상태가 바뀌는 순간의 그림·소리 (판정에는 영향 없음).
        private void PlayFx(State now)
        {
            if (!primed)
            {
                primed = true;
                lastState = now;
                return;
            }
            if (now == lastState) return;
            var center = transform.position;
            if (now == State.Visible)
            {
                if (lastState == State.Cloud) BlinkPlatformFx.CloudFade(center, width); // 솜구름 유예 중 바로 다시 나타난 경우
                BlinkPlatformFx.Appear(center, width);
                popAge = 0f;
            }
            else if (now == State.Cloud)
            {
                BlinkPlatformFx.Vanish(center, width); // 하늘빛 발판은 흩어지고 솜구름이 대신 받쳐 준다
                BlinkPlatformFx.CloudRise(center, width);
            }
            else
            {
                if (lastState == State.Cloud) BlinkPlatformFx.CloudFade(center, width);
                else BlinkPlatformFx.Vanish(center, width);
                if (Time.time <= standingUntil) BlinkPlatformFx.Underfoot(standingFeet);
                standingUntil = -10f;
            }
            lastState = now;
        }

        // 나타날 때 발판이 살짝 커졌다 제자리로.
        private void UpdatePop()
        {
            if (popAge >= PopTime) return;
            popAge += Time.deltaTime;
            float t = Mathf.Clamp01(popAge / PopTime);
            float k = 1f + PopScale * Mathf.Sin(t * Mathf.PI);
            visual.localScale = t >= 1f ? Vector3.one : new Vector3(k, k, 1f);
        }

    }
}
