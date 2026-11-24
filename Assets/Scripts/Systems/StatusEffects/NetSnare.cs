using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 코볼트 그물에 묶인 플레이어 (NetSnareRules). PlayerControlState.Snared를 켜서 A 조작(이동·점프·대시·순간이동)만 막고,
    // A 키(KeyBindings의 P1 동작 아무거나)를 누를 때마다 0.5초씩 줄인다. B 공격은 막지 않는다.
    // 그림은 임시 (몸을 덮는 갈색 격자 사각형, 아트 규칙 — 새 그림은 사람 확인 뒤).
    public class NetSnare : MonoBehaviour
    {
        private static readonly Color NetColor = new Color(0.55f, 0.4f, 0.2f, 0.75f);

        private PlayerControlState control;
        private HealthSystem health;
        private float startedAt;
        private int presses;
        private bool active;
        private SpriteRenderer look;
        private Vector3 lookScale;

        public bool Active => active;

        public static bool IsSnared(GameObject player)
        {
            var snare = player != null ? player.GetComponent<NetSnare>() : null;
            return snare != null && snare.active;
        }

        // 그물이 플레이어에 닿았을 때. 이미 묶여 있으면 무시 (시간을 되돌리지 않음). 묶였으면 true.
        public static bool Apply(GameObject player)
        {
            if (player == null || player.GetComponent<PlayerMovement>() == null) return false;
            var snare = player.GetComponent<NetSnare>();
            if (snare == null) snare = player.AddComponent<NetSnare>();
            return snare.Begin();
        }

        private void Awake()
        {
            control = GetComponent<PlayerControlState>();
            health = GetComponent<HealthSystem>();
        }

        private bool Begin()
        {
            if (active || control == null || (health != null && health.IsDead)) return false;
            active = true;
            startedAt = Time.time;
            presses = 0;
            control.Snared = true;
            ShowLook();
            GameAudio.PlaySfx("ArenaLock", 0.5f, 0.1f);
            StageEvents.PostNotice($"그물에 걸렸다! A는 {KeyBindings.Name(P1Action.Left)}/{KeyBindings.Name(P1Action.Right)} 등을 연타해 빠져나오기");
            return true;
        }

        private void Update()
        {
            if (!active) return;
            if (health != null && health.IsDead) { End(false); return; }
            if (Time.timeScale > 0f && !control.Locked && RoleInput.Focus != ControlFocus.B && AnyP1Down()) // 창·일시정지·방 이동 잠금 중의 키는 세지 않는다
            {
                presses++;
                if (look != null) look.transform.localScale = lookScale * 1.12f; // 흔들어 빠져나오는 느낌
            }
            else if (look != null) look.transform.localScale = Vector3.Lerp(look.transform.localScale, lookScale, Time.deltaTime * 12f);

            float elapsed = Time.time - startedAt;
            if (look != null)
            {
                var c = NetColor;
                c.a *= 0.4f + 0.6f * NetSnareRules.Fraction(elapsed, presses);
                look.color = c;
            }
            if (NetSnareRules.Free(elapsed, presses)) End(true);
        }

        private static bool AnyP1Down()
        {
            for (int i = 0; i < KeyBindingRules.ActionCount; i++)
                if (KeyBindings.GetDown((P1Action)i)) return true;
            return false;
        }

        private void End(bool escaped)
        {
            active = false;
            if (control != null) control.Snared = false;
            if (look != null) Destroy(look.gameObject);
            look = null;
            if (escaped)
            {
                GameAudio.PlaySfx("Dash", 0.4f, 0.1f);
                EffectLibrary.PlayOneShot(transform.position + Vector3.up * 0.5f, null, 12f, NetColor, 0.9f);
            }
        }

        private void OnDisable()
        {
            if (active) End(false);
        }

        private void ShowLook()
        {
            var body = GetComponent<Collider2D>();
            var go = new GameObject("NetSnareLook");
            go.transform.SetParent(transform, false);
            float height = body != null ? body.bounds.size.y : 1f;
            go.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            look = go.AddComponent<SpriteRenderer>();
            look.sprite = SolidSpriteFactory.CreateSquare();
            look.color = NetColor;
            look.sortingOrder = 8;
            lookScale = new Vector3(body != null ? body.bounds.size.x + 0.3f : 0.8f, height + 0.2f, 1f);
            go.transform.localScale = lookScale;
        }
    }
}
