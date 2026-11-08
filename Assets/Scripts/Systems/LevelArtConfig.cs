using UnityEngine;

namespace IBlameYou.Systems
{
    // Tools/I Blame You/Generate Sprites And Animations 메뉴로 생성/갱신되는 설정 에셋.
    // 아트(스프라이트/애니메이터)와, 그 아트로 구워낸 플레이어/적 프리팹 참조를 담는다.
    // Assets/Resources에 두고 런타임에서 Resources.Load로 읽는다.
    [CreateAssetMenu(fileName = "LevelArtConfig", menuName = "I Blame You/Level Art Config")]
    public class LevelArtConfig : ScriptableObject
    {
        public Sprite groundTile;

        public RuntimeAnimatorController playerAnimatorController;
        public Sprite playerDefaultSprite;
        public GameObject playerPrefab;

        public RuntimeAnimatorController slimeAnimatorController;
        public Sprite slimeDefaultSprite;
        public GameObject slimePrefab;
        public GameObject motherSlimePrefab; // 슬라임 아트를 쓰는 정예 변종

        public RuntimeAnimatorController goblinAnimatorController;
        public Sprite goblinDefaultSprite;
        public GameObject goblinPrefab;

        public RuntimeAnimatorController goblinArcherAnimatorController;
        public Sprite goblinArcherDefaultSprite;
        public GameObject goblinArcherPrefab;

        public RuntimeAnimatorController goblinSoldierAnimatorController;
        public Sprite goblinSoldierDefaultSprite;
        public GameObject goblinSoldierPrefab;

        public RuntimeAnimatorController goblinWarriorAnimatorController;
        public Sprite goblinWarriorDefaultSprite;
        public GameObject goblinWarriorPrefab;

        // 스킬/투사체 이펙트 프레임 (런타임에 SpriteFrameAnimator로 재생).
        public Sprite[] fireballFrames;
        public Sprite[] waterLanceFrames;
        public Sprite[] windGustFrames;
        public Sprite[] earthSpikeFrames;
        public Sprite[] arcaneOrbFrames;
        public Sprite[] shieldFrames;
        public Sprite[] teleportFrames;
        public Sprite[] arrowFrames;
        public Sprite[] slashFrames;
    }
}
