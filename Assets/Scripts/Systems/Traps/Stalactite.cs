using UnityEngine;

namespace IBlameYou.Systems
{
    // 동굴 천장의 종유석: 플레이어가 바로 아래로 오면 잠깐 흔들려 예고하고 떨어진다.
    // 떨어지는 동안 닿으면 물리 피해(적은 2배), 땅(발판 포함)에 닿거나 너무 멀리 떨어지면 부서진다. 몇 초 뒤 천장에서 다시 자란다.
    // 매달려 있을 때는 몸이 없어서 점프하다 머리를 부딪히지 않는다 (동굴 천장이 바닥 + 5~6칸이라 매달린 칸에 머리가 닿는다).
    public class Stalactite : MonoBehaviour
    {
        private const float TriggerHalfWidth = 1.3f;  // 플레이어가 좌우로 이만큼 안에 있으면
        private const float TriggerDepth = 8f;         // 아래로 이만큼 안에 있으면 떨어진다
        private const float ShakeTime = 0.4f;
        private const float ShakeAmount = 0.07f;
        private const float Gravity = 30f;
        private const float MaxFallSpeed = 18f;
        private const float MaxFallDistance = 20f;     // 낭떠러지로 떨어지면 이만큼 뒤 부서진다
        private const float RegrowDelay = 5f;
        private const float RegrowTime = 0.6f;
        private const float DamageMultiplier = 1f;
        private const float EnemyDamageMultiplier = 2f; // 적에게는 2배: 종유석 아래로 적을 끌어들이면 이득이 되게
        private const float SoundRange = 12f;          // FlameJetTrap과 같다
        private const float TipOffset = 0.45f;         // 그림 가운데에서 뾰족한 끝까지
        private const float LookupInterval = 0.5f;     // 플레이어를 못 찾았을 때 다시 찾는 간격
        private const float ShadowMaxAlpha = 0.55f;    // 그림자 예고: 흔들릴 때 옅게 → 떨어질수록 진하게
        private const float ShadowWidth = 0.9f;
        private const float ShadowHeight = 0.22f;
        private const float ShadowSearch = 24f;        // 이만큼 아래까지 땅을 찾는다 (없으면 그림자 없음)

        // 돌 부스러기 예고: 흔들리는 동안 끝에서 작은 돌가루가 툭툭 떨어진다 (그림자와 함께 "곧 떨어진다")
        private const int PebbleCount = 3;
        private const float PebbleGap = 0.12f;         // 흔들림 0.4초 동안 셋이 차례로
        private const float PebbleLife = 0.45f;
        private const float PebbleGravity = 14f;
        private const float PebbleSize = 0.09f;
        private static readonly Color PebbleColor = new Color(0.62f, 0.57f, 0.68f);

        private enum Phase { Hang, Shake, Fall, Gone, Regrow }

        private Phase phase = Phase.Hang;
        private float timer;
        private float speed;
        private Vector3 home;
        private SpriteRenderer body;
        private Color baseColor;
        private BoxCollider2D hitBox;
        private Transform player;
        private float nextLookup;
        private Vector3 bodyScale;
        private float bodyHalfHeight;
        private SpriteRenderer shadow;
        private float floorY;
        private bool hasFloor;
        private SpriteRenderer[] pebbles;
        private float[] pebbleAge;
        private Vector2[] pebbleVelocity;
        private int nextPebble;

        // position: 매달린 칸의 가운데 (월드). 그 칸 바로 위가 천장이다.
        public static Stalactite Create(Transform parent, Vector2 position)
        {
            var go = new GameObject("Stalactite");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trap = go.AddComponent<Stalactite>();
            trap.home = go.transform.position;
            trap.body = TrapCommon.AddSprite(go.transform, "Body", TrapCommon.Frame("Stalactite"), Vector2.zero, new Vector2(0.5f, 0.9f),
                new Color(0.55f, 0.5f, 0.62f), 2);
            trap.baseColor = trap.body.color;
            trap.bodyScale = trap.body.transform.localScale;
            trap.bodyHalfHeight = trap.body.bounds.extents.y;

            // 떨어질 자리 그림자 (부모 = 방 뿌리라 종유석과 같이 지워진다). 흔들리기 시작할 때 땅을 찾아 놓는다
            var shadowGo = new GameObject("StalactiteShadow");
            shadowGo.transform.SetParent(parent, false);
            trap.shadow = shadowGo.AddComponent<SpriteRenderer>();
            trap.shadow.sprite = SolidSpriteFactory.CreateCircle();
            trap.shadow.color = new Color(0.05f, 0.02f, 0.1f, 0f);
            trap.shadow.sortingOrder = 1;
            trap.shadow.enabled = false;

            // 돌가루 (부모 = 방 뿌리: 종유석이 흔들려도 같이 흔들리지 않게)
            trap.pebbles = new SpriteRenderer[PebbleCount];
            trap.pebbleAge = new float[PebbleCount];
            trap.pebbleVelocity = new Vector2[PebbleCount];
            for (int i = 0; i < PebbleCount; i++)
            {
                var pebbleGo = new GameObject("StalactitePebble");
                pebbleGo.transform.SetParent(parent, false);
                pebbleGo.transform.localScale = new Vector3(PebbleSize, PebbleSize, 1f);
                var pebble = pebbleGo.AddComponent<SpriteRenderer>();
                pebble.sprite = SolidSpriteFactory.CreateSquare();
                pebble.color = PebbleColor;
                pebble.sortingOrder = 2;
                pebble.enabled = false;
                trap.pebbles[i] = pebble;
            }

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            trap.hitBox = go.AddComponent<BoxCollider2D>();
            trap.hitBox.isTrigger = true;
            trap.hitBox.size = new Vector2(0.45f, 0.85f);
            trap.hitBox.enabled = false;
            return trap;
        }

        private void Update()
        {
            UpdatePebbles();
            switch (phase)
            {
                case Phase.Hang:
                    if (PlayerBelow())
                    {
                        Enter(Phase.Shake);
                        FindFloor();
                        if (CameraNear()) GameAudio.PlaySfx("StoneCrack", 0.8f);
                    }
                    break;
                case Phase.Shake:
                    timer += Time.deltaTime;
                    transform.position = home + new Vector3(Mathf.Sin(timer * 70f) * ShakeAmount, 0f, 0f);
                    ShowShadow(0.3f * Mathf.Clamp01(timer / ShakeTime));
                    if (nextPebble < PebbleCount && timer >= nextPebble * PebbleGap) DropPebble(nextPebble++);
                    if (timer >= ShakeTime)
                    {
                        transform.position = home;
                        Enter(Phase.Fall);
                    }
                    break;
                case Phase.Fall:
                    Fall();
                    break;
                case Phase.Gone:
                    timer += Time.deltaTime;
                    if (timer >= RegrowDelay)
                    {
                        transform.position = home;
                        body.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f); // 첫 프레임에 다 자란 채로 번쩍이지 않게
                        body.enabled = true;
                        Enter(Phase.Regrow);
                    }
                    break;
                case Phase.Regrow:
                    timer += Time.deltaTime;
                    float t = Mathf.Clamp01(timer / RegrowTime);
                    body.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * t);
                    // 천장에 붙은 윗면은 그대로 두고 아래로 자란다
                    float grow = Mathf.Lerp(0.3f, 1f, t);
                    body.transform.localScale = new Vector3(bodyScale.x, bodyScale.y * grow, bodyScale.z);
                    body.transform.localPosition = new Vector3(0f, bodyHalfHeight * (1f - grow), 0f);
                    if (t >= 1f) Enter(Phase.Hang);
                    break;
            }
        }

        private void Enter(Phase next)
        {
            phase = next;
            timer = 0f;
            speed = 0f;
            hitBox.enabled = next == Phase.Fall;
            if (next == Phase.Shake) nextPebble = 0;
            if (next != Phase.Shake && next != Phase.Fall && shadow != null) shadow.enabled = false;
        }

        private void DropPebble(int i)
        {
            if (pebbles == null || pebbles[i] == null) return;
            pebbles[i].enabled = true;
            pebbles[i].color = PebbleColor;
            pebbles[i].transform.position = home + new Vector3(Random.Range(-0.12f, 0.12f), -TipOffset, 0f);
            pebbleAge[i] = 0f;
            pebbleVelocity[i] = new Vector2(Random.Range(-0.6f, 0.6f), -0.5f);
        }

        private void UpdatePebbles()
        {
            if (pebbles == null) return;
            for (int i = 0; i < PebbleCount; i++)
            {
                var pebble = pebbles[i];
                if (pebble == null || !pebble.enabled) continue;
                pebbleAge[i] += Time.deltaTime;
                if (pebbleAge[i] >= PebbleLife)
                {
                    pebble.enabled = false;
                    continue;
                }
                pebbleVelocity[i].y -= PebbleGravity * Time.deltaTime;
                pebble.transform.position += (Vector3)(pebbleVelocity[i] * Time.deltaTime);
                float fade = 1f - pebbleAge[i] / PebbleLife;
                pebble.color = new Color(PebbleColor.r, PebbleColor.g, PebbleColor.b, fade);
            }
        }

        private void FindFloor()
        {
            var hit = Physics2D.Raycast((Vector2)home + Vector2.down * TipOffset, Vector2.down, ShadowSearch, 1 << TrapCommon.GroundLayer);
            hasFloor = hit.collider != null;
            if (hasFloor) floorY = hit.point.y;
        }

        // alpha: 0~1 (ShadowMaxAlpha 비율). 땅이 가까워질수록 그림자가 조금 커진다
        private void ShowShadow(float alpha)
        {
            if (shadow == null || !hasFloor) return;
            shadow.enabled = true;
            shadow.transform.position = new Vector3(home.x, floorY + 0.04f, 0f);
            float size = Mathf.Lerp(0.6f, 1f, alpha);
            shadow.transform.localScale = new Vector3(ShadowWidth * size, ShadowHeight * size, 1f);
            shadow.color = new Color(0.05f, 0.02f, 0.1f, ShadowMaxAlpha * alpha);
        }

        private bool PlayerBelow()
        {
            if (player == null)
            {
                if (Time.time < nextLookup) return false;
                nextLookup = Time.time + LookupInterval;
                var movement = FindFirstObjectByType<Player.PlayerMovement>();
                if (movement == null) return false;
                player = movement.transform;
            }

            Vector2 offset = player.position - home;
            return Mathf.Abs(offset.x) <= TriggerHalfWidth && offset.y < 0f && -offset.y <= TriggerDepth;
        }

        private void Fall()
        {
            speed = Mathf.Min(speed + Gravity * Time.deltaTime, MaxFallSpeed);
            float step = speed * Time.deltaTime;
            Vector2 tip = (Vector2)transform.position + Vector2.down * TipOffset;
            var hit = Physics2D.Raycast(tip, Vector2.down, step, 1 << TrapCommon.GroundLayer);
            if (hit.collider != null)
            {
                transform.position += Vector3.down * hit.distance;
                Shatter();
                return;
            }

            transform.position += Vector3.down * step;
            if (hasFloor)
            {
                float total = home.y - TipOffset - floorY;
                float left = transform.position.y - TipOffset - floorY;
                ShowShadow(total > 0.01f ? Mathf.Lerp(0.3f, 1f, 1f - Mathf.Clamp01(left / total)) : 1f);
            }
            if (home.y - transform.position.y >= MaxFallDistance) Shatter();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (phase != Phase.Fall) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null)
            {
                StrikeEnemy(other);
                return;
            }

            var health = player.GetComponent<HealthSystem>();
            if (health == null || health.IsDead || health.IsInvulnerable) return;

            TrapCommon.Strike(player, DamageKind.Physical, Element.Neutral, DamageMultiplier);
            Shatter();
        }

        // 떨어지는 종유석은 적도 맞힌다 (함정 중 유일하게: 플레이어가 적을 아래로 끌어들일 수 있다).
        // 적의 감지 범위 같은 트리거 콜라이더는 몸이 아니므로 무시한다. 맞히면 부서진다.
        private void StrikeEnemy(Collider2D other)
        {
            if (other == null || other.isTrigger) return;
            var enemy = other.GetComponentInParent<IBlameYou.Enemies.EnemyController>();
            if (enemy == null) return;
            var health = enemy.GetComponent<HealthSystem>();
            if (health == null || health.IsDead || health.IsInvulnerable) return;

            DamageCalculator.Strike(TrapCommon.Source, enemy, DamageKind.Physical, Element.Neutral, EnemyDamageMultiplier);
            PlayEnemyBonk(other.bounds);
            Shatter();
        }

        // 적을 맞혔을 때만 나는 연출: 머리 위 노란 "쾅!" + 머리에 부딪히는 번쩍 + 아주 짧은 흔들림.
        // 플레이어가 맞을 때(빨간 피해 숫자·피격 흔들림)와 달리 노란 글자라 "적을 끌어들여 맞혔다"가 보인다.
        private static readonly Color BonkColor = new Color(1f, 0.86f, 0.3f);

        private void PlayEnemyBonk(Bounds enemyBounds)
        {
            var head = new Vector3(enemyBounds.center.x, enemyBounds.max.y, 0f);
            EffectLibrary.PlayHit(Element.Neutral, head, 1.2f);
            DamageNumbers.ShowText(head + Vector3.up * 0.55f, "쾅!", BonkColor, 0.9f);
            if (CameraNear()) IBlameYou.Core.CameraFollow.ShakeMain(0.1f, 0.06f);
        }

        private void Shatter()
        {
            var tip = transform.position + Vector3.down * TipOffset;
            var dust = EffectLibrary.CreateVisual(null, "StalactiteDust", TrapCommon.Frames("Smoke"), 16f, false,
                new Color(0.7f, 0.66f, 0.75f, 0.7f), 0.6f, 6, true);
            dust.transform.position = tip;
            if (CameraNear()) GameAudio.PlaySfx("RockShatter", 0.75f);

            body.enabled = false;
            body.color = baseColor;
            Enter(Phase.Gone);
        }

        private bool CameraNear()
        {
            var cam = Camera.main;
            return cam != null && Mathf.Abs(cam.transform.position.x - transform.position.x) < SoundRange;
        }
    }
}
