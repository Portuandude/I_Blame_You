using System;

namespace IBlameYou.Systems
{
    // 선악도 방 (2026-11-24 사람 결정, Docs/OpenDecisions.md P2·P3): 스테이지마다 0~1개, 적 없음.
    // P3은 "방마다 무작위 (인과율 감소 / 선악 선택 이벤트)"인데, 인과율을 줄이는 방법(K5)은 사람이 나중에 정하므로
    // 지금은 선악 선택 이벤트만 나온다. 판에서 처음 들어가면 A·B 선택지 룰렛 창(ChoiceRouletteRule)이 뜨고,
    // 각자 고른 선택지대로 선악도가 바뀌며(숨은 값, 화면에 숫자 없음), 룰렛으로 정해진 선택지의 골드·체력 결과가 바로 일어난다.
    // 이벤트 글은 NPC·서사가 정해지기 전의 가안이다 (NPC가 나오지 않는 짧은 장면만).
    public static class AlignmentRoomRule
    {
        public const int Chance = 2; // 1/Chance 확률로 그 판에 선악도 방이 하나 (0~1개)

        public struct Option
        {
            public string Text;
            public int Alignment;   // 고른 사람의 선악도 변화
            public int Gold;        // 이 선택지가 일어나면 골드 (음수 = 내기, 가진 만큼만)
            public float HealthPct; // 이 선택지가 일어나면 체력 (최대 체력 비율, 음수 = 잃기, 쓰러지지는 않음)

            public Option(string text, int alignment, int gold = 0, float healthPct = 0f)
            {
                Text = text;
                Alignment = alignment;
                Gold = gold;
                HealthPct = healthPct;
            }
        }

        public struct Event
        {
            public string Title;
            public Option[] Options;

            public Event(string title, params Option[] options)
            {
                Title = title;
                Options = options;
            }
        }

        // 가안 이벤트 (선한 쪽은 대가가 있거나 보상이 없고, 악한 쪽은 당장 이득이 있다)
        public static readonly Event[] Events =
        {
            new Event("쓰러진 여행자",
                new Option("가진 약을 나눠 준다", 3, healthPct: -0.15f),
                new Option("모른 척 지나간다", 0),
                new Option("짐을 뒤진다", -3, gold: 30)),
            new Event("이끼 낀 제단",
                new Option("골드를 바치고 기도한다", 2, gold: -20, healthPct: 0.3f),
                new Option("기도만 하고 간다", 1),
                new Option("박힌 보석을 떼어 간다", -3, gold: 40)),
            new Event("떨어진 돈주머니",
                new Option("주인이 찾게 그대로 둔다", 2),
                new Option("반만 가져간다", -1, gold: 15),
                new Option("전부 가져간다", -3, gold: 30)),
            new Event("우리에 갇힌 슬라임",
                new Option("우리를 열어 준다", 2),
                new Option("모른 척 지나간다", 0),
                new Option("젤리를 짜내 먹는다", -3, healthPct: 0.25f)),
            new Event("굶주린 아기 고블린",
                new Option("골드를 나눠 준다", 3, gold: -15),
                new Option("무시한다", 0),
                new Option("겁줘서 쫓아낸다", -2)),
        };

        // 그 판(스테이지 시드)·방에서 나올 이벤트. 같은 판이면 늘 같은 이벤트.
        public static int Pick(int stageSeed, int roomIndex)
        {
            int h = unchecked(stageSeed * 397 ^ (roomIndex + 1) * 7919);
            return (int)((uint)h % (uint)Events.Length);
        }

        public static string[] Texts(Event e)
        {
            var texts = new string[e.Options.Length];
            for (int i = 0; i < texts.Length; i++) texts[i] = e.Options[i].Text;
            return texts;
        }

        public static int[] Alignments(Event e)
        {
            var values = new int[e.Options.Length];
            for (int i = 0; i < values.Length; i++) values[i] = e.Options[i].Alignment;
            return values;
        }

        // 실제로 바뀌는 골드 (내는 골드는 가진 만큼만). available: 쓸 수 있는 골드.
        public static int GoldChange(Option o, int available) =>
            o.Gold >= 0 ? o.Gold : -Math.Min(-o.Gold, Math.Max(0, available));

        // 실제로 바뀌는 체력. 잃을 때는 1 아래로 내려가지 않는다 (쓰러진 몸이면 0).
        public static float HealthChange(Option o, float current, float max)
        {
            if (current <= 0f || max <= 0f) return 0f;
            float delta = o.HealthPct * max;
            if (delta >= 0f) return Math.Min(delta, max - current);
            return -Math.Min(-delta, Math.Max(0f, current - 1f));
        }

        // 결과 알림 한 줄 (골드·체력 변화를 글로)
        public static string Summary(Option o, int gold, float health)
        {
            string text = $"\"{o.Text}\"";
            if (gold > 0) text += $" · 골드 +{gold}";
            else if (gold < 0) text += $" · 골드 {gold}";
            if (health >= 0.5f) text += $" · 체력 +{health:0}";
            else if (health <= -0.5f) text += $" · 체력 {health:0}";
            return text;
        }
    }
}
