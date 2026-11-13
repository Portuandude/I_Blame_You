using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 근접 고블린(고블린·병사·대전사) 휘두르기 연출 (그림·소리만, 판정 크기·시간·피해는 GoblinController 그대로, 2026-11-13).
    // - Swing: 휘두르는 순간 "붕" (GoblinSwing) + 무기가 지나간 자리에 위→앞으로 그어지는 흰 초승달 궤적
    //   정예(대전사)는 낮은 음 + 더 크고 따뜻한 색 궤적
    // - Connected: 맞힌 자리에 노란 불꽃 조각 (아픈 소리·히트스톱은 플레이어 쪽에서 이미 난다). 정예는 약한 화면 흔들림
    // - Whiffed: 아무도 못 맞히면 무기 끝 아래 땅에 작은 먼지 (정예는 흙 조각도)
    // 소리는 화면 안일 때만, 여러 마리가 한꺼번에 휘둘러도 0.08초에 한 번.
    public class GoblinSwingFx : MonoBehaviour
    {
        private static readonly Color ArcColor = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color EliteArcColor = new Color(1f, 0.82f, 0.55f, 0.85f);
        private static readonly Color SparkColor = new Color(1f, 0.92f, 0.45f, 0.95f);
        private static readonly Color DirtColor = new Color(0.62f, 0.5f, 0.38f, 0.9f);

        private const int Segments = 6;
        private const float SweepTime = 0.08f; // 궤적 조각이 위에서 앞까지 차례로 나타나는 시간
        private const float FadeTime = 0.16f;  // 조각 하나가 옅어지는 시간
        private const float StartAngle = 75f;  // 앞을 0도로 볼 때 위쪽에서 시작
        private const float EndAngle = -35f;   // 앞 아래쪽에서 끝
        private const float SfxGap = 0.08f;

        private static float nextSwingSfx;

        private SpriteRenderer[] pieces;
        private Color color;
        private float age;

        // pivot: 고블린 몸 가운데쯤 (궤적의 중심), facing: -1/1, reach: 궤적 반지름 (판정 앞쪽 끝까지).
        public static void Swing(Vector3 pivot, float facing, float reach, bool elite)
        {
            if (SlimeSplitFx.OnScreen(pivot) && Time.time >= nextSwingSfx)
            {
                nextSwingSfx = Time.time + SfxGap;
                GameAudio.PlaySfxPitched("GoblinSwing", elite ? 0.78f : 1f, elite ? 0.65f : 0.5f);
            }

            var go = new GameObject("GoblinSwingArc");
            go.transform.position = pivot;
            var fx = go.AddComponent<GoblinSwingFx>();
            fx.color = elite ? EliteArcColor : ArcColor;
            fx.pieces = new SpriteRenderer[Segments];

            var square = SolidSpriteFactory.CreateSquare();
            float thickness = elite ? 0.13f : 0.09f;
            float length = reach * (EndAngle - StartAngle) / Segments * Mathf.Deg2Rad; // 호 길이를 조각 수로 나눈 만큼
            length = Mathf.Abs(length) * 1.15f;
            for (int i = 0; i < Segments; i++)
            {
                float t = (i + 0.5f) / Segments;
                float a = Mathf.Lerp(StartAngle, EndAngle, t) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(a) * facing, Mathf.Sin(a), 0f) * reach;

                var piece = new GameObject("Arc");
                piece.transform.SetParent(go.transform, false);
                piece.transform.localPosition = offset;
                // 호를 따라 눕힌다 (접선 방향). 가운데 조각이 가장 굵다.
                float tangent = Mathf.Atan2(-Mathf.Cos(a), Mathf.Sin(a) * facing) * Mathf.Rad2Deg;
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, tangent);
                float bulge = Mathf.Sin(t * Mathf.PI);
                piece.transform.localScale = new Vector3(length, thickness * (0.45f + 0.55f * bulge), 1f);

                var r = piece.AddComponent<SpriteRenderer>();
                r.sprite = square;
                r.color = Color.clear; // 차례가 오면 나타난다
                r.sortingOrder = 6;
                fx.pieces[i] = r;
            }
        }

        // 맞힌 자리 (플레이어 몸 쪽). direction: 휘두른 방향 (-1/1).
        public static void Connected(Vector3 point, float facing, bool elite)
        {
            int count = elite ? 7 : 5;
            for (int i = 0; i < count; i++)
            {
                var v = new Vector3(facing * Random.Range(1.5f, 4f), Random.Range(0.5f, 3.5f), 0f);
                if (i % 3 == 2) v.x = -v.x * 0.4f; // 몇 개는 뒤로 튄다
                SlimeSplitFx.Drop(point, v, SparkColor, Random.Range(0.06f, elite ? 0.12f : 0.09f));
            }

            if (elite && SlimeSplitFx.OnScreen(point)) IBlameYou.Core.CameraFollow.ShakeMain(0.08f, 0.06f);
        }

        // 헛스윙. ground: 무기 끝 아래 땅 (고블린 발 높이).
        public static void Whiffed(Vector3 ground, float facing, bool elite)
        {
            VfxPlayer.Dust(ground, elite ? 0.6f : 0.3f);
            if (!elite) return;
            for (int i = 0; i < 3; i++)
            {
                var v = new Vector3(facing * Random.Range(0.8f, 2.2f), Random.Range(1.5f, 3f), 0f);
                SlimeSplitFx.Drop(ground + Vector3.up * 0.05f, v, DirtColor, Random.Range(0.06f, 0.1f));
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            bool alive = false;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                float born = SweepTime * i / Segments;
                float local = age - born;
                if (local < 0f)
                {
                    alive = true;
                    continue;
                }

                float k = 1f - Mathf.Clamp01(local / FadeTime);
                if (k > 0f) alive = true;
                var c = color;
                c.a *= k;
                pieces[i].color = c;
            }

            if (!alive) Destroy(gameObject);
        }
    }
}
