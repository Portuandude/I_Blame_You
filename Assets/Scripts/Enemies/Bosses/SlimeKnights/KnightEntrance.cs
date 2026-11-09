using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 중간보스 슬라임 기사 등장 연출: 기사가 위에서 떨어져 쿵 착지한다 (충격파 고리 + 흙먼지 + 투구 반짝임 + 화면 살짝 흔들림).
    // 진짜 기사는 떨어지는 동안 꺼 두고(AI·접촉 피해·체력바 없음), 같은 그림을 그린 대역이 떨어진 뒤 그 자리에서 켠다.
    // 그래서 물리·AI·전투 구역 등록(ArenaZone)은 평소와 똑같고, 연출이 끝나기 전에 맞거나 움직일 일이 없다.
    public class KnightEntrance : MonoBehaviour
    {
        private const float DropHeight = 7f;       // 위에서 떨어지기 시작하는 높이 (천장이 낮으면 그 아래까지)
        private const float CeilingMargin = 1.5f;
        private const float FallTime = 0.45f;      // 가속하며 떨어진다 (t²)
        private const float ShakeDuration = 0.25f;
        private const float ShakeMagnitude = 0.18f;

        private GameObject knight;
        private SpriteRenderer standIn;
        private Vector3 landing;
        private float height;
        private Vector3 baseScale;
        private float age;
        private bool landed;

        // 방금 만든 기사에 등장 연출을 건다. 그림이 없으면(단색 사각형 대체) 연출 없이 바로 둔다.
        public static void Play(GameObject knightObject)
        {
            if (knightObject == null) return;
            var visual = knightObject.transform.Find("Visual");
            var source = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (source == null || source.sprite == null) return;

            var go = new GameObject("KnightEntrance");
            go.transform.SetParent(knightObject.transform.parent, true); // 방이 치워지면 함께 사라진다 (부모가 나중에 정해지면 Update에서 따라간다)
            var entrance = go.AddComponent<KnightEntrance>();
            entrance.knight = knightObject;
            entrance.landing = visual.position;

            // 천장이 낮으면 천장 아래에서 떨어진다
            float drop = DropHeight;
            int groundMask = 1 << TrapCommon.GroundLayer;
            var hit = Physics2D.Raycast(visual.position + Vector3.up * 0.5f, Vector2.up, DropHeight + CeilingMargin, groundMask);
            if (hit.collider != null) drop = Mathf.Max(0f, hit.distance + 0.5f - CeilingMargin);
            entrance.height = drop;

            var standInObject = new GameObject("Visual");
            standInObject.transform.SetParent(go.transform, false);
            standInObject.transform.position = visual.position + Vector3.up * drop;
            standInObject.transform.localScale = visual.lossyScale;
            entrance.baseScale = visual.lossyScale;
            entrance.standIn = standInObject.AddComponent<SpriteRenderer>();
            entrance.standIn.sprite = source.sprite;
            entrance.standIn.flipX = source.flipX;
            entrance.standIn.color = source.color;
            entrance.standIn.sortingOrder = source.sortingOrder;

            knightObject.SetActive(false);
            if (drop < 0.5f) entrance.Land(); // 떨어질 자리가 없으면 바로 착지 연출만
        }

        private void Update()
        {
            if (landed) return;
            if (knight == null) { Destroy(gameObject); return; } // 기사가 치워졌으면 (방 이동 등) 대역도 치운다
            if (transform.parent != knight.transform.parent) transform.SetParent(knight.transform.parent, true); // 스포너가 만든 뒤 부모를 정한다

            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / FallTime);
            standIn.transform.position = landing + Vector3.up * (height * (1f - t * t));
            // 떨어지는 동안 세로로 살짝 늘어난다 (속도감)
            float stretch = 1f + 0.15f * t;
            standIn.transform.localScale = new Vector3(baseScale.x, baseScale.y * stretch, baseScale.z);
            if (t >= 1f) Land();
        }

        private void Land()
        {
            if (landed) return;
            landed = true;
            if (knight == null) { Destroy(gameObject); return; }

            var feet = knight.transform.position; // 스폰 위치 = 발밑 (ArenaZone이 바닥 위 자리를 준다)
            knight.SetActive(true);
            EnemySquash.For(knight).Land();

            VfxPlayer.Shockwave(feet, 1.3f);
            VfxPlayer.Dust(feet, 1f);
            var bounds = standIn != null ? standIn.bounds : new Bounds(feet + Vector3.up, Vector3.one);
            VfxPlayer.HelmetGlint(new Vector3(bounds.center.x, bounds.max.y - 0.25f, 0f));
            CameraFollow.ShakeMain(ShakeDuration, ShakeMagnitude);
            GameAudio.PlaySfx("Land", 0.8f);
            Destroy(gameObject);
        }
    }
}
