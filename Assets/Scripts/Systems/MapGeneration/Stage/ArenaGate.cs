using UnityEngine;

namespace IBlameYou.Systems
{
    // 전투 구역 장벽(ArenaZone이 만든다)의 그림 연출. 충돌은 ArenaZone이 붙인 BoxCollider2D가 처음부터 맡고, 여기서는 그림만 움직인다.
    // - 닫힐 때: 그림이 DropHeight 위에서 DropTime 동안 떨어져 바닥에 닿으면 먼지 (2026-11-13 전투 구역 연출)
    // - 닫힌 동안: 그림이 숨쉬듯 천천히 밝아졌다 어두워진다
    // - 열릴 때(Open): 충돌을 바로 풀고 그림만 OpenTime 동안 위로 올라가며 투명해진 뒤 장벽째 사라진다
    public class ArenaGate : MonoBehaviour
    {
        public const float DropTime = 0.18f;
        public const float DropHeight = 4f;
        public const float OpenTime = 0.35f;
        public const float OpenRise = 1.5f;
        private const float PulseSpeed = 2.4f;   // 라디안/초 (한 번 숨쉬는 데 약 2.6초)
        private const float PulseMin = 0.6f;     // 가장 어두울 때 원래 알파의 배율

        private SpriteRenderer visual;
        private Color baseColor;
        private Vector3 restLocal;  // 그림이 바닥에 닿았을 때 자리 (부모 = 장벽 기준)
        private float footY;        // 장벽 바닥 y (월드)
        private float started;
        private float openedAt = -1f;
        private float openAlpha;    // 열리기 시작할 때 알파 (숨쉬기 중간에서 끊김 없이 사라지게)
        private bool landed;

        public static ArenaGate Attach(GameObject gate, SpriteRenderer visual, float footY)
        {
            var fx = gate.AddComponent<ArenaGate>();
            fx.visual = visual;
            fx.baseColor = visual != null ? visual.color : Color.white;
            fx.restLocal = visual != null ? visual.transform.localPosition : Vector3.zero;
            fx.footY = footY;
            fx.started = Time.time;
            if (visual != null) visual.transform.localPosition = fx.restLocal + Vector3.up * DropHeight;
            return fx;
        }

        // 구역 돌파: 막힘은 바로 풀고 그림만 걷힌다.
        public void Open()
        {
            if (openedAt >= 0f) return;
            openedAt = Time.time;
            openAlpha = visual != null ? visual.color.a : 0f;
            foreach (var box in GetComponents<Collider2D>()) box.enabled = false;
            if (visual == null) Destroy(gameObject);
        }

        private void Update()
        {
            if (visual == null) return;

            if (openedAt >= 0f)
            {
                float k = Mathf.Clamp01((Time.time - openedAt) / OpenTime);
                visual.transform.localPosition = restLocal + Vector3.up * (OpenRise * k * k);
                visual.color = WithAlpha(openAlpha * (1f - k));
                if (k >= 1f) Destroy(gameObject);
                return;
            }

            if (!landed)
            {
                float k = Mathf.Clamp01((Time.time - started) / DropTime);
                visual.transform.localPosition = restLocal + Vector3.up * (DropHeight * (1f - k * k)); // 점점 빨라지며 떨어짐
                if (k < 1f) return;
                landed = true;
                VfxPlayer.Dust(new Vector3(transform.position.x, footY, 0f), 1f);
            }

            // 숨쉬기: 1 ↔ PulseMin
            float pulse = Mathf.Lerp(PulseMin, 1f, 0.5f + 0.5f * Mathf.Cos((Time.time - started) * PulseSpeed));
            visual.color = WithAlpha(baseColor.a * pulse);
        }

        private Color WithAlpha(float a) => new Color(baseColor.r, baseColor.g, baseColor.b, a);
    }
}
