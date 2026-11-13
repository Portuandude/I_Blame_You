using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 쌍둥이 슬라임 기사 광폭화 연출 (그림·소리만, 광폭화 수치·시간은 EnrageAbility 그대로).
    // - LinkSpark: 쓰러진 기사 자리에서 그 기사 원소색 빛 알갱이가 둥글게 날아가 남은 기사에게 닿으면 붉은 큰 고리가 퍼진다
    // - Puff: 머리 위로 솟아 커지며 옅어지는 김 덩이 (광폭화 중 붉은 김, 풀릴 때 회색 김)
    // - Ghost: 광폭화 중 빠르게 움직일 때 몸 그림을 붉게 남기는 잔상
    // 모두 런타임 원 스프라이트(SolidSpriteFactory.CreateCircle)나 지금 몸 스프라이트를 쓰므로 그림 파일이 없다.
    public class KnightEnrageFx : MonoBehaviour
    {
        private static readonly Color RingColor = new Color(1f, 0.3f, 0.25f, 0.9f);

        private enum Kind { Spark, Puff, Ghost }

        private Kind kind;
        private SpriteRenderer sprite;
        private Color baseColor;
        private float lifetime;
        private float age;

        // Spark
        private Vector3 from;
        private Transform target;
        private Vector3 lastTarget;

        // Puff
        private Vector3 drift;
        private float startScale;
        private float endScale;

        public static void LinkSpark(Vector3 fromPosition, Color color, Transform targetKnight)
        {
            if (targetKnight == null) return;
            var fx = Make("KnightLinkSpark", Kind.Spark, fromPosition, SolidSpriteFactory.CreateCircle(), color, 0.45f, 8);
            fx.from = fromPosition;
            fx.target = targetKnight;
            fx.lastTarget = TargetPoint(targetKnight);
            fx.transform.localScale = Vector3.one * 0.45f;

            // 꼬리처럼 따라오는 작은 알갱이 둘 (조금 늦게 출발)
            for (int i = 1; i <= 2; i++)
            {
                var tail = Make("KnightLinkTail", Kind.Spark, fromPosition, SolidSpriteFactory.CreateCircle(), WithAlpha(color, 0.6f), 0.45f, 7);
                tail.from = fromPosition;
                tail.target = targetKnight;
                tail.lastTarget = fx.lastTarget;
                tail.age = -0.05f * i;
                tail.transform.localScale = Vector3.one * (0.3f - 0.08f * i);
            }
        }

        public static void Puff(Vector3 position, Color color, float scale)
        {
            var fx = Make("KnightPuff", Kind.Puff, position, SolidSpriteFactory.CreateCircle(), color, 0.55f, 6);
            fx.drift = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.9f, 1.3f), 0f);
            fx.startScale = 0.22f * scale;
            fx.endScale = 0.55f * scale;
            fx.transform.localScale = Vector3.one * fx.startScale;
        }

        public static void Ghost(SpriteRenderer body, Color color)
        {
            if (body == null || body.sprite == null) return;
            var fx = Make("KnightGhost", Kind.Ghost, body.transform.position, body.sprite, color, 0.25f, body.sortingOrder - 1);
            fx.transform.rotation = body.transform.rotation;
            fx.transform.localScale = body.transform.lossyScale;
            fx.sprite.flipX = body.flipX;
            fx.sprite.flipY = body.flipY;
        }

        private static KnightEnrageFx Make(string name, Kind kind, Vector3 position, Sprite sprite, Color color, float lifetime, int order)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var fx = go.AddComponent<KnightEnrageFx>();
            fx.kind = kind;
            fx.lifetime = lifetime;
            fx.baseColor = color;
            fx.sprite = go.AddComponent<SpriteRenderer>();
            fx.sprite.sprite = sprite;
            fx.sprite.color = color;
            fx.sprite.sortingOrder = order;
            return fx;
        }

        private static Vector3 TargetPoint(Transform knight) => knight.position + Vector3.up * 0.6f;

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < 0f)
            {
                sprite.enabled = false; // 꼬리 알갱이는 출발 전엔 안 보인다
                return;
            }
            sprite.enabled = true;
            float t = Mathf.Clamp01(age / lifetime);

            switch (kind)
            {
                case Kind.Spark:
                    if (target != null) lastTarget = TargetPoint(target); // 남은 기사가 움직여도 따라간다 (사라졌으면 마지막 자리로)
                    float eased = t * t * (3f - 2f * t);
                    var pos = Vector3.Lerp(from, lastTarget, eased);
                    pos.y += Mathf.Sin(t * Mathf.PI) * 1.4f; // 위로 둥글게
                    transform.position = pos;
                    if (t >= 1f)
                    {
                        if (name == "KnightLinkSpark" && target != null)
                        {
                            VfxPlayer.ManaRing(target, Vector3.up * 0.6f, RingColor, 2.4f);
                            CameraFollow.ShakeMain(0.12f, 0.08f);
                        }
                        Destroy(gameObject);
                    }
                    break;

                case Kind.Puff:
                    transform.position += drift * Time.deltaTime;
                    transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
                    sprite.color = WithAlpha(baseColor, baseColor.a * (1f - t));
                    if (t >= 1f) Destroy(gameObject);
                    break;

                case Kind.Ghost:
                    sprite.color = WithAlpha(baseColor, baseColor.a * (1f - t));
                    if (t >= 1f) Destroy(gameObject);
                    break;
            }
        }
    }
}
