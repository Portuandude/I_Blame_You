using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 킹 머리 위의 왕관. 슬라임 몸이 출렁이는 높이(현재 프레임의 윗변)를 따라 매 프레임 올려 둔다.
    public class SlimeKingCrown : MonoBehaviour
    {
        private const float Sink = 0.18f; // 머리에 살짝 박히게

        private SpriteRenderer body;
        private SpriteRenderer crown;

        public static SlimeKingCrown Attach(GameObject king, SpriteRenderer bodyRenderer, float size)
        {
            var go = new GameObject("Crown");
            go.transform.SetParent(king.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 2;

            var frames = EffectLibrary.LoadRuntimeFrames("Bosses/SlimeKing/Crown");
            if (frames != null)
            {
                renderer.sprite = frames[0];
                go.transform.localScale = new Vector3(size, size, 1f);
            }
            else
            {
                renderer.sprite = SolidSpriteFactory.CreateSquare();
                renderer.color = new Color(0.95f, 0.75f, 0.3f);
                go.transform.localScale = new Vector3(size, size * 0.5f, 1f);
            }

            var crown = go.AddComponent<SlimeKingCrown>();
            crown.body = bodyRenderer;
            crown.crown = renderer;
            return crown;
        }

        private void LateUpdate()
        {
            if (body == null || body.sprite == null) return;
            var bounds = body.bounds;
            float half = crown.bounds.extents.y;
            transform.position = new Vector3(bounds.center.x, bounds.max.y + half - Sink, transform.position.z);
        }
    }
}
