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
    }
}
