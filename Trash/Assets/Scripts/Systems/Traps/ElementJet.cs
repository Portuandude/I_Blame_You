using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 퍼즐 방 "방어막 다리"의 속성 분출 통로 (2026-11-14, Docs/PuzzleRoomDesign.md 2절 2번).
    // 바닥 분출구 width칸에서 그 속성 기운이 쉬지 않고 Height만큼 솟는다. 몸이 없는 트리거라 길을 막지 않는다.
    // - 같은 속성 방어막(ElementShield)을 두른 플레이어는 그냥 지나간다 (방어막 둘레에 작은 불똥만).
    // - 아니면 약한 속성 피해 + 통로 입구(returnTo)로 튕겨 돌아간다 (연기 + "치익"). 같은 순간 두 번 튕기지 않게 짧은 쿨다운.
    // 안내 문구는 없다: 분출 색이 곧 필요한 방어막 속성이다.
    public class ElementJet : MonoBehaviour
    {
        public const float Height = 3.2f;
        private const float DamageMultiplier = 0.25f;
        private const float BounceCooldown = 0.4f;

        private Element element;
        private Vector2 returnTo;
        private float nextBounce;
        private SpriteRenderer[] columns;
        private Color color;

        // bottomLeft: 첫 칸 바닥 왼쪽 아래 (월드). returnTo: 튕겨 돌아갈 플레이어 위치 (월드, 선 칸 + 1 = 몸 가운데 — FeatureTestTools.JumpToArea와 같은 높이).
        public static ElementJet Create(Transform parent, Vector2 bottomLeft, int width, Element element, Vector2 returnTo)
        {
            var go = new GameObject($"ElementJet_{element}");
            go.transform.SetParent(parent, false);
            go.transform.position = bottomLeft;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(width - 0.2f, Height);
            trigger.offset = new Vector2(width / 2f, Height / 2f + 0.15f);

            var jet = go.AddComponent<ElementJet>();
            jet.element = element;
            jet.returnTo = returnTo;
            jet.color = ElementInfo.Color(element);

            var ventSprite = TrapCommon.Frame("FlameVent");
            jet.columns = new SpriteRenderer[width];
            for (int i = 0; i < width; i++)
            {
                var vent = TrapCommon.AddSprite(go.transform, $"Vent_{i}", ventSprite, new Vector2(i + 0.5f, 0.15f), new Vector2(1f, 0.3f),
                    new Color(0.3f, 0.28f, 0.32f), 1);
                if (ventSprite != null) vent.color = Color.Lerp(Color.white, jet.color, 0.5f);
                var column = TrapCommon.AddSprite(go.transform, $"Column_{i}", null, new Vector2(i + 0.5f, Height / 2f + 0.15f),
                    new Vector2(0.8f, Height), new Color(jet.color.r, jet.color.g, jet.color.b, 0.35f), 2);
                jet.columns[i] = column;
            }
            return jet;
        }

        private void Update()
        {
            if (columns == null) return;
            // 기둥마다 엇갈려 일렁인다 (쉬지 않고 뿜는 느낌).
            for (int i = 0; i < columns.Length; i++)
            {
                if (columns[i] == null) continue;
                float a = 0.28f + 0.17f * (0.5f + 0.5f * Mathf.Sin(Time.time * 7f + i * 1.3f));
                columns[i].color = new Color(color.r, color.g, color.b, a);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null || Time.time < nextBounce) return;

            var shield = player.GetComponent<ElementShield>();
            if (shield != null && shield.Active && shield.ActiveElement == element) return; // 같은 속성 방어막: 지나간다

            nextBounce = Time.time + BounceCooldown;
            var health = player.GetComponent<HealthSystem>();
            if (health != null && !health.IsDead && !health.IsInvulnerable)
                TrapCommon.Strike(player, DamageKind.Magic, element, DamageMultiplier);
            EffectLibrary.PlayHit(element, other.bounds.center, 1.3f);
            GameAudio.PlaySfx("FlameSizzle", 0.7f);

            // 통로 입구로 튕겨 돌아간다 (떨어지는 속도는 지운다).
            player.transform.position = new Vector3(returnTo.x, returnTo.y, player.transform.position.z);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
            EffectLibrary.PlayOneShot(player.transform.position + Vector3.up, TrapCommon.Frames("Smoke"), 12f,
                new Color(0.7f, 0.7f, 0.75f, 0.7f), 0.8f);
        }
    }
}
