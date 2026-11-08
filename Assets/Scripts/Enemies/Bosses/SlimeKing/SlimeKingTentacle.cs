using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 킹 패턴 3의 촉수 한 가닥. 킹 몸에서 곧게 뻗어 나가다가
    // - 벽/바닥에 닿거나 사거리 끝에 가면 되감긴다.
    // - 플레이어에 닿으면 붙는다: 처음 SlowTime초 동안 이동속도 절반, GrabTime초 안에 BreakDistance 넘게 멀어지지 못하면
    //   킹 앞으로 끌려가 피해를 받고 잠깐 경직된다 (그랩). 멀어지면 촉수가 끊어진다.
    public class SlimeKingTentacle : MonoBehaviour
    {
        private enum State { Extend, Retract, Tethered, Done }

        private const float Speed = 12f;
        private const float RetractSpeed = 18f;
        private const float TipRadius = 0.35f;
        private const float SlowTime = 2f;
        private const float SlowMultiplier = 0.5f;
        private const float GrabTime = 5f;
        private const float BreakDistance = 11f;
        private const float PullTime = 0.35f;
        private const float GrabDamageMultiplier = 1.2f;
        private const float GrabStun = 1f;

        private SlimeKingController king;
        private LineRenderer line;
        private Transform tip;
        private Vector2 direction;
        private float range;
        private float length;
        private State state;
        private LayerMask groundMask;

        private PlayerMovement player;
        private CombatStats playerStats;
        private float tetherTime;
        private bool slowed;

        public bool Finished => state == State.Done;
        public bool IsTethered => state == State.Tethered;

        public static SlimeKingTentacle Launch(SlimeKingController king, Vector2 direction, float range, LayerMask groundMask)
        {
            var go = new GameObject("SlimeKingTentacle");
            go.transform.SetParent(king.transform.parent, true);
            var tentacle = go.AddComponent<SlimeKingTentacle>();
            tentacle.king = king;
            tentacle.direction = direction.normalized;
            tentacle.range = range;
            tentacle.groundMask = groundMask;
            tentacle.state = State.Extend;
            tentacle.BuildVisual();
            return tentacle;
        }

        private void BuildVisual()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startWidth = 0.32f;
            line.endWidth = 0.16f;
            line.startColor = new Color(0.55f, 0.3f, 0.9f, 0.95f);
            line.endColor = new Color(0.75f, 0.5f, 1f, 0.95f);
            line.sortingOrder = 3;
            line.numCapVertices = 4;

            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(transform, false);
            var renderer = tipGo.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateCircle();
            renderer.color = new Color(0.75f, 0.5f, 1f);
            renderer.sortingOrder = 4;
            tipGo.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            tip = tipGo.transform;
            UpdateVisual(Origin);
        }

        private Vector2 Origin => king != null ? king.TentacleOrigin : (Vector2)transform.position;

        // 다른 촉수가 붙었거나 패턴이 끝나 거둬들일 때.
        public void Retract()
        {
            if (state == State.Extend) state = State.Retract;
        }

        private void Update()
        {
            if (king == null || king.Dead)
            {
                Release();
                Destroy(gameObject);
                return;
            }

            var origin = Origin;
            switch (state)
            {
                case State.Extend:
                    length += Speed * Time.deltaTime;
                    var tipPosition = origin + direction * length;
                    if (TryCatch(tipPosition)) break;
                    if (length >= range || Physics2D.OverlapCircle(tipPosition, TipRadius * 0.5f, groundMask)) state = State.Retract;
                    UpdateVisual(tipPosition);
                    break;

                case State.Retract:
                    length -= RetractSpeed * Time.deltaTime;
                    if (length <= 0f)
                    {
                        state = State.Done;
                        Destroy(gameObject);
                        return;
                    }

                    UpdateVisual(origin + direction * length);
                    break;

                case State.Tethered:
                    UpdateTether(origin);
                    break;
            }
        }

        private bool TryCatch(Vector2 tipPosition)
        {
            foreach (var hit in Physics2D.OverlapCircleAll(tipPosition, TipRadius))
            {
                var target = TrapCommon.PlayerFrom(hit);
                if (target == null) continue;
                var health = target.GetComponent<HealthSystem>();
                if (health == null || health.IsDead) continue;

                player = target;
                playerStats = target.GetComponent<CombatStats>();
                state = State.Tethered;
                tetherTime = 0f;
                if (playerStats != null)
                {
                    playerStats.GrabSlowMultiplier = SlowMultiplier;
                    slowed = true;
                }

                DamageNumbers.ShowText(target.transform.position + Vector3.up * 1.4f, "촉수에 붙잡힘!", new Color(0.8f, 0.55f, 1f), 1f);
                StageEvents.PostNotice($"{BreakDistance:0}칸 넘게 멀어져서 촉수를 끊어라!");
                king.OnTentacleAttached(this);
                return true;
            }

            return false;
        }

        private void UpdateTether(Vector2 origin)
        {
            if (player == null || player.GetComponent<HealthSystem>().IsDead)
            {
                Release();
                state = State.Retract;
                return;
            }

            Vector2 target = PlayerCenter();
            UpdateVisual(target);
            tetherTime += Time.deltaTime;

            if (slowed && tetherTime >= SlowTime) EndSlow();

            float distance = Vector2.Distance(origin, target);
            if (distance > BreakDistance)
            {
                DamageNumbers.ShowText(target + Vector2.up * 1.4f, "촉수를 끊어냈다!", new Color(0.6f, 1f, 0.6f), 1f);
                Release();
                length = distance;
                direction = (target - origin).normalized;
                state = State.Retract;
                return;
            }

            // 비주얼: 남은 시간에 따라 촉수가 점점 붉어진다.
            float danger = Mathf.Clamp01(tetherTime / GrabTime);
            line.endColor = Color.Lerp(new Color(0.75f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f), danger);

            if (tetherTime >= GrabTime)
            {
                state = State.Done;
                king.StartCoroutine(king.GrabRoutine(player, PullTime, GrabDamageMultiplier, GrabStun));
                Release();
                Destroy(gameObject);
            }
        }

        private Vector2 PlayerCenter()
        {
            var body = player.GetComponent<Collider2D>();
            return body != null ? (Vector2)body.bounds.center : (Vector2)player.transform.position;
        }

        private void UpdateVisual(Vector2 end)
        {
            var origin = Origin;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            tip.position = end;
        }

        private void EndSlow()
        {
            if (!slowed) return;
            slowed = false;
            if (playerStats != null) playerStats.GrabSlowMultiplier = 1f;
        }

        private void Release()
        {
            EndSlow();
            if (state == State.Tethered && king != null) king.OnTentacleReleased(this);
            player = null;
        }

        private void OnDestroy()
        {
            EndSlow();
        }
    }
}
