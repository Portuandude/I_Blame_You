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

        private BoxCollider2D solid;
        private SpriteRenderer[] renderers;
        private float offset;

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
            var sprite = TrapCommon.Frame("Crumble");
            for (int i = 0; i < width; i++)
                TrapCommon.AddSprite(visual, $"Tile_{i}", sprite, new Vector2(i - (width - 1) / 2f, -0.05f),
                    new Vector2(1f, Thickness + 0.1f), Body, 0);
            platform.renderers = visual.GetComponentsInChildren<SpriteRenderer>();
            platform.offset = beat * BeatDelay;
            platform.Apply();
            return platform;
        }

        private void Update() => Apply();

        // 지금 박자 위치 (0 ~ Cycle). Time.time 기준이라 방을 다시 들어와도 다른 발판과 박자가 맞는다.
        private float Phase => Mathf.Repeat(Time.time - offset, Cycle);

        private void Apply()
        {
            float t = Phase;
            bool solidNow = t < VisibleTime + WarnTime;
            float alpha = 1f;
            if (t >= VisibleTime && t < VisibleTime + WarnTime)
                alpha = Mathf.Repeat((t - VisibleTime) / 0.1f, 2f) < 1f ? 0.45f : 1f; // 0.1초마다 깜빡
            else if (!solidNow)
                alpha = GoneAlpha;

            if (solid.enabled != solidNow) solid.enabled = solidNow;
            foreach (var r in renderers)
            {
                var c = r.color;
                r.color = new Color(c.r, c.g, c.b, alpha);
            }
        }
    }
}
