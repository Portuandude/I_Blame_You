using System;

namespace IBlameYou.Systems
{
    // 두 사람의 성향 묶음 (엔딩 표의 "둘 다 선 / 다름 / 둘 다 악"). 중립은 없다 (OpenDecisions E1: 0 이하 악, 양수 선).
    public enum AlignmentPair
    {
        BothGood,
        Mixed,
        BothEvil
    }

    // 엔딩 (Docs/DesignDecisionsTodo.md B1 엔딩 표, 2026-11-24 07:29 사람 결정). 이름은 표의 번호 그대로 (가1 = Ga1).
    // 저장(엔딩 목록, E7)이 번호로 기억할 수 있으니 새 엔딩은 맨 뒤에만 더한다.
    public enum EndingId
    {
        Ga1, Ga2, Ga3, Ga4,             // 인과율 낮음 + 마지막 스테이지 승리
        Na1, Na2, Na3, Na4, Na5, Na6,   // 인과율 낮음 + 마지막 스테이지 패배
        Da1, Da2, Da3, Da4, Da5, Da6,   // 인과율 보통 이상 + 마지막 스테이지 승리
        Ra1,                            // 인과율 보통 이상 + 패배 + 핵심 NPC 생존: 핵심 NPC의 희생으로 재도전 (엔딩 아님)
        Ra2                             // 인과율 보통 이상 + 패배 + 핵심 NPC 사망: 두 영혼 소멸
    }

    // 판 도중 인과율이 임계치(100)에 닿았을 때 (표 "마", E3·E8)
    public enum OverflowOutcome
    {
        Vanish,           // 마1 (둘 다 악): 더는 부활하지 못하고 두 영혼 소멸
        ExtraRevive,      // 마2 (다름), 마3 (둘 다 선 + 핵심 NPC 이미 사망): 부활 1번 더
        NpcSacrificeReset // 마3 (둘 다 선 + 핵심 NPC 생존): 핵심 NPC가 희생해 인과율 초기화
    }

    // 멀티 엔딩 판정 규칙 (순수 계산, 연출·글은 스토리가 정해진 뒤). 가르는 것: 인과율 · 마지막 스테이지 승패 · 두 사람 성향 · 핵심 NPC 생사.
    // - 인과율 "낮음" = HiddenStatsRule.NormalFrom(25) 미만, 그 위(보통·높음·임계치)는 모두 "보통 이상".
    // - 핵심 NPC는 1명 (E6). 마지막 스테이지에서 지면 부활 없이 바로 엔딩 (E4, 라1만 예외). 라1 재도전에서 또 지면 라2 (E5).
    // - 엔딩을 본 뒤에는 새 게임, 엔딩 목록만 남는다 (E7). 판 도중 쓰러짐(도시 부활)은 엔딩이 아니다.
    // - 임계치를 넘은 뒤 "부활 1번 더"를 쓰고 다시 넘으면 바로 다시 마1~마3 판정 (E8).
    public static class EndingRule
    {
        public static AlignmentPair Pair(int alignmentA, int alignmentB)
        {
            bool goodA = HiddenStatsRule.IsGood(alignmentA);
            bool goodB = HiddenStatsRule.IsGood(alignmentB);
            if (goodA && goodB) return AlignmentPair.BothGood;
            if (!goodA && !goodB) return AlignmentPair.BothEvil;
            return AlignmentPair.Mixed;
        }

        public static bool IsLowCausality(int causality) => causality < HiddenStatsRule.NormalFrom;

        // 마지막 스테이지(5)가 끝났을 때의 엔딩. retried = 라1 재도전 중이었는지 (그때 지면 라2).
        public static EndingId Final(int causality, bool wonFinalStage, int alignmentA, int alignmentB, bool keyNpcAlive, bool retried = false)
        {
            var pair = Pair(alignmentA, alignmentB);
            if (IsLowCausality(causality))
            {
                if (wonFinalStage)
                {
                    if (!keyNpcAlive) return EndingId.Ga4; // 성향 상관없음 (대사만 다름)
                    return ByPair(pair, EndingId.Ga1, EndingId.Ga2, EndingId.Ga3);
                }

                return keyNpcAlive
                    ? ByPair(pair, EndingId.Na4, EndingId.Na5, EndingId.Na6)
                    : ByPair(pair, EndingId.Na1, EndingId.Na2, EndingId.Na3);
            }

            if (wonFinalStage)
            {
                return keyNpcAlive
                    ? ByPair(pair, EndingId.Da4, EndingId.Da5, EndingId.Da6)
                    : ByPair(pair, EndingId.Da1, EndingId.Da2, EndingId.Da3);
            }

            return keyNpcAlive && !retried ? EndingId.Ra1 : EndingId.Ra2; // 성향 상관없음
        }

        // 라1만 엔딩이 아니다 (재도전). 나머지는 엔딩 목록(E7)에 남는다.
        public static bool IsRetry(EndingId id) => id == EndingId.Ra1;

        // 라1은 핵심 NPC가 희생하므로 재도전 판에서 핵심 NPC는 죽은 것으로 친다.
        public static bool KeyNpcAliveAfter(EndingId id, bool keyNpcAlive) => id != EndingId.Ra1 && keyNpcAlive;

        // 판 도중 인과율이 임계치에 닿았는지 (HiddenStatsRule.Band == Overflow)
        public static bool ReachedThreshold(int causality) => causality >= HiddenStatsRule.MaxCausality;

        public static OverflowOutcome Overflow(int alignmentA, int alignmentB, bool keyNpcAlive)
        {
            switch (Pair(alignmentA, alignmentB))
            {
                case AlignmentPair.BothEvil: return OverflowOutcome.Vanish;
                case AlignmentPair.Mixed: return OverflowOutcome.ExtraRevive;
                default: return keyNpcAlive ? OverflowOutcome.NpcSacrificeReset : OverflowOutcome.ExtraRevive;
            }
        }

        // 마지막 스테이지에서 나오는 서로 다른 결말 수 (16): 나4·나5는 같은 결말, 라1은 재도전이라 뺀다. 표 아래 "약 18개"는 어림.
        public static int DistinctEndingCount
        {
            get
            {
                int count = 0;
                foreach (EndingId id in Enum.GetValues(typeof(EndingId)))
                {
                    if (!IsRetry(id) && SameResultAs(id) == id) count++;
                }
                return count;
            }
        }

        // 같은 결말로 치는 엔딩: 나5 = 나4 (NPC의 희생으로 악신을 저지, 둘은 귀환)
        public static EndingId SameResultAs(EndingId id) => id == EndingId.Na5 ? EndingId.Na4 : id;

        // 표의 번호 글 (가1 등)
        public static string Code(EndingId id)
        {
            string name = id.ToString();
            string group;
            switch (name[0])
            {
                case 'G': group = "가"; break;
                case 'N': group = "나"; break;
                case 'D': group = "다"; break;
                default: group = "라"; break;
            }
            return group + name.Substring(2);
        }

        // 한 줄 요약 (표 그대로, 연출 글은 스토리 완성 뒤)
        public static string Summary(EndingId id)
        {
            switch (id)
            {
                case EndingId.Ga1: return "아무도 희생하지 않고 분리되어 귀환";
                case EndingId.Ga2: return "핵심 NPC의 희생으로 귀환";
                case EndingId.Ga3: return "핵심 NPC를 사살, 두 영혼 소멸";
                case EndingId.Ga4: return "악신을 몰아내고 귀환";
                case EndingId.Na1: return "흥미를 잃은 악신이 돌려보냄";
                case EndingId.Na2: return "악신의 하수인";
                case EndingId.Na3: return "73번째 악마";
                case EndingId.Na4:
                case EndingId.Na5: return "NPC의 희생으로 악신을 저지, 귀환";
                case EndingId.Na6: return "NPC를 팔아넘기고 귀환";
                case EndingId.Da1: return "한 몸으로 잔류, 영웅";
                case EndingId.Da2: return "한 몸으로 잔류, 비판받는 영웅";
                case EndingId.Da3: return "한 몸으로 잔류, 폭군";
                case EndingId.Da4: return "두 몸으로 잔류, 영웅";
                case EndingId.Da5: return "두 몸으로 잔류, 영웅과 뒷세계의 왕";
                case EndingId.Da6: return "한 몸으로 잔류, 뒷세계의 왕";
                case EndingId.Ra1: return "핵심 NPC의 희생으로 재도전";
                default: return "두 영혼 소멸";
            }
        }

        private static EndingId ByPair(AlignmentPair pair, EndingId bothGood, EndingId mixed, EndingId bothEvil)
        {
            switch (pair)
            {
                case AlignmentPair.BothGood: return bothGood;
                case AlignmentPair.Mixed: return mixed;
                default: return bothEvil;
            }
        }
    }
}
