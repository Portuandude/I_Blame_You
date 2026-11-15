using UnityEngine;

namespace IBlameYou.Player
{
    // 불꽃 비의 불똥 하나: 아래로 떨어지다 땅(0)에 닿으면 구역 꼭대기로 돌아간다 (FireWall rain).
    public class FireRainDrop : MonoBehaviour
    {
        private float top;
        private float speed;

        public void Begin(float height)
        {
            top = height;
            speed = Random.Range(7f, 10f);
        }

        private void Update()
        {
            var p = transform.localPosition;
            p.y -= speed * Time.deltaTime;
            if (p.y < 0.1f) p.y = top;
            transform.localPosition = p;
        }
    }
}
