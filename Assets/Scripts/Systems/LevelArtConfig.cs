using UnityEngine;

namespace IBlameYou.Systems
{
    // Tools/I Blame You/Generate Sprites And Animations 메뉴로 생성/갱신되는 설정 에셋.
    // Assets/Resources에 두고 런타임에서 Resources.Load로 읽는다.
    [CreateAssetMenu(fileName = "LevelArtConfig", menuName = "I Blame You/Level Art Config")]
    public class LevelArtConfig : ScriptableObject
    {
        public Sprite groundTile;
        public RuntimeAnimatorController playerAnimatorController;
        public Sprite playerDefaultSprite;
        public RuntimeAnimatorController slimeAnimatorController;
        public Sprite slimeDefaultSprite;
    }
}
