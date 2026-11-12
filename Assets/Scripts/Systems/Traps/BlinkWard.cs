using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 마력 안개: 마법 스위치 발판 낭떠러지 위에 깔린 옅은 보랏빛 기둥. 안에서는 순간이동·대시가 흩어진다.
    // - 순간이동: 출발점·도착점·지나는 길이 안개에 닿으면 발동하지 않는다 (TeleportSkill).
    // - 대시: 안개 안에서는 시작하지 못하고, 대시하다 들어오면 그 자리에서 끝난다 (PlayerMovement).
    // 그래서 스위치 발판 낭떠러지(9칸 이상)는 룬을 맞혀 발판을 타야만 건넌다 (2026-11-12 플레이테스트 피드백).
    // 몸이 지나가는 순수 영역이라 콜라이더가 없다. 안개 목록은 정적으로 모아 두고 사각형으로만 판단한다.
    public class BlinkWard : MonoBehaviour
    {
        private static readonly List<BlinkWard> Active = new List<BlinkWard>();
        private const int MoteCount = 6;
        private static readonly Color MistColor = new Color(0.62f, 0.45f, 0.95f, 0.10f);
        private static readonly Color MoteColor = new Color(0.85f, 0.7f, 1f, 0.55f);

        private Rect area;
        private SpriteRenderer mist;
        private Transform[] motes;
        private float[] moteSeeds;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        // bottomLeft: 안개 영역 왼쪽 아래 (월드), width·height: 유닛.
        public static BlinkWard Create(Transform parent, Vector2 bottomLeft, float width, float height)
        {
            var go = new GameObject("BlinkWard");
            go.transform.SetParent(parent, false);
            go.transform.position = bottomLeft;
            var ward = go.AddComponent<BlinkWard>();
            ward.area = new Rect(bottomLeft, new Vector2(width, height));

            ward.mist = TrapCommon.AddSprite(go.transform, "Mist", null, new Vector2(width / 2f, height / 2f),
                new Vector2(width, height), MistColor, -1);

            // 천천히 떠오르는 작은 빛 알갱이: 여기는 무언가 다르다는 모양 신호 (문구 없이).
            ward.motes = new Transform[MoteCount];
            ward.moteSeeds = new float[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                var mote = TrapCommon.AddSprite(go.transform, $"Mote_{i}", null, Vector2.zero, new Vector2(0.18f, 0.18f), MoteColor, 2);
                ward.motes[i] = mote.transform;
                ward.moteSeeds[i] = (i + 0.5f) / MoteCount;
            }
            return ward;
        }

        private void Update()
        {
            if (mist != null)
            {
                float a = MistColor.a * (0.8f + 0.2f * Mathf.Sin(Time.time * 1.7f));
                mist.color = new Color(MistColor.r, MistColor.g, MistColor.b, a);
            }
            if (motes == null) return;
            const float riseTime = 5f;
            const float riseHeight = 7f;
            float baseY = FloorGuess();
            for (int i = 0; i < motes.Length; i++)
            {
                if (motes[i] == null) continue;
                float t = Mathf.Repeat(Time.time / riseTime + moteSeeds[i], 1f);
                float x = area.width * Mathf.Repeat(moteSeeds[i] * 3.7f + 0.13f, 1f) + 0.3f * Mathf.Sin(Time.time * 2f + i);
                motes[i].localPosition = new Vector3(x, baseY + t * riseHeight, 0f);
            }
        }

        // 알갱이는 낭떠러지 아래가 아니라 사람 눈높이 근처(영역 아래에서 1/3쯤)에서 떠오른다.
        private float FloorGuess() => area.height / 3f;

        public static bool Contains(Vector2 point)
        {
            foreach (var ward in Active)
                if (ward != null && ward.area.Contains(point)) return true;
            return false;
        }

        // from → to 선분이 안개에 닿는지 (순간이동 길). 순간이동은 상하좌우뿐이지만 일반 선분으로 본다.
        public static bool Touches(Vector2 from, Vector2 to)
        {
            foreach (var ward in Active)
            {
                if (ward == null) continue;
                var r = ward.area;
                if (r.Contains(from) || r.Contains(to)) return true;
                float minX = Mathf.Min(from.x, to.x), maxX = Mathf.Max(from.x, to.x);
                float minY = Mathf.Min(from.y, to.y), maxY = Mathf.Max(from.y, to.y);
                if (maxX < r.xMin || minX > r.xMax || maxY < r.yMin || minY > r.yMax) continue;
                // 상하좌우 선분이면 경계 상자가 겹치는 것으로 충분하다. 대각선은 안개 네 모서리가 선분 양쪽에 갈리는지 본다.
                if (Mathf.Approximately(from.x, to.x) || Mathf.Approximately(from.y, to.y)) return true;
                float s0 = Side(from, to, new Vector2(r.xMin, r.yMin));
                float s1 = Side(from, to, new Vector2(r.xMax, r.yMin));
                float s2 = Side(from, to, new Vector2(r.xMin, r.yMax));
                float s3 = Side(from, to, new Vector2(r.xMax, r.yMax));
                if (!(s0 > 0f && s1 > 0f && s2 > 0f && s3 > 0f) && !(s0 < 0f && s1 < 0f && s2 < 0f && s3 < 0f)) return true;
            }
            return false;
        }

        private static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }
}
