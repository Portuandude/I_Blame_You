using UnityEngine;

namespace IBlameYou.Systems
{
    // 유물 아이콘 반짝 연출 (VfxPlayer.MedalGlint·AcornGlint·HoneyGlint·StarCandyGlint·HorseshoeGlint): 회복할 때 플레이어 머리 위로 유물 아이콘이 톡 튀어나와 떠오르고,
    // 반짝임(Resources/Effects/Sparkle)이 한 번 터진 뒤 옅어지며 사라진다 (0.9초).
    // 그림은 유물 아이콘 Resources/UI/HUD/Relics/<이름> (24px)을 키워 쓴다 (용사의 훈장 = HeroMedal, 도토리 주머니 = AcornPouch, 꿀단지 = HoneyJar, 별사탕 = StarCandy, 말굽 자석 = HorseshoeMagnet).
    // 그림이 없으면 아무것도 하지 않는다.
    public class MedalGlintFx : MonoBehaviour
    {
        private const float Lifetime = 0.9f;
        private const float PopTime = 0.15f;     // 0 → 1.15배로 튀어나오는 시간
        private const float Rise = 0.55f;        // 사는 동안 떠오르는 높이 (유닛)
        private const float FadeStart = 0.6f;    // 이 비율부터 옅어진다
        private const float IconScale = 2.6f;    // 24px / ppu 100 → 보이는 크기 약 0.62유닛
        private const float SparkleDelay = 0.12f;

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> sprites =
            new System.Collections.Generic.Dictionary<string, Sprite>(); // 아이콘 이름 → 그림 (없으면 null로 기억)

        private Transform follow;
        private Vector3 offset;
        private Vector3 basePosition;
        private SpriteRenderer icon;
        private float age;
        private bool sparkled;

        // follow 기준 offset(월드 방향)에서 시작해 따라다닌다. follow의 자식이 아니라서 좌우 뒤집힘·크기의 영향을 받지 않는다.
        public static void Create(Transform follow, Vector3 offset, string relicIcon)
        {
            if (follow == null || string.IsNullOrEmpty(relicIcon)) return;
            if (!sprites.TryGetValue(relicIcon, out var sprite))
            {
                sprite = Resources.Load<Sprite>("UI/HUD/Relics/" + relicIcon);
                sprites[relicIcon] = sprite;
            }
            if (sprite == null) return;

            var go = new GameObject("RelicGlint_" + relicIcon);
            go.transform.position = follow.position + offset;
            go.transform.localScale = Vector3.zero;
            var fx = go.AddComponent<MedalGlintFx>();
            fx.follow = follow;
            fx.offset = offset;
            fx.basePosition = go.transform.position;
            fx.icon = go.AddComponent<SpriteRenderer>();
            fx.icon.sprite = sprite;
            fx.icon.sortingOrder = 8;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);

            // 떠오르기: 처음엔 빠르고 끝에서 느리게. 따라가던 대상이 사라지면 그 자리에서 마저 떠오른다.
            float eased = 1f - (1f - t) * (1f - t);
            if (follow != null) basePosition = follow.position + offset;
            transform.position = basePosition + Vector3.up * (Rise * eased);

            // 크기: 0 → 1.15배 → 1배 (톡 튀는 느낌)
            float pop = age < PopTime
                ? Mathf.Lerp(0f, 1.15f, age / PopTime)
                : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((age - PopTime) / PopTime));
            transform.localScale = new Vector3(IconScale * pop, IconScale * pop, 1f);

            if (!sparkled && age >= SparkleDelay)
            {
                sparkled = true;
                VfxPlayer.HelmetGlint(transform.position + new Vector3(0.18f, 0.18f, 0f)); // 메달 오른쪽 위에서 반짝
            }

            float alpha = t < FadeStart ? 1f : 1f - (t - FadeStart) / (1f - FadeStart);
            icon.color = new Color(1f, 1f, 1f, alpha);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
