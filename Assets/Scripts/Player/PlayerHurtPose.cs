using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 마법사 피격 프레임 (Docs/ArtStyle.md "포즈는 과장되게"): 맞은 순간 눈을 질끈 감고 뒤로 젖힌 그림 2장을 잠깐 보여 준다.
    // 그림은 Resources/Player/Hurt (Tools/ArtGen/generate_wizard.py, 중앙 피벗·100px=1유닛으로 다른 마법사 프레임과 같은 틀).
    // 애니메이터 클립을 새로 굽지 않도록 Animator가 그 프레임 그림을 정한 뒤 LateUpdate에서 덮어쓴다.
    // HitStun의 뒤로 젖히기(회전)·PlayerSquash(크기)와는 다른 속성을 쓰므로 함께 겹쳐 보인다. 그림이 없으면 아무것도 하지 않는다.
    [RequireComponent(typeof(HealthSystem))]
    public class PlayerHurtPose : MonoBehaviour
    {
        private const float FirstFrameTime = 0.1f; // 크게 젖힌 그림
        private const float TotalTime = 0.25f;     // 반쯤 돌아온 그림까지

        private HealthSystem health;
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float elapsed = TotalTime;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            var visual = transform.Find("Visual");
            if (visual != null) spriteRenderer = visual.GetComponent<SpriteRenderer>();
            frames = EffectLibrary.LoadRuntimeFrames("Player/Hurt");
            health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(float amount)
        {
            if (health.IsDead || frames == null || frames.Length < 2) return;
            elapsed = 0f;
        }

        private void LateUpdate()
        {
            if (elapsed >= TotalTime || spriteRenderer == null) return;
            elapsed += Time.deltaTime;
            if (elapsed >= TotalTime || health.IsDead) return; // 다음 프레임부터 Animator 그림이 그대로 보인다
            spriteRenderer.sprite = frames[elapsed < FirstFrameTime ? 0 : 1];
        }
    }
}
