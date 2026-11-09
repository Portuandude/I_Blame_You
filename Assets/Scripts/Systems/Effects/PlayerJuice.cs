using IBlameYou.Enemies;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 손맛 이펙트 묶음 (Docs/ArtStyle.md): 착지 먼지, 대쉬 잔상, 레벨업 빛기둥, 적이 쓰러질 때 별·하트 파편.
    // 그림은 Resources/Effects/Fx (Tools/ArtGen/generate_juice_fx.py). 그림이 없으면 해당 이펙트만 건너뛴다.
    public class PlayerJuice : MonoBehaviour
    {
        private const float LandingSpeed = 6f;       // 이보다 빠르게 떨어지다 닿으면 먼지
        private const float AfterimageInterval = 0.035f;
        private const float AfterimageLife = 0.22f;
        private static readonly Color AfterimageColor = new Color(0.5f, 0.97f, 1f, 0.6f); // aqua

        private PlayerMovement movement;
        private PlayerProgression progression;
        private Rigidbody2D body;
        private SpriteRenderer bodyRenderer;
        private bool wasGrounded = true;
        private float fallSpeed;
        private float nextAfterimage;

        public static PlayerJuice For(GameObject player)
        {
            var juice = player.GetComponent<PlayerJuice>();
            return juice != null ? juice : player.AddComponent<PlayerJuice>();
        }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            body = GetComponent<Rigidbody2D>();
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            EnemyController.Killed += OnEnemyKilled;
        }

        private void Start()
        {
            progression = GetComponent<PlayerProgression>();
            if (progression != null) progression.LeveledUp += OnLeveledUp;
        }

        private void OnDisable()
        {
            EnemyController.Killed -= OnEnemyKilled;
        }

        private void OnDestroy()
        {
            if (progression != null) progression.LeveledUp -= OnLeveledUp;
        }

        private void Update()
        {
            if (movement == null) return;

            bool grounded = movement.IsGrounded;
            if (!grounded && body != null) fallSpeed = -body.linearVelocity.y; // 공중 마지막 프레임 속도 (낙하 복구 순간이동은 0으로 만든다)
            if (grounded && !wasGrounded)
            {
                if (fallSpeed >= LandingSpeed) PlayAtFeet(EffectLibrary.LoadRuntimeFrames("Effects/Fx/Dust"), 18f, 1f);
                fallSpeed = 0f;
            }
            wasGrounded = grounded;

            if (movement.IsDashing && Time.time >= nextAfterimage)
            {
                nextAfterimage = Time.time + AfterimageInterval;
                SpawnAfterimage();
            }
        }

        // 지금 그려진 몸 그림을 그대로 복사해 aqua로 물들이고 빠르게 사라지게 한다.
        private void SpawnAfterimage()
        {
            if (bodyRenderer == null || bodyRenderer.sprite == null) return;
            var go = new GameObject("DashAfterimage");
            go.transform.SetPositionAndRotation(bodyRenderer.transform.position, bodyRenderer.transform.rotation);
            go.transform.localScale = bodyRenderer.transform.lossyScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = bodyRenderer.sprite;
            renderer.flipX = bodyRenderer.flipX;
            renderer.sortingLayerID = bodyRenderer.sortingLayerID;
            renderer.sortingOrder = bodyRenderer.sortingOrder; // 지형(0)보다 앞, 몸과 같은 순서에서 z로 뒤에
            go.transform.position += new Vector3(0f, 0f, 0.01f);
            renderer.color = AfterimageColor;
            StartCoroutine(FadeOut(renderer, AfterimageLife));
        }

        private void OnLeveledUp(int level)
        {
            PlayAtFeet(EffectLibrary.LoadRuntimeFrames("Effects/Fx/LevelUp"), 16f, 1f);
        }

        private static void OnEnemyKilled(EnemyController enemy, float experience)
        {
            if (enemy == null) return;
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Fx/DeathPop");
            if (frames == null) return;
            var collider = enemy.GetComponent<Collider2D>();
            Vector3 center = collider != null ? collider.bounds.center : enemy.transform.position + Vector3.up * 0.5f;
            EffectLibrary.PlayOneShot(center, frames, 20f, Color.white, 0.5f);
        }

        // 그림 아래 끝이 발에 오도록 (프레임은 중앙 피벗으로 읽힌다).
        private void PlayAtFeet(Sprite[] frames, float fps, float scale)
        {
            if (frames == null) return;
            Vector3 feet = transform.position;
            var collider = GetComponent<Collider2D>();
            if (collider != null) feet = new Vector3(collider.bounds.center.x, collider.bounds.min.y, 0f);
            float half = frames[0].bounds.size.y * 0.5f * scale;
            EffectLibrary.PlayOneShot(feet + new Vector3(0f, half - 0.05f, 0f), frames, fps, Color.white, 0.5f, false, scale);
        }

        // 잔상처럼 잠깐 보였다 사라지는 그림 (플레이어가 사라져도 남지 않게 Destroy도 예약).
        private System.Collections.IEnumerator FadeOut(SpriteRenderer target, float life)
        {
            Destroy(target.gameObject, life + 0.05f);
            float start = Time.time;
            float startAlpha = target.color.a;
            while (target != null && Time.time - start < life)
            {
                var c = target.color;
                c.a = startAlpha * (1f - (Time.time - start) / life);
                target.color = c;
                yield return null;
            }
        }
    }
}
