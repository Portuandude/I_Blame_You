using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 적 전용 피격 그림 (로드맵 "적별 전용 경직 그림"): 맞은 순간 X 눈 + 팔 번쩍 + 몸 찌그러짐 그림, 이어서 반쯤 돌아온 그림을 잠깐 보여 준다.
    // 그림은 Resources/Enemies/<오브젝트 이름>/Hurt (Tools/ArtGen/generate_sprites.py --hurt, 고블린 4종). 다른 적 그림과 같은 아래 가운데 피벗.
    // 슬라임 무리(속성 슬라임 Slime_<속성>, 어미 슬라임, 슬라임 기사, 슬라임 킹)는 모두 Enemies/Slime/Hurt (decorate_slime.py --hurt)를 쓴다.
    // 속성 슬라임·기사는 ElementRecolor(실행 순서 1000)가 이 그림을 같은 이름의 회색판·투구판으로 다시 바꿔 끼운다.
    // PlayerHurtPose처럼 Animator가 그림을 정한 뒤 LateUpdate에서 덮어쓴다 (애니메이터 클립·프리팹 재생성 불필요).
    // HitStun의 몸 젖히기(Visual 회전)와 머리 위 별(StunStars)은 그대로 겹친다. 그림이 없는 적은 아무것도 하지 않는다.
    // 이름은 스폰 뒤에 정해지므로(EnemySpawner.Spawn) 처음 맞을 때 읽는다.
    public class EnemyHurtPose : MonoBehaviour
    {
        private const float FirstFrameTime = 0.1f;
        private const float TotalTime = 0.25f;
        private const float BossMinDamageRatio = 0.03f; // 보스(슬라임 킹)는 최대 체력 3% 이상 큰 피해일 때만 (맞을 때마다 얼굴이 바뀌면 산만하다)

        private HealthSystem health;
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private bool loaded;
        private bool boss;
        private float elapsed = TotalTime;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            var visual = transform.Find("Visual");
            if (visual != null) spriteRenderer = visual.GetComponent<SpriteRenderer>();
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(float amount)
        {
            if (!loaded)
            {
                loaded = true;
                boss = name == "SlimeKing";
                // 킹은 그림이 있을 때만 회색판(ElementRecolor)을 쓴다. 그림 없이 원으로 그린 킹에 슬라임 그림을 덮지 않는다.
                frames = EffectLibrary.LoadRuntimeFrames($"Enemies/{FolderFor(name)}/Hurt", new Vector2(0.5f, 0f));
                if (boss && (spriteRenderer == null || spriteRenderer.GetComponent<ElementRecolor>() == null)) frames = null;
            }
            if (health.IsDead || frames == null || frames.Length < 2 || spriteRenderer == null) return;
            if (boss && amount < health.Max * BossMinDamageRatio) return;
            elapsed = 0f;
        }

        // 슬라임 킹도 같은 그림 (회색판에 왕 색을 곱한다). 단 큰 피해일 때만 (BossMinDamageRatio).
        internal static string FolderFor(string objectName)
        {
            if (objectName == "Slime" || objectName == "SlimeKing" || objectName.StartsWith("Slime_") || objectName == "MotherSlime" || objectName.StartsWith("SlimeKnight"))
                return "Slime";
            return objectName;
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
