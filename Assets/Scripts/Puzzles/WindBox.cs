using System;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 퍼즐 방 "바람 상자"의 돌 상자 (2026-11-22, Docs/PuzzleRoomDesign.md 4번). 1×1칸 땅(Ground 레이어)이라 A가 올라설 수 있다.
    // B의 바람 마법(투사체)이 맞으면 날아온 쪽의 반대쪽으로 한 칸 미끄러져 멈춘다. 칸 번호는 WindBoxRules가 정하고 여기서는 연출만.
    // 다른 속성·평타는 "팅" 튕긴다. 디딤 자리(선반 아래·바로 옆)에 닿으면 바닥 무늬와 상자 문양이 밝아지며 "딸깍".
    // 마법 하나에 한 칸 (칼바람 3연발도 미끄러지는 동안과 PushCooldown 안의 것은 무시).
    public class WindBox : MonoBehaviour, IElementPushReceiver
    {
        public const float SlideTime = 0.2f;
        public const float PushCooldown = 0.3f;
        private const float DeflectCooldown = 0.4f;
        private const float Size = 0.98f; // 옆 칸 땅 모서리에 걸리지 않게 살짝 작게
        private static readonly Color StoneColor = new Color(0.5f, 0.52f, 0.58f);
        private static readonly Color RimColor = new Color(0.78f, 0.82f, 0.88f);
        private static readonly Color MarkColor = new Color(0.55f, 1f, 0.8f, 0.22f);

        private ChunkData chunk;
        private Vector2 chunkOrigin; // 청크 (0, 0) 칸의 월드 자리
        private int row;             // 상자가 선 칸 (청크 y)
        private int column;          // 지금 칸 (청크 x)
        private int ledgeX0, ledgeWidth;
        private Action<int> moved;

        private Rigidbody2D body;
        private SpriteRenderer sigil;
        private SpriteRenderer mark;
        private Vector3 sigilScale = Vector3.one;
        private Vector2 slideFrom, slideTo;
        private float slideStart = -10f;
        private bool sliding;
        private float nextPushTime;
        private float nextDeflectTime;
        private bool onStep;

        public int Column => column;
        public bool OnStep => onStep;

        // origin: 청크 (0, 0) 칸의 월드 자리. column·row: 지금 상자 칸 (다시 들어온 방이면 밀어 둔 자리).
        public static WindBox Create(Transform parent, ChunkData chunk, Vector2 origin, int column, int row, int ledgeX0, int ledgeWidth,
            Action<int> onMoved)
        {
            // 선반 아래·옆 디딤 자리를 바닥에 옅은 바람빛 띠로 (글 없이 "여기"를 알린다)
            var markRenderer = TrapCommon.AddSprite(parent, "WindBoxMark", null,
                origin + new Vector2(ledgeX0 - 1 + (ledgeWidth + 2) / 2f, row + 0.06f), new Vector2(ledgeWidth + 2, 0.12f), MarkColor, -2);

            var go = new GameObject("WindBox");
            go.transform.SetParent(parent, false);
            go.layer = TrapCommon.GroundLayer;
            go.AddComponent<UnsafeGround>(); // 떨어졌을 때 돌아갈 땅으로 치지 않는다 (움직이는 몸)

            var box = go.AddComponent<WindBox>();
            box.chunk = chunk;
            box.chunkOrigin = origin;
            box.row = row;
            box.column = column;
            box.ledgeX0 = ledgeX0;
            box.ledgeWidth = ledgeWidth;
            box.moved = onMoved;
            box.mark = markRenderer;

            var at = box.CellCenter(column);
            go.transform.position = at;
            box.body = go.AddComponent<Rigidbody2D>();
            box.body.bodyType = RigidbodyType2D.Kinematic;
            box.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            box.body.position = at;
            var solid = go.AddComponent<BoxCollider2D>();
            solid.size = new Vector2(Size, Size);
            solid.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            // 돌 상자: 밝은 테두리 + 어두운 몸 + 가운데 바람 문양 (바람으로 미는 것이라는 모양 신호)
            // 그림(Resources/Stage/WindBox, Tools/ArtGen --windbox)이 있으면 그 돌 상자, 없으면 단색 테두리 + 몸
            var boxSprite = TrapCommon.Frame("WindBox");
            if (boxSprite != null)
                TrapCommon.AddSprite(go.transform, "Stone", boxSprite, Vector2.zero, Vector2.one, Color.white, 2);
            else
            {
                TrapCommon.AddSprite(go.transform, "Rim", null, Vector2.zero, new Vector2(Size, Size), RimColor, 1);
                TrapCommon.AddSprite(go.transform, "Stone", null, Vector2.zero, new Vector2(Size - 0.14f, Size - 0.14f), StoneColor, 2);
            }
            var sigilSprite = TrapCommon.Frame($"RuneSigil_{Element.Wind}");
            box.sigil = TrapCommon.AddSprite(go.transform, "Sigil", sigilSprite, Vector2.zero, new Vector2(0.45f, 0.45f),
                ElementInfo.Color(Element.Wind), 3);
            if (sigilSprite != null) box.sigil.transform.localScale = Vector3.one * 0.7f;
            box.sigilScale = box.sigil.transform.localScale;

            box.onStep = WindBoxRules.IsStep(column, ledgeX0, ledgeWidth);
            box.ApplyLook();
            return box;
        }

        private Vector2 CellCenter(int x) => chunkOrigin + new Vector2(x + 0.5f, row + Size / 2f);

        public void ReceiveElement(Element element, Vector2 direction)
        {
            Vector3 at = transform.position;
            if (element != Element.Wind)
            {
                if (Time.time < nextDeflectTime) return;
                nextDeflectTime = Time.time + DeflectCooldown;
                ElementGateFx.Deflected(at); // 바람이 아니면 "팅" (등불·봉인문과 같은 연출)
                return;
            }
            if (sliding || Time.time < nextPushTime) return;
            nextPushTime = Time.time + PushCooldown;

            int dir = WindBoxRules.DirectionFrom(direction.x);
            int next = WindBoxRules.Push(chunk, column, row, dir);
            EffectLibrary.PlayHit(Element.Wind, at, 0.9f);
            if (next == column || Occupied(next))
            {
                // 못 간다: 상자가 살짝 떨고 "쿵"
                GameAudio.PlaySfx("BoxBump", 0.6f);
                StartCoroutine(Shake(dir));
                return;
            }

            column = next;
            slideFrom = body.position;
            slideTo = CellCenter(next);
            slideStart = Time.time;
            sliding = true;
            GameAudio.PlaySfx("WindGust", 0.35f);
            GameAudio.PlaySfx("BoxSlide", 0.6f); // 돌이 바닥을 긁는 "드르륵-톡"
            moved?.Invoke(column);
        }

        // 가려는 칸에 플레이어가 있으면 밀지 않는다 (몸을 끼우지 않게)
        private bool Occupied(int x)
        {
            var hits = Physics2D.OverlapBoxAll(CellCenter(x), new Vector2(0.8f, 0.8f), 0f);
            foreach (var hit in hits)
                if (hit != null && !hit.isTrigger && TrapCommon.PlayerFrom(hit) != null) return true;
            return false;
        }

        private void FixedUpdate()
        {
            if (!sliding) return;
            float t = Mathf.Clamp01((Time.time - slideStart) / SlideTime);
            float eased = 1f - (1f - t) * (1f - t); // 처음 빠르고 끝에서 느려지며 멈춘다
            body.MovePosition(Vector2.Lerp(slideFrom, slideTo, eased));
            if (t < 1f) return;

            sliding = false;
            EffectLibrary.PlayOneShot(new Vector3(slideTo.x, slideTo.y - Size / 2f + 0.1f, 0f), TrapCommon.Frames("Smoke"), 14f,
                new Color(0.8f, 0.78f, 0.72f, 0.6f), 0.5f);
            bool step = WindBoxRules.IsStep(column, ledgeX0, ledgeWidth);
            if (step && !onStep) GameAudio.PlaySfx("Switch_On", 0.7f, 0f); // 디딤 자리에 닿음: "딸깍"
            onStep = step;
            ApplyLook();
        }

        private System.Collections.IEnumerator Shake(int dir)
        {
            var visual = transform;
            for (int i = 0; i < 4; i++)
            {
                float dx = (i % 2 == 0 ? 0.06f : -0.06f) * (dir == 0 ? 1 : dir);
                foreach (Transform child in visual) child.localPosition = new Vector3(dx, 0f, 0f);
                yield return new WaitForSeconds(0.04f);
            }
            foreach (Transform child in visual) child.localPosition = Vector3.zero;
        }

        private void ApplyLook()
        {
            if (sigil != null)
            {
                var c = ElementInfo.Color(Element.Wind);
                float k = onStep ? 1f : 0.6f;
                sigil.color = new Color(c.r * k + (1f - k) * 0.2f, c.g * k + (1f - k) * 0.2f, c.b * k + (1f - k) * 0.2f, 1f);
            }
            if (mark != null) mark.color = new Color(MarkColor.r, MarkColor.g, MarkColor.b, onStep ? 0.55f : MarkColor.a);
        }

        private void Update()
        {
            if (sigil == null) return;
            // 문양이 천천히 숨 쉰다 (디딤 자리에서는 빠르게)
            float pulse = 1f + (onStep ? 0.08f : 0.05f) * Mathf.Sin(Time.time * (onStep ? 7f : 2.2f));
            sigil.transform.localScale = sigilScale * pulse;
        }
    }
}
