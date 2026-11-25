using UnityEngine;

namespace IBlameYou.Systems
{
    // Animator 에셋 없이 스프라이트 배열을 순서대로 바꿔 끼우는 간단한 프레임 애니메이션 (투사체/이펙트용).
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFrameAnimator : MonoBehaviour
    {
        private Sprite[] frames;
        private float frameRate = 12f;
        private bool loop = true;
        private bool destroyWhenDone;
        private SpriteRenderer spriteRenderer;
        private float time;

        public void Play(Sprite[] sprites, float fps, bool looping, bool destroyAtEnd = false)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            frames = sprites;
            frameRate = fps;
            loop = looping;
            destroyWhenDone = destroyAtEnd;
            time = 0f;
            if (frames != null && frames.Length > 0) spriteRenderer.sprite = frames[0];
        }

        // 루프 그림 여러 개를 나란히 둘 때 서로 다른 프레임에서 시작하게 (불꽃 장막 기둥).
        public void SkipFrames(int count)
        {
            if (frames == null || frames.Length == 0 || frameRate <= 0f) return;
            time += count / frameRate;
            spriteRenderer.sprite = frames[Mathf.FloorToInt(time * frameRate) % frames.Length];
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;

            time += Time.deltaTime;
            int index = Mathf.FloorToInt(time * frameRate);
            if (index >= frames.Length)
            {
                if (destroyWhenDone)
                {
                    Destroy(gameObject);
                    return;
                }

                index = loop ? index % frames.Length : frames.Length - 1;
            }

            spriteRenderer.sprite = frames[index];
        }
    }
}
