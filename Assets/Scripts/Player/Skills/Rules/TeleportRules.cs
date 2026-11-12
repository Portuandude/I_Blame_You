using System;

namespace IBlameYou.Player
{
    // 순간이동의 방향·거리 규칙 (순수 C#, EditMode 테스트 대상). 물리 조회는 TeleportSkill이 하고 그 결과만 여기로 넘긴다.
    // - 방향은 상/하/좌/우 넷뿐: W/A/S/D 입력에서 가장 큰 축 하나만 쓴다 (같으면 세로 우선). 입력이 없으면 바라보는 쪽.
    // - 좌/우/위: 땅·발판에 막히면 그 앞에서 멈춘다 (위로 갈 때는 발판 아래면도 막는다).
    // - 아래: 발판 역할 오브젝트(한쪽 발판·움직이는/스위치 발판·무너지는 발판)는 뚫고 내려가지만 땅은 뚫지 않는다. 낙사 방지로, 도착 지점 바로 아래(MaxSafeDrop 안)에 디딜 곳이 있고
    //   그곳이 낙사 높이보다 위인 지점까지만 간다. 그런 지점이 MinTravel 이상 떨어진 곳에 하나도 없으면 발동하지 않는다.
    public static class TeleportRules
    {
        public const float MaxSafeDrop = 2.5f; // 점프 높이(약 2.45칸) 정도: 이보다 깊으면 바닥이 안 보이는 곳으로 떨어지는 것으로 본다
        public const float MinTravel = 0.5f;   // 이보다 짧게밖에 못 가면 실패로 친다 (마나를 쓰지 않는다)
        public const float Step = 0.25f;       // 아래 방향에서 안전한 지점을 찾을 때 거리를 줄여 가는 간격
        public const float Skin = 0.05f;       // 막힌 면 앞에서 남기는 틈

        // W/A/S/D 입력(x, y)을 네 방향 하나로 바꾼다. dx, dy 중 하나만 ±1이 된다.
        public static void Cardinal(float x, float y, int facing, out int dx, out int dy)
        {
            const float dead = 0.01f;
            if (Math.Abs(x) < dead && Math.Abs(y) < dead)
            {
                dx = facing < 0 ? -1 : 1;
                dy = 0;
                return;
            }

            if (Math.Abs(y) >= Math.Abs(x))
            {
                dx = 0;
                dy = y > 0f ? 1 : -1;
            }
            else
            {
                dx = x > 0f ? 1 : -1;
                dy = 0;
            }
        }

        // 막는 면까지의 거리(hitDistance, 없으면 PositiveInfinity)를 보고 실제로 갈 거리. 좌/우/위에 쓴다.
        public static float Blocked(float distance, float hitDistance)
        {
            if (float.IsNaN(hitDistance) || float.IsInfinity(hitDistance)) return distance;
            return Math.Min(distance, Math.Max(hitDistance - Skin, 0f));
        }

        // 아래 방향 거리. 실패면 -1.
        // solidHit: 아래로 땅(발판 제외)까지의 거리, 없으면 PositiveInfinity.
        // overlapsAt(t): t만큼 내려간 자리에서 몸이 땅·발판과 겹치는지.
        // dropAt(t): t만큼 내려간 자리에서 발 밑 디딜 곳(땅·발판)까지의 거리, MaxSafeDrop 안에 없으면 PositiveInfinity.
        // feetY: 지금 발 높이, killY: 이 높이 이하로 내려가면 낙사(FallRecovery)로 본다.
        public static float PlanDown(float distance, float solidHit, float feetY, float killY,
            Func<float, bool> overlapsAt, Func<float, float> dropAt)
        {
            float max = Blocked(distance, solidHit);
            if (max < MinTravel) return -1f;

            int steps = (int)Math.Floor((max - MinTravel) / Step + 1e-4);
            for (int i = 0; i <= steps; i++)
            {
                float t = i == 0 ? max : max - i * Step;
                if (t < MinTravel) break;
                if (overlapsAt(t)) continue;

                float drop = dropAt(t);
                if (float.IsNaN(drop) || drop > MaxSafeDrop) continue;
                if (feetY - t - drop <= killY) continue;
                return t;
            }

            return -1f;
        }
    }
}
