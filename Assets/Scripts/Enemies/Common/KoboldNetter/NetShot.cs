using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 코볼트 그물 (그물 코볼트·코볼트 족장이 던진다). 플레이어에 맞으면 약한 물리 피해 + NetSnare(A만 묶임).
    // 그림은 임시: 화살 프레임이 있으면 갈색으로, 없으면 갈색 사각형 (아트 규칙 — 새 그림은 사람 확인 뒤).
    public static class NetShot
    {
        public const float Speed = 9f;
        public const float HitRadius = 0.35f;
        public const float DamageMultiplier = 0.5f;
        private static readonly Color NetColor = new Color(0.6f, 0.45f, 0.22f);

        public static Projectile Launch(CombatStats owner, Vector2 origin, Vector2 direction, float range)
        {
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            var go = new GameObject("Net");
            go.transform.position = origin;
            var look = EffectLibrary.CreateVisual(go.transform, "Visual", EffectLibrary.ArrowFrames, 12f, true, NetColor, 0.45f);
            if (look != null)
            {
                look.color = NetColor;
                look.transform.localScale = new Vector3(look.transform.localScale.x * 0.8f, look.transform.localScale.y * 1.8f, 1f); // 넓게 펼친 그물
            }
            var net = go.AddComponent<Projectile>();
            net.Launch(owner, false, DamageKind.Physical, Element.Neutral, direction, Speed, HitRadius, range, false, DamageMultiplier);
            net.Hit = health => { if (health != null) NetSnare.Apply(health.gameObject); };
            GameAudio.PlaySfx("Dash", 0.35f, 0.15f);
            return net;
        }
    }
}
