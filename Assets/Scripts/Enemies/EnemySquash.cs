using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 적 공격 예고 squash & stretch (Docs/ArtStyle.md "포즈는 과장되게"): 공격 직전 세로로 웅크렸다가(-15%)
    // 공격 순간 길게 늘어난다(+20%, 0.18초에 걸쳐 원래대로). 그림(Visual 자식)의 크기만 바꾸고 발 위치는 그대로 둔다.
    // 슬라임처럼 그림 윤곽으로 몸 판정을 만드는 적(SpriteColliderFitter)은 판정이 같이 줄지 않게 원래 크기를 알려 준다.
    public class EnemySquash : MonoBehaviour
    {
        public const float WindupSquash = 0.15f;
        public const float ReleaseStretch = 0.2f;
        private const float ReleaseTime = 0.18f;

        private Transform visual;
        private SpriteRenderer spriteRenderer;
        private Vector3 baseScale;
        private Vector3 basePosition;
        private bool active;

        private float from;       // 이 구간을 시작할 때의 양 (양수: 늘어남, 음수: 웅크림)
        private float to;
        private float duration;
        private float elapsed;
        private bool easeOut;     // true: 처음에 빨리 (웅크림), false: 끝에 갈수록 0으로 (늘어났다 돌아오기)

        // 컨트롤러가 처음 쓸 때 붙인다 (스포너·프리팹을 따로 고치지 않아도 되게).
        public static EnemySquash For(GameObject go)
        {
            var squash = go.GetComponent<EnemySquash>();
            return squash != null ? squash : go.AddComponent<EnemySquash>();
        }

        // 공격 예비 동작 시작: time 동안 웅크린다.
        public void Windup(float time)
        {
            if (!Capture()) return;
            Begin(Current(), -WindupSquash, Mathf.Max(0.01f, time), true);
        }

        // 공격 순간: 늘어났다가 돌아온다.
        public void Release()
        {
            if (!Capture()) return;
            Begin(ReleaseStretch, 0f, ReleaseTime, false);
        }

        // 공격이 끊겼을 때 (경직·사망) 바로 원래 모양으로.
        public void Cancel()
        {
            if (!active) return;
            active = false;
            Restore();
        }

        private bool Capture()
        {
            if (visual == null)
            {
                visual = transform.Find("Visual");
                if (visual == null) return false;
                spriteRenderer = visual.GetComponent<SpriteRenderer>();
            }

            if (!active)
            {
                // 연출 중이 아닐 때의 크기가 원래 크기다 (스포너가 정한 Visual 크기)
                baseScale = visual.localScale;
                basePosition = visual.localPosition;
                var fitter = GetComponent<SpriteColliderFitter>();
                if (fitter != null) fitter.SetRestPose(basePosition, baseScale);
            }

            return true;
        }

        private void Begin(float start, float end, float time, bool fast)
        {
            from = start;
            to = end;
            duration = time;
            elapsed = 0f;
            easeOut = fast;
            active = true;
        }

        private float Current()
        {
            if (!active) return 0f;
            float t = Mathf.Clamp01(elapsed / duration);
            float k = easeOut ? 1f - (1f - t) * (1f - t) : t * t * (3f - 2f * t);
            return Mathf.Lerp(from, to, k);
        }

        private void LateUpdate()
        {
            if (!active || visual == null) return;

            elapsed += Time.deltaTime;
            float amount = Current();
            if (elapsed >= duration && Mathf.Approximately(to, 0f))
            {
                active = false;
                Restore();
                return;
            }

            float sy = 1f + amount;
            float sx = 1f - amount * 0.6f; // 부피가 대략 유지되게
            visual.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);

            // 발(그림의 아래 끝)이 제자리에 남도록 그림 원점을 옮긴다
            float feet = spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.min.y * baseScale.y : 0f;
            visual.localPosition = new Vector3(basePosition.x, basePosition.y + feet * (1f - sy), basePosition.z);
        }

        private void Restore()
        {
            if (visual == null) return;
            visual.localScale = baseScale;
            visual.localPosition = basePosition;
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}
