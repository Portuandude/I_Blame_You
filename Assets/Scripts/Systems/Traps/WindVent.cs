using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 바람 구멍 (FeatureKind.WindVent, 2026-11-14 레벨 재설계 2차 "세로 방 다양화").
    // 바닥 환풍구에서 위로 바람 기둥이 솟는다. 기둥 안에 들어간 플레이어를 위로 밀어 올리고(초속 RiseSpeed까지),
    // 꼭대기 FadeHeight칸에서는 바람이 약해져 둥실 떠 있다가 옆으로 빠져나가면 떨어진다. 덤 길이라 검증기는 무시한다.
    // 대시 중이면 밀지 않는다 (대시가 중력·속도를 직접 다룬다). 적은 영향을 받지 않는다.
    public class WindVent : MonoBehaviour
    {
        public const float RiseSpeed = 9f;      // 기둥 아래·가운데에서 이 속도까지 올린다
        public const float FadeHeight = 2f;     // 꼭대기에서 이만큼 아래부터 목표 속도가 0까지 줄어든다
        private const float Accel = 70f;        // 목표 속도로 다가가는 가속 (중력 3배 ≈ 29.4보다 커야 오른다)
        private const float EnterGap = 0.3f;    // 이만큼 기둥 밖에 있다가 다시 들어오면 "들어감" (소리·먼지)
        private const float GrateHeight = 0.28f;
        private static readonly Color GrateColor = new Color(0.32f, 0.36f, 0.42f);
        private static readonly Color SlotColor = new Color(0.14f, 0.16f, 0.2f);
        private static readonly Color RimColor = new Color(0.62f, 0.9f, 0.7f); // 바람 속성 연초록 테두리

        private float width;
        private float height;
        private float lastPushStep = -1f;
        private float lastInside = -10f;

        public float Top => transform.position.y + height;

        // feet: 기둥 왼쪽 아래 (바닥 윗면, 월드). width·height: 기둥 칸 수.
        public static WindVent Create(Transform parent, Vector2 feet, int width, int height)
        {
            var go = new GameObject("WindVent");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(feet.x, feet.y, 0f);
            var vent = go.AddComponent<WindVent>();
            vent.width = Mathf.Max(1, width);
            vent.height = Mathf.Max(2, height);

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(vent.width - 0.1f, vent.height);
            trigger.offset = new Vector2(vent.width * 0.5f, vent.height * 0.5f);

            vent.BuildGrate();
            WindVentFx.Attach(vent);
            return vent;
        }

        // 바닥에 박힌 환풍구: 어두운 틀 + 세로 틈 + 연초록 테두리 (밟고 서는 바닥은 그대로 타일)
        private void BuildGrate()
        {
            var square = SolidSpriteFactory.CreateSquare();
            Part("Rim", square, new Vector2(width * 0.5f, GrateHeight * 0.5f), new Vector2(width + 0.12f, GrateHeight + 0.08f), RimColor, 1);
            Part("Grate", square, new Vector2(width * 0.5f, GrateHeight * 0.5f), new Vector2(width, GrateHeight), GrateColor, 2);
            int slots = Mathf.RoundToInt(width * 3f);
            for (int i = 0; i < slots; i++)
            {
                float x = (i + 0.5f) * width / slots;
                Part("Slot", square, new Vector2(x, GrateHeight * 0.5f), new Vector2(0.08f, GrateHeight * 0.6f), SlotColor, 3);
            }
        }

        private void Part(string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null || other.isTrigger) return;
            if (Mathf.Approximately(lastPushStep, Time.fixedTime)) return; // 몸 콜라이더가 여럿이어도 한 물리 단계에 한 번만
            if (player.IsDashing) return;
            var health = player.GetComponent<HealthSystem>();
            if (health != null && health.IsDead) return;
            var body = player.GetComponent<Rigidbody2D>();
            if (body == null) return;
            lastPushStep = Time.fixedTime;

            bool entered = Time.time - lastInside > EnterGap;
            lastInside = Time.time;
            if (entered)
            {
                player.ResetAirborne(); // 낮은 점프 끊기가 바람 속도를 깎지 않게, 코요테 점프가 끼어들지 않게
                WindVentFx.Enter(this, new Vector3(other.bounds.center.x, other.bounds.min.y, 0f));
            }

            float strength = Mathf.Clamp01((Top - other.bounds.min.y) / FadeHeight);
            float target = RiseSpeed * strength;
            var v = body.linearVelocity;
            if (v.y < target) body.linearVelocity = new Vector2(v.x, Mathf.MoveTowards(v.y, target, Accel * Time.fixedDeltaTime));
        }

        public float Width => width;
        public float Height => height;
    }
}
