using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 대쉬하는 동안 일정 간격으로 마법사 그림의 잔상을 남긴다 (aqua, Docs/ArtStyle.md "잔상").
    // 잔상 수명(0.16초)과 간격(0.05초)이 맞물려 화면에는 보통 3개(진한 것 → 옅은 것)만 보인다.
    [RequireComponent(typeof(PlayerMovement))]
    public class DashAfterimage : MonoBehaviour
    {
        private const float Interval = 0.05f;

        private PlayerMovement movement;
        private SpriteRenderer visual;
        private float nextSpawn;
        private bool wasDashing;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            var child = transform.Find("Visual");
            visual = child != null ? child.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            bool dashing = movement != null && movement.IsDashing;
            if (dashing && (!wasDashing || Time.time >= nextSpawn))
            {
                Afterimage.Spawn(visual, Afterimage.Aqua);
                nextSpawn = Time.time + Interval;
            }

            wasDashing = dashing;
        }
    }
}
