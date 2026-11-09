using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 보스·중간보스 등장 연출: 위에서 떨어져 쿵 착지한다 (충격파 고리 + 흙먼지 + 꼭대기(투구·왕관) 반짝임 + 화면 흔들림, 이름 띠는 선택).
    // 진짜 몸은 떨어지는 동안 꺼 두고(AI·접촉 피해·체력바 없음), 같은 그림(몸 + 왕관 같은 다른 자식 그림)을 그린 대역이 떨어진 뒤 그 자리에서 켠다.
    // 그래서 물리·AI·전투 구역 등록(ArenaZone)은 평소와 똑같고, 연출이 끝나기 전에 맞거나 움직일 일이 없다.
    // 쓰는 곳: SlimeKnightSpawner(쌍둥이 기사), SlimeKingSpawner(슬라임 킹).
    public class BossEntrance : MonoBehaviour
    {
        private const float DropHeight = 7f;       // 위에서 떨어지기 시작하는 높이 (천장이 낮으면 그 아래까지)
        private const float CeilingMargin = 1.5f;
        private const float FallTime = 0.45f;      // 가속하며 떨어진다 (t²)
        private const float ShakeDuration = 0.25f;
        private const float ShakeMagnitude = 0.18f;
        private const float BannerAbove = 1.2f;    // 이름 띠: 대역 그림 꼭대기보다 이만큼 위

        private GameObject body;
        private Transform standIn;
        private Vector3 landing;
        private float height;
        private float weight;
        private string bannerName;
        private float age;
        private bool landed;

        // 방금 만든 몸에 등장 연출을 건다. 그림이 없으면(단색 사각형 대체) 연출 없이 바로 둔다.
        // weight: 충격파 크기·흔들림 배율 (기사 1, 슬라임 킹 1.7). bannerName: 착지 때 띄울 이름 띠 (없으면 null).
        public static void Play(GameObject bodyObject, float weight = 1f, string bannerName = null)
        {
            if (bodyObject == null) return;
            var visual = bodyObject.transform.Find("Visual");
            var source = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (source == null || source.sprite == null) return;

            var go = new GameObject("BossEntrance");
            go.transform.SetParent(bodyObject.transform.parent, true); // 방이 치워지면 함께 사라진다 (부모가 나중에 정해지면 Update에서 따라간다)
            var entrance = go.AddComponent<BossEntrance>();
            entrance.body = bodyObject;
            entrance.landing = bodyObject.transform.position;
            entrance.weight = weight;
            entrance.bannerName = bannerName;

            // 천장이 낮으면 천장 아래에서 떨어진다
            float drop = DropHeight;
            int groundMask = 1 << TrapCommon.GroundLayer;
            var hit = Physics2D.Raycast(visual.position + Vector3.up * 0.5f, Vector2.up, DropHeight + CeilingMargin, groundMask);
            if (hit.collider != null) drop = Mathf.Max(0f, hit.distance + 0.5f - CeilingMargin);
            entrance.height = drop;

            // 대역: 몸 위치를 원점으로, 몸 아래의 그림(Visual + 왕관 등)을 같은 월드 위치·크기로 복사한다
            var standIn = new GameObject("StandIn").transform;
            standIn.SetParent(go.transform, false);
            standIn.position = bodyObject.transform.position + Vector3.up * drop;
            foreach (var renderer in bodyObject.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.sprite == null || !renderer.enabled) continue;
                var copy = new GameObject(renderer.name).AddComponent<SpriteRenderer>();
                copy.transform.SetParent(standIn, false);
                copy.transform.localPosition = renderer.transform.position - bodyObject.transform.position;
                copy.transform.localScale = renderer.transform.lossyScale;
                copy.sprite = renderer.sprite;
                copy.flipX = renderer.flipX;
                copy.color = renderer.color;
                copy.sortingLayerID = renderer.sortingLayerID;
                copy.sortingOrder = renderer.sortingOrder;
            }
            entrance.standIn = standIn;

            bodyObject.SetActive(false);
            if (drop < 0.5f) entrance.Land(); // 떨어질 자리가 없으면 바로 착지 연출만
        }

        private void Update()
        {
            if (landed) return;
            if (body == null) { Destroy(gameObject); return; } // 몸이 치워졌으면 (방 이동 등) 대역도 치운다
            if (transform.parent != body.transform.parent) transform.SetParent(body.transform.parent, true); // 스포너가 만든 뒤 부모를 정한다

            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / FallTime);
            standIn.position = landing + Vector3.up * (height * (1f - t * t));
            standIn.localScale = new Vector3(1f, 1f + 0.15f * t, 1f); // 떨어지는 동안 세로로 살짝 늘어난다 (속도감, 발 기준)
            if (t >= 1f) Land();
        }

        private void Land()
        {
            if (landed) return;
            landed = true;
            if (body == null) { Destroy(gameObject); return; }

            var top = TopOfStandIn();
            var feet = body.transform.position; // 스폰 위치 = 발밑 (RoomShaper가 바닥 위 자리를 준다)
            body.SetActive(true);
            EnemySquash.For(body).Land();

            VfxPlayer.Shockwave(feet, 1.3f * weight);
            VfxPlayer.Dust(feet, 1f);
            VfxPlayer.HelmetGlint(top + Vector3.down * 0.25f);
            if (!string.IsNullOrEmpty(bannerName)) VfxPlayer.RelicBanner(top + Vector3.up * BannerAbove, bannerName);
            CameraFollow.ShakeMain(ShakeDuration * Mathf.Sqrt(weight), ShakeMagnitude * weight);
            GameAudio.PlaySfx("Hit_Earth", 0.9f); // 쿵 (전용 착지음이 없어 땅 적중음)
            Destroy(gameObject);
        }

        // 대역 그림 전체의 꼭대기 가운데 (투구·왕관 끝). 그림이 없으면 몸 위치 + 1.
        private Vector3 TopOfStandIn()
        {
            if (standIn != null) standIn.localScale = Vector3.one; // 늘어난 채로 재지 않게
            var renderers = standIn != null ? standIn.GetComponentsInChildren<SpriteRenderer>() : null;
            if (renderers == null || renderers.Length == 0) return body.transform.position + Vector3.up;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return new Vector3(bounds.center.x, bounds.max.y, 0f);
        }
    }
}
