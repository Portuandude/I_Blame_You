using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 보스 구역 끝의 출구. 보스 구역을 돌파하면 열리고, 들어가면 스테이지 클리어.
    public class StageExit : MonoBehaviour
    {
        private SpriteRenderer visual;
        private bool open;
        private bool used;

        public static StageExit Create(Transform parent, Vector2 position, bool open)
        {
            var go = new GameObject("StageExit");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.5f, 3f);
            trigger.offset = new Vector2(0f, 1.5f);

            var exit = go.AddComponent<StageExit>();
            exit.visual = EffectLibrary.CreateVisual(go.transform, "Portal", TrapCommon.Frames("Exit"), 10f, true,
                new Color(1f, 0.9f, 0.4f), 1.4f, 1);
            exit.visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            exit.SetOpen(open);
            return exit;
        }

        public void SetOpen(bool value)
        {
            open = value;
            var color = visual.color;
            visual.color = new Color(color.r, color.g, color.b, value ? 1f : 0.2f);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!open || used) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;

            used = true;
            var control = player.GetComponent<PlayerControlState>();
            if (control != null) control.Locked = true;
            GameAudio.PlaySfx("Hit_Wind", 0.8f);
            StageEvents.RaiseCleared();
        }
    }
}
