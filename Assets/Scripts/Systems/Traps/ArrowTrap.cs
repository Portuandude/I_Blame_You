using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 벽에 박힌 화살 발사대. 플레이어가 사정거리 안(같은 높이 근처)에 있으면 일정 간격으로 직선 화살을 쏜다 (유도 없음).
    // 쏘기 직전 발사대가 붉게 깜빡여 예고한다. 예고가 시작되면 "끼릭" 소리(TrapWarn)가 나고,
    // 예고 동안 화살이 날아갈 줄을 따라 옅은 붉은 점선이 깜빡인다 (벽에 막히면 거기까지).
    public class ArrowTrap : MonoBehaviour
    {
        private const float Interval = 2.6f;
        private const float WarnTime = 0.45f;
        private const float Range = 14f;
        private const float HeightTolerance = 3f;
        private const float ArrowSpeed = 9f;
        private const float DamageMultiplier = 0.8f;
        private const float MuzzleOffset = 0.6f;
        private const float DotSpacing = 0.7f;
        private const float DotSize = 0.14f;
        private const float SoundRange = 14f;
        private static readonly Color DotColor = new Color(1f, 0.35f, 0.3f);

        private float direction;
        private float timer;
        private SpriteRenderer body;
        private Color baseColor;
        private Transform player;
        private bool warned;

        // 지금 예고 중인(곧 쏘는) 발사대. 화면 밖이면 HUD가 가장자리에 "!"를 띄운다 (SkillHUD.EnemyGuide.cs DrawAimWarnings).
        public static readonly HashSet<ArrowTrap> Warning = new HashSet<ArrowTrap>();
        private Transform dots;

        // position: 발사대 칸의 가운데 (월드). direction: -1 왼쪽, 1 오른쪽.
        public static ArrowTrap Create(Transform parent, Vector2 position, int direction)
        {
            var go = new GameObject("ArrowTrap");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trap = go.AddComponent<ArrowTrap>();
            trap.direction = direction < 0 ? -1f : 1f;
            trap.body = TrapCommon.AddSprite(go.transform, "Body", TrapCommon.Frame("ArrowTrap"), Vector2.zero, new Vector2(1f, 1f),
                new Color(0.45f, 0.4f, 0.38f), 1);
            trap.body.flipX = trap.direction > 0f;
            trap.baseColor = trap.body.color;
            trap.timer = Interval * 0.6f;
            return trap;
        }

        private void Update()
        {
            if (player == null)
            {
                var movement = FindFirstObjectByType<Player.PlayerMovement>();
                if (movement == null) return;
                player = movement.transform;
            }

            if (!InRange())
            {
                timer = Mathf.Min(timer, Interval - WarnTime - 0.3f); // 다시 들어오면 바로 쏘지 않고 예고부터
                body.color = baseColor;
                SetWarning(false);
                return;
            }

            timer += Time.deltaTime;
            bool warning = timer >= Interval - WarnTime;
            bool blinkOn = Mathf.Repeat(Time.time * 10f, 1f) < 0.5f;
            body.color = warning && blinkOn ? new Color(1f, 0.45f, 0.4f) : baseColor;
            SetWarning(warning);
            if (warning && dots != null)
            {
                float alpha = blinkOn ? 0.55f : 0.25f;
                foreach (Transform dot in dots)
                {
                    var r = dot.GetComponent<SpriteRenderer>();
                    if (r != null) r.color = new Color(DotColor.r, DotColor.g, DotColor.b, alpha);
                }
            }

            if (timer >= Interval)
            {
                timer = 0f;
                SetWarning(false);
                Fire();
            }
        }

        // 예고 시작·끝. 시작할 때 소리와 점선을 만든다 (발판이 움직이거나 벽이 무너질 수 있으니 매번 다시 잰다).
        private void SetWarning(bool on)
        {
            if (on == warned) return;
            warned = on;
            if (on) Warning.Add(this);
            else Warning.Remove(this);
            if (dots != null)
            {
                Destroy(dots.gameObject);
                dots = null;
            }
            if (!on) return;

            if (CameraNear()) GameAudio.PlaySfx("TrapWarn", 0.7f);
            BuildDots();
        }

        private void BuildDots()
        {
            var muzzle = (Vector2)transform.position + new Vector2(direction * MuzzleOffset, 0.05f);
            float length = Range;
            var hit = Physics2D.Raycast(muzzle, new Vector2(direction, 0f), Range, 1 << TrapCommon.GroundLayer);
            if (hit.collider != null) length = hit.distance;

            var go = new GameObject("WarnLine");
            go.transform.SetParent(transform, false);
            dots = go.transform;
            var square = SolidSpriteFactory.CreateSquare();
            for (float d = DotSpacing * 0.5f; d < length; d += DotSpacing)
            {
                var dot = new GameObject("Dot");
                dot.transform.SetParent(dots, false);
                dot.transform.position = muzzle + new Vector2(direction * d, 0f);
                dot.transform.localScale = new Vector3(DotSize * 1.6f, DotSize, 1f);
                var r = dot.AddComponent<SpriteRenderer>();
                r.sprite = square;
                r.color = new Color(DotColor.r, DotColor.g, DotColor.b, 0.4f);
                r.sortingOrder = 2;
            }
        }

        private void OnDisable() => SetWarning(false);

        private void OnDestroy() => Warning.Remove(this);

        private bool CameraNear()
        {
            var cam = Camera.main;
            return cam != null && Vector2.Distance(cam.transform.position, transform.position) < SoundRange;
        }

        private bool InRange()
        {
            Vector2 offset = player.position - transform.position;
            return offset.x * direction > 0f && Mathf.Abs(offset.x) <= Range && Mathf.Abs(offset.y) <= HeightTolerance;
        }

        private void Fire()
        {
            var go = new GameObject("TrapArrow");
            go.transform.position = transform.position + new Vector3(direction * MuzzleOffset, 0.05f, 0f);
            EffectLibrary.CreateVisual(go.transform, "Visual", EffectLibrary.ArrowFrames, 12f, true, new Color(0.9f, 0.8f, 0.5f), 0.5f);
            go.AddComponent<Projectile>().Launch(TrapCommon.Source, false, DamageKind.Physical, Element.Neutral, new Vector2(direction, 0f),
                ArrowSpeed, 0.2f, Range * 1.3f, false, DamageMultiplier);
        }
    }
}
