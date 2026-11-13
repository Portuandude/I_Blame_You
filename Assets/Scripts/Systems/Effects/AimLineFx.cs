using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 적 조준선 (고블린 궁수): 화살이 날아갈 쪽으로 옅은 노란 점선. 주인이 Aim으로 시작점·목표·진하기를 매 프레임 알려 주고
    // (플레이어를 따라 움직인다), 쏘거나 끊기면 Remove. 벽에 막히면 거기까지만. 주인이 사라지면 스스로 없어진다.
    public class AimLineFx : MonoBehaviour
    {
        private const float DotSpacing = 0.6f;
        private const float DotLength = 0.2f;
        private const float DotThickness = 0.08f;
        private const float MinAlpha = 0.2f;
        private const float MaxAlpha = 0.55f;
        private const float BlinkFrom = 0.7f; // 이만큼 진행되면 깜빡이기 시작 (곧 쏜다)
        private static readonly Color DotColor = new Color(1f, 0.85f, 0.35f);

        private readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        private Color color = DotColor;
        private GameObject owner;
        private int shown;
        private float strength;

        // color: 없으면 노란색 (슬라임 킹 촉수는 보라).
        public static AimLineFx Create(GameObject owner, Color? color = null)
        {
            var go = new GameObject("AimLine");
            var fx = go.AddComponent<AimLineFx>();
            fx.owner = owner;
            if (color.HasValue) fx.color = color.Value;
            return fx;
        }

        // from: 화살이 나가는 점, to: 노리는 점, maxLength: 화살이 갈 수 있는 거리, blockers: 화살을 막는 층.
        // 점선은 노리는 점까지만 (그 뒤로 길게 늘어지지 않게). strength 0~1: 시위를 당길수록 진해진다.
        public void Aim(Vector2 from, Vector2 to, float maxLength, LayerMask blockers, float strength01)
        {
            strength = Mathf.Clamp01(strength01);
            Vector2 delta = to - from;
            float length = Mathf.Min(delta.magnitude, maxLength);
            if (length < 0.01f)
            {
                SetShown(0);
                return;
            }

            Vector2 dir = delta.normalized;
            if (blockers.value != 0)
            {
                var hit = Physics2D.Raycast(from, dir, length, blockers);
                if (hit.collider != null) length = hit.distance;
            }

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, 0f, angle);
            int count = 0;
            for (float d = DotSpacing * 0.5f; d < length; d += DotSpacing)
            {
                var dot = DotAt(count++);
                dot.transform.position = from + dir * d;
                dot.transform.rotation = rotation;
            }
            SetShown(count);
        }

        public void Remove()
        {
            if (this != null) Destroy(gameObject);
        }

        private void Update()
        {
            if (owner == null || !owner.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, strength);
            if (strength >= BlinkFrom && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f) alpha *= 0.45f;
            var c = new Color(color.r, color.g, color.b, alpha);
            for (int i = 0; i < shown; i++) dots[i].color = c;
        }

        private SpriteRenderer DotAt(int index)
        {
            while (dots.Count <= index)
            {
                var go = new GameObject("Dot");
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(DotLength, DotThickness, 1f);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = SolidSpriteFactory.CreateSquare();
                r.sortingOrder = 2;
                r.color = Color.clear;
                dots.Add(r);
            }
            return dots[index];
        }

        private void SetShown(int count)
        {
            for (int i = 0; i < dots.Count; i++) dots[i].enabled = i < count;
            shown = count;
        }
    }
}
