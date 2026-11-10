using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 시간 무적(HealthSystem.GraceActive, 유물 그림자 베일) 동안 마법사 그림이 lilac으로 물들어 빠르게 깜빡이고 보라 잔상을 남긴다.
    // 무적이 끝나면 원래 색으로 되돌린다. PlayerRelics가 그림자 베일을 얻을 때 붙인다.
    public class GraceBlink : MonoBehaviour
    {
        private const float BlinkRate = 14f;        // 초당 깜빡임 수
        private const float GhostInterval = 0.08f;
        private static readonly Color Lilac = new Color(0.75f, 0.62f, 1f); // palette.py lilac

        private HealthSystem health;
        private SpriteRenderer visual;
        private Color baseColor = Color.white;
        private bool active;
        private float nextGhost;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            var child = transform.Find("Visual");
            visual = child != null ? child.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (health == null || visual == null) return;

            bool grace = health.GraceActive && !health.IsDead;
            if (!grace)
            {
                Restore();
                return;
            }

            if (!active)
            {
                active = true;
                baseColor = visual.color;
                nextGhost = 0f;
            }

            bool dim = Mathf.Repeat(Time.time * BlinkRate, 1f) < 0.5f;
            var tint = Color.Lerp(baseColor, Lilac, 0.45f);
            tint.a = baseColor.a * (dim ? 0.45f : 0.9f);
            visual.color = tint;

            if (Time.time >= nextGhost)
            {
                Afterimage.Spawn(visual, Lilac, 0.45f, 0.2f);
                nextGhost = Time.time + GhostInterval;
            }
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (!active) return;
            active = false;
            if (visual != null) visual.color = baseColor;
        }
    }
}
