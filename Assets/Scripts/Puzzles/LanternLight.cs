using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 등불 하나: 받침 기둥 + 속성 문양 머리. 몸이 지나가는 트리거라 길을 막지 않는다 (Projectile이 트리거의 IElementReceiver를 부른다).
    public class LanternLight : MonoBehaviour, IElementReceiver
    {
        private const float HeadHeight = 1.3f;
        private const float DeflectCooldown = 0.4f;

        private LanternPuzzle puzzle;
        private SpriteRenderer sigil;
        private SpriteRenderer flame;
        private Color color;
        private Vector3 sigilScale = Vector3.one;
        private bool sigilArt; // 문양 그림이 있으면 그림이 이미 속성 색이라 흰색 기준으로 밝기만 바꾼다
        private float nextDeflectTime;

        public Element Element { get; private set; }
        public int Order { get; private set; }
        public bool Lit { get; private set; }
        public Vector3 HeadPosition => transform.position + Vector3.up * HeadHeight;

        internal static LanternLight Create(Transform parent, LanternPuzzle.LanternSpec spec, LanternPuzzle puzzle)
        {
            var go = new GameObject($"Lantern_{spec.Order}");
            go.transform.SetParent(parent, false);
            go.transform.position = spec.Bottom;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.9f, 1.7f);
            trigger.offset = new Vector2(0f, 0.95f); // 선 칸 바닥 위 0.1 ~ 1.8 (룬과 같은 높이: 그 높이에 서서 쏘면 맞는다)

            var lantern = go.AddComponent<LanternLight>();
            lantern.puzzle = puzzle;
            lantern.Element = spec.Element;
            lantern.Order = spec.Order;
            lantern.color = ElementInfo.Color(spec.Element);

            var postSprite = TrapCommon.Frame("RuneSwitch");
            TrapCommon.AddSprite(go.transform, "Post", postSprite, new Vector2(0f, postSprite != null ? 0.9f : 0.4f),
                new Vector2(0.3f, 0.8f), new Color(0.3f, 0.28f, 0.36f), 1);
            var sigilSprite = TrapCommon.Frame($"RuneSigil_{spec.Element}");
            lantern.sigil = TrapCommon.AddSprite(go.transform, "Sigil", sigilSprite, new Vector2(0f, HeadHeight),
                new Vector2(0.6f, 0.6f), lantern.color, 3);
            lantern.sigilScale = lantern.sigil.transform.localScale;
            lantern.sigilArt = sigilSprite != null;

            // 켜지면 머리 위에 속성 불꽃 (그 속성 마법의 날아가는 그림을 제자리에서 돌린다)
            lantern.flame = EffectLibrary.CreateVisual(go.transform, "Flame", EffectLibrary.FlightFrames(spec.Element), 12f, true,
                lantern.color, 0.55f, 4);
            lantern.flame.transform.localPosition = new Vector3(0f, HeadHeight + 0.55f, 0f);
            lantern.flame.gameObject.SetActive(false);
            lantern.ApplyLook();
            return lantern;
        }

        public void ReceiveElement(Element element) => puzzle.Receive(this, element);

        public void SetLit(bool lit, bool withFx)
        {
            Lit = lit;
            ApplyLook();
            if (!withFx || !lit) return;
            EffectLibrary.PlayHit(Element, HeadPosition, 1.4f);
            GameAudio.PlaySfx($"Hit_{Element}", 0.6f);
            GameAudio.PlaySfx("Switch_On", 0.8f, 0f);
        }

        // 꺼짐: 연기 한 줌.
        public void Snuff()
        {
            Lit = false;
            ApplyLook();
            EffectLibrary.PlayOneShot(HeadPosition + Vector3.up * 0.3f, TrapCommon.Frames("Smoke"), 12f,
                new Color(0.7f, 0.7f, 0.75f, 0.7f), 0.6f);
        }

        // 틀린 속성: 문양이 하얗게 번쩍이고 "팅" (봉인문과 같은 연출).
        public void Deflect()
        {
            if (Time.time < nextDeflectTime) return;
            nextDeflectTime = Time.time + DeflectCooldown;
            ElementGateFx.Deflected(HeadPosition);
            if (sigil != null) StartCoroutine(Flash());
        }

        private System.Collections.IEnumerator Flash()
        {
            sigil.color = Color.white;
            yield return new WaitForSeconds(0.15f);
            ApplyLook();
        }

        private void ApplyLook()
        {
            if (flame != null) flame.gameObject.SetActive(Lit);
            if (sigil == null) return;
            sigil.transform.localScale = sigilScale;
            Color baseColor = sigilArt ? Color.white : color;
            float k = Lit ? 1f : 0.55f;
            sigil.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, Lit ? 1f : 0.85f);
        }

        private void Update()
        {
            if (sigil == null) return;
            // 꺼져 있으면 천천히 숨 쉬고, 켜져 있으면 살짝 일렁인다 (룬과 같은 말투).
            float pulse = Lit ? 1f + 0.06f * Mathf.Sin(Time.time * 9f) : 1f + 0.1f * Mathf.Sin(Time.time * 2.4f + Order);
            sigil.transform.localScale = sigilScale * pulse;
        }
    }
}
