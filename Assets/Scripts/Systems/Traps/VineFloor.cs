using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 숲 바닥 덩굴 (스테이지 2, 2026-11-24 사람 결정 OpenDecisions F2): 밟고 있는 동안 느려진다 (CombatStats.TerrainSlow, 적도 같다).
    // 불 마법으로 태우면 사라진다: 불 투사체가 지나가면(IElementTouchReceiver) 또는 불꽃 장막을 가까이 쓰면(BurnNear).
    // 탄 덩굴은 방을 다시 만들 때 돌아오지 않는다 (ChunkStreamer가 기억). 그림은 임시 (초록 띠, 아트 규칙).
    public class VineFloor : MonoBehaviour, IElementTouchReceiver
    {
        private static readonly List<VineFloor> Active = new List<VineFloor>();
        private static readonly Color VineColor = new Color(0.3f, 0.6f, 0.25f, 0.9f);

        private readonly Dictionary<CombatStats, int> inside = new Dictionary<CombatStats, int>(); // 몸마다 들어와 있는 콜라이더 수
        private System.Action onBurned;
        private bool burned;

        public static VineFloor Create(Transform parent, Vector2 bottomLeft, int width, System.Action onBurned)
        {
            var go = new GameObject("VineFloor");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(bottomLeft.x + width / 2f, bottomLeft.y, 0f);

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(width - 0.1f, 0.6f);
            trigger.offset = new Vector2(0f, 0.3f);

            TrapCommon.AddSprite(go.transform, "Vines", null, new Vector2(0f, 0.15f), new Vector2(width, 0.3f), VineColor, 0);
            for (int i = 0; i < width; i++)
                TrapCommon.AddSprite(go.transform, "Curl", null, new Vector2(-width / 2f + 0.5f + i, 0.38f), new Vector2(0.3f, 0.25f),
                    new Color(0.4f, 0.72f, 0.3f), 1);

            var vine = go.AddComponent<VineFloor>();
            vine.onBurned = onBurned;
            return vine;
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable()
        {
            Active.Remove(this);
            foreach (var stats in inside.Keys) if (stats != null) stats.TerrainSlowStacks = Mathf.Max(0, stats.TerrainSlowStacks - 1);
            inside.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var stats = other.GetComponentInParent<CombatStats>();
            if (stats == null) return;
            inside.TryGetValue(stats, out int count);
            inside[stats] = count + 1;
            if (count == 0) stats.TerrainSlowStacks++; // 몸의 첫 콜라이더만 센다 (콜라이더가 여럿이어도 한 번)
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var stats = other.GetComponentInParent<CombatStats>();
            if (stats == null || !inside.TryGetValue(stats, out int count)) return;
            if (count > 1) { inside[stats] = count - 1; return; } // 아직 다른 콜라이더가 덩굴 위
            inside.Remove(stats);
            stats.TerrainSlowStacks = Mathf.Max(0, stats.TerrainSlowStacks - 1);
        }

        public void TouchElement(Element element)
        {
            if (element == Element.Fire) Burn();
        }

        // 불꽃 장막 (SpellCaster): from에서 facing 쪽으로 range 안, 높이 차 3칸 이내의 덩굴을 모두 태운다
        public static void BurnNear(Vector2 from, float facing, float range)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var vine = Active[i];
                if (vine == null) continue;
                var box = vine.GetComponent<BoxCollider2D>();
                float half = box != null ? box.size.x / 2f : 0.5f;
                float dx = (vine.transform.position.x - from.x) * Mathf.Sign(facing);
                if (dx < -half || dx > range + half || Mathf.Abs(vine.transform.position.y - from.y) > 3f) continue;
                vine.Burn();
            }
        }

        private void Burn()
        {
            if (burned) return;
            burned = true;
            var pos = transform.position + Vector3.up * 0.3f;
            EffectLibrary.PlayOneShot(pos, EffectLibrary.HitFrames(Element.Fire), 18f, new Color(1f, 0.5f, 0.2f, 0.8f), 1.2f);
            GameAudio.PlaySfx("Hit_Fire", 0.6f);
            onBurned?.Invoke();
            Destroy(gameObject);
        }
    }
}
